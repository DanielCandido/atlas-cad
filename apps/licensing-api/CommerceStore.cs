using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Licensing;
using Npgsql;

namespace Atlas.Licensing.Api;

public sealed class CommerceStore(
    NpgsqlDataSource dataSource,
    CommerceOptions options,
    AsaasClient asaas,
    ILogger<CommerceStore> logger)
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public async Task<CheckoutResponse> RegisterAsync(RegisterSubscriptionRequest request, CancellationToken cancellationToken)
    {
        ValidateRegistration(request);
        if (!options.Plans.TryGetValue(request.PlanCode, out var plan))
            throw new LicenseServerException("plan_invalid", "Plano inválido.", 400);
        var billingType = request.BillingType.Trim().ToUpperInvariant();
        var taxId = Digits(request.TaxId);
        if (billingType is not ("PIX" or "BOLETO" or "CREDIT_CARD"))
            throw new LicenseServerException("billing_type_invalid", "Use cartão de crédito, PIX ou boleto.", 400);
        var billing = billingType == "CREDIT_CARD" ? ValidateBillingDetails(request) : null;

        var customerId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                await ExecuteAsync(connection, transaction,
                    "INSERT INTO atlas_customers(id,name,email,tax_id_hash,password_hash) VALUES ($1,$2,$3,$4,$5)",
                    cancellationToken, customerId, request.Name.Trim(), NormalizeEmail(request.Email), HashToken(taxId),
                    PasswordHasher.Hash(request.Password));
                await ExecuteAsync(connection, transaction,
                    "INSERT INTO atlas_subscriptions(id,customer_id,plan_code,status,billing_type) VALUES ($1,$2,$3,'provisioning',$4)",
                    cancellationToken, subscriptionId, customerId, plan.Code, billingType);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            { throw new LicenseServerException("email_in_use", "Já existe uma conta com este e-mail.", 409); }
        }

        try
        {
            var asaasCustomerId = await asaas.CreateCustomerAsync(
                customerId, request.Name, request.Email, taxId, billing, cancellationToken);
            await SaveAsaasCustomerAsync(customerId, asaasCustomerId, cancellationToken);
            if (billingType == "CREDIT_CARD")
            {
                var checkout = await asaas.CreateRecurringCheckoutAsync(
                    subscriptionId, asaasCustomerId, plan, cancellationToken);
                await SaveCheckoutAsync(subscriptionId, checkout, cancellationToken);
            }
            else
            {
                var result = await asaas.CreateSubscriptionAsync(
                    subscriptionId, asaasCustomerId, plan, billingType, cancellationToken);
                await CompleteProvisioningAsync(subscriptionId, result, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao provisionar assinatura {SubscriptionId} no Asaas.", subscriptionId);
            await MarkProvisioningFailedAsync(subscriptionId, cancellationToken);
            var (failedToken, failedExpires) = await CreateSessionAsync(customerId, cancellationToken);
            var failedPortal = await GetPortalAsync(customerId, cancellationToken);
            return new CheckoutResponse(failedToken, failedExpires, null, failedPortal);
        }

        var (token, expires) = await CreateSessionAsync(customerId, cancellationToken);
        var portal = await GetPortalAsync(customerId, cancellationToken);
        return new CheckoutResponse(token, expires, portal.PaymentUrl, portal);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT id,password_hash FROM atlas_customers WHERE email=$1", connection);
        command.Parameters.AddWithValue(email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !PasswordHasher.Verify(request.Password, reader.GetString(1)))
            throw new LicenseServerException("credentials_invalid", "E-mail ou senha inválidos.", 401);
        var customerId = reader.GetGuid(0);
        await reader.DisposeAsync();
        var (token, expires) = await CreateSessionAsync(customerId, cancellationToken);
        return new AuthResponse(token, expires, await GetPortalAsync(customerId, cancellationToken));
    }

    public async Task<Guid> AuthenticateAsync(string? authorization, CancellationToken cancellationToken)
    {
        if (authorization == null || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new LicenseServerException("authentication_required", "Autenticação necessária.", 401);
        var token = authorization[7..].Trim();
        if (token.Length < 40) throw new LicenseServerException("authentication_required", "Sessão inválida.", 401);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT customer_id FROM atlas_sessions WHERE token_hash=$1 AND expires_at>now()", connection);
        command.Parameters.AddWithValue(HashToken(token));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : throw new LicenseServerException("session_expired", "Sua sessão expirou.", 401);
    }

    public async Task<PortalSnapshot> GetPortalAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var repair = new NpgsqlCommand("""
            UPDATE atlas_subscriptions
            SET status='provisioning_failed',updated_at=now()
            WHERE customer_id=$1
              AND status='active'
              AND (asaas_subscription_id IS NULL OR paid_through IS NULL)
            """, connection))
        {
            repair.Parameters.AddWithValue(customerId);
            await repair.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var expire = new NpgsqlCommand("""
            UPDATE atlas_subscriptions
            SET status='checkout_expired',checkout_status='EXPIRED',updated_at=now()
            WHERE customer_id=$1
              AND status='pending_checkout'
              AND checkout_expires_at IS NOT NULL
              AND checkout_expires_at<=now()
            """, connection))
        {
            expire.Parameters.AddWithValue(customerId);
            await expire.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("""
            SELECT c.name,c.email,s.id,s.plan_code,s.status,s.billing_type,s.paid_through,s.payment_url,l.id,a.device_fingerprint
            FROM atlas_customers c
            JOIN atlas_subscriptions s ON s.customer_id=c.id
            LEFT JOIN atlas_licenses l ON l.subscription_id=s.id
            LEFT JOIN atlas_activations a ON a.license_id=l.id AND a.revoked_at IS NULL AND a.generation=l.generation
            WHERE c.id=$1 ORDER BY s.created_at DESC LIMIT 1
            """, connection);
        command.Parameters.AddWithValue(customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LicenseServerException("subscription_not_found", "Assinatura não encontrada.", 404);
        var customerName = reader.GetString(0);
        var email = reader.GetString(1);
        var currentSubscriptionId = reader.GetGuid(2);
        var planCode = reader.GetString(3);
        var status = reader.GetString(4);
        var billingType = reader.IsDBNull(5) ? null : reader.GetString(5);
        DateTimeOffset? paidThrough = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6);
        var paymentUrl = reader.IsDBNull(7) ? null : reader.GetString(7);
        var licenseId = reader.IsDBNull(8) ? null : reader.GetString(8);
        var machineFingerprint = reader.IsDBNull(9) ? null : reader.GetString(9);
        await reader.DisposeAsync();

        var subscriptions = new List<SubscriptionSummary>();
        await using var history = new NpgsqlCommand("""
            SELECT id,plan_code,status,billing_type,paid_through,payment_url,created_at
            FROM atlas_subscriptions WHERE customer_id=$1 ORDER BY created_at DESC
            """, connection);
        history.Parameters.AddWithValue(customerId);
        await using var historyReader = await history.ExecuteReaderAsync(cancellationToken);
        while (await historyReader.ReadAsync(cancellationToken))
        {
            var historyId = historyReader.GetGuid(0);
            var historyPlanCode = historyReader.GetString(1);
            var historyStatus = historyReader.GetString(2);
            var historyPaymentUrl = historyReader.IsDBNull(5) ? null : historyReader.GetString(5);
            subscriptions.Add(new SubscriptionSummary(
                historyId,
                historyPlanCode,
                options.Plans.TryGetValue(historyPlanCode, out var historyPlan) ? historyPlan.Name : historyPlanCode,
                historyStatus,
                historyReader.GetString(3),
                historyReader.IsDBNull(4) ? null : historyReader.GetFieldValue<DateTimeOffset>(4),
                historyPaymentUrl,
                historyReader.GetFieldValue<DateTimeOffset>(6),
                historyId == currentSubscriptionId,
                historyStatus is "provisioning_failed" or "checkout_cancelled" or "checkout_expired" ||
                    (historyStatus is "pending_payment" or "past_due" && historyPaymentUrl == null)));
        }

        var entitled = status == "active" && paidThrough > DateTimeOffset.UtcNow;
        return new PortalSnapshot(customerName, email, planCode,
            options.Plans.TryGetValue(planCode, out var plan) ? plan.Name : planCode,
            status, billingType, paidThrough, entitled, entitled, paymentUrl, licenseId,
            machineFingerprint, subscriptions);
    }

    public async Task<PortalSnapshot> RetrySubscriptionAsync(
        Guid customerId, RetrySubscriptionRequest request, CancellationToken cancellationToken)
    {
        var taxId = Digits(request.TaxId);
        if (taxId.Length is not (11 or 14))
            throw new LicenseServerException("tax_id_invalid", "Informe o CPF ou CNPJ usado no cadastro.", 400);

        ProvisioningRow row;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = new NpgsqlCommand("""
            SELECT c.name,c.email,c.tax_id_hash,c.asaas_customer_id,
                   s.id,s.plan_code,s.status,s.billing_type,s.asaas_subscription_id,
                   s.asaas_checkout_id,s.checkout_status
            FROM atlas_customers c
            JOIN atlas_subscriptions s ON s.customer_id=c.id AND s.status NOT IN ('cancelled','replaced')
            WHERE c.id=$1 ORDER BY s.created_at DESC LIMIT 1
            """, connection))
        {
            command.Parameters.AddWithValue(customerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new LicenseServerException("subscription_not_found", "Assinatura não encontrada.", 404);
            row = new ProvisioningRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetGuid(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10));
        }

        var expectedTaxIdHash = HashToken(taxId);
        var taxIdMatches = string.Equals(row.TaxIdHash, expectedTaxIdHash, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(row.TaxIdHash, taxId, StringComparison.Ordinal);
        if (!taxIdMatches)
            throw new LicenseServerException("tax_id_invalid", "O CPF ou CNPJ não corresponde ao cadastro.", 403);
        if (!options.Plans.TryGetValue(row.PlanCode, out var plan))
            throw new LicenseServerException("plan_invalid", "Plano inválido.", 400);
        if (row.Status is "active" or "suspended" or "cancelled" or "replaced")
            throw new LicenseServerException("subscription_retry_not_allowed", "Esta assinatura não pode ser reprovisionada.", 409);
        var billing = row.BillingType == "CREDIT_CARD" ? ValidateBillingDetails(request) : null;

        try
        {
            if (row.AsaasSubscriptionId != null)
            {
                var payment = await asaas.GetFirstPaymentAsync(row.AsaasSubscriptionId, cancellationToken);
                if (payment == null)
                    throw new LicenseServerException(
                        "payment_not_ready",
                        "O Asaas ainda não disponibilizou a cobrança. Tente novamente em instantes.",
                        409);
                await CompleteProvisioningAsync(
                    row.SubscriptionId,
                    new AsaasSubscriptionResult(row.AsaasSubscriptionId, payment),
                    cancellationToken);
                return await GetPortalAsync(customerId, cancellationToken);
            }

            await SetSubscriptionStatusAsync(row.SubscriptionId, "provisioning", cancellationToken);
            var asaasCustomerId = row.AsaasCustomerId;
            if (asaasCustomerId == null)
            {
                asaasCustomerId = await asaas.CreateCustomerAsync(
                    customerId, row.Name, row.Email, taxId, billing, cancellationToken);
                await SaveAsaasCustomerAsync(customerId, asaasCustomerId, cancellationToken);
            }
            else if (billing != null)
            {
                await asaas.UpdateCustomerBillingAsync(asaasCustomerId, billing, cancellationToken);
            }

            if (row.BillingType == "CREDIT_CARD")
            {
                var checkout = await asaas.CreateRecurringCheckoutAsync(
                    row.SubscriptionId, asaasCustomerId, plan, cancellationToken);
                await SaveCheckoutAsync(row.SubscriptionId, checkout, cancellationToken);
            }
            else
            {
                var result = await asaas.CreateSubscriptionAsync(
                    row.SubscriptionId, asaasCustomerId, plan, row.BillingType, cancellationToken);
                await CompleteProvisioningAsync(row.SubscriptionId, result, cancellationToken);
            }
            return await GetPortalAsync(customerId, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao reprovisionar assinatura {SubscriptionId} no Asaas.", row.SubscriptionId);
            await MarkProvisioningFailedAsync(row.SubscriptionId, cancellationToken);
            throw;
        }
    }

    public async Task<PortalSnapshot> ChangePlanAsync(Guid customerId, string planCode, CancellationToken cancellationToken)
    {
        if (!options.Plans.TryGetValue(planCode, out var plan)) throw new LicenseServerException("plan_invalid", "Plano inválido.", 400);
        var subscription = await GetSubscriptionAsync(customerId, cancellationToken);
        if (subscription.AsaasId == null) throw new LicenseServerException("subscription_not_ready", "Assinatura ainda não foi provisionada.", 409);
        await asaas.UpdateSubscriptionAsync(subscription.AsaasId, plan, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("UPDATE atlas_subscriptions SET plan_code=$1,updated_at=now() WHERE id=$2", connection);
        command.Parameters.AddWithValue(plan.Code); command.Parameters.AddWithValue(subscription.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetPortalAsync(customerId, cancellationToken);
    }

    public async Task CancelAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(customerId, cancellationToken);
        if (subscription.AsaasId != null)
            await asaas.CancelSubscriptionAsync(subscription.AsaasId, cancellationToken);
        else if (subscription.CheckoutId != null && subscription.CheckoutStatus == "ACTIVE")
            await asaas.CancelCheckoutAsync(subscription.CheckoutId, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, "UPDATE atlas_subscriptions SET status='cancelled',updated_at=now() WHERE id=$1", cancellationToken, subscription.Id);
        await ExecuteAsync(connection, transaction,
            "UPDATE atlas_activations SET revoked_at=now() WHERE license_id IN (SELECT id FROM atlas_licenses WHERE subscription_id=$1) AND revoked_at IS NULL",
            cancellationToken, subscription.Id);
        await ExecuteAsync(connection, transaction,
            "UPDATE atlas_licenses SET generation=generation+1 WHERE subscription_id=$1",
            cancellationToken, subscription.Id);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<MachineActivationResponse> RegisterMachineAsync(Guid customerId, string identifier, CancellationToken cancellationToken)
    {
        var machine = ParseAndVerifyMachine(identifier);
        var fingerprint = LicenseCrypto.DeviceFingerprint(LicenseCrypto.PublicParameters(machine.X, machine.Y));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT l.id,l.generation
            FROM atlas_licenses l JOIN atlas_subscriptions s ON s.id=l.subscription_id
            WHERE s.customer_id=$1 AND s.status='active' AND s.paid_through>now()
            ORDER BY s.created_at DESC LIMIT 1 FOR UPDATE OF l
            """, connection, transaction);
        command.Parameters.AddWithValue(customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LicenseServerException("subscription_inactive", "O pagamento precisa estar confirmado antes da ativação.", 403);
        var licenseId = reader.GetString(0); var generation = reader.GetInt32(1);
        await reader.DisposeAsync();
        await using var active = new NpgsqlCommand("SELECT device_fingerprint FROM atlas_activations WHERE license_id=$1 AND generation=$2 AND revoked_at IS NULL", connection, transaction);
        active.Parameters.AddWithValue(licenseId); active.Parameters.AddWithValue(generation);
        var existing = await active.ExecuteScalarAsync(cancellationToken) as string;
        if (existing != null) throw new LicenseServerException("activation_limit", "A licença já está ativa. Cancele a ativação atual antes de trocar de máquina.", 409);
        var code = PostgresLicensingStore.NewActivationCode();
        await ExecuteAsync(connection, transaction,
            "DELETE FROM atlas_activation_codes WHERE license_id=$1 AND used_at IS NULL",
            cancellationToken, licenseId);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO atlas_activation_codes(code_hash,license_id,generation,device_fingerprint,expires_at)
            VALUES ($1,$2,$3,$4,now()+interval '7 days')
            """, cancellationToken, PostgresLicensingStore.HashActivationCode(code), licenseId, generation, fingerprint);
        await transaction.CommitAsync(cancellationToken);
        return new MachineActivationResponse(code, DateTimeOffset.UtcNow.AddDays(7), fingerprint);
    }

    public async Task DeactivateMachineAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT l.id FROM atlas_licenses l JOIN atlas_subscriptions s ON s.id=l.subscription_id
            WHERE s.customer_id=$1 AND s.status NOT IN ('cancelled','replaced') ORDER BY s.created_at DESC LIMIT 1 FOR UPDATE OF l
            """, connection, transaction);
        command.Parameters.AddWithValue(customerId);
        var licenseId = await command.ExecuteScalarAsync(cancellationToken) as string ??
            throw new LicenseServerException("license_not_found", "Licença não encontrada.", 404);
        await ExecuteAsync(connection, transaction, "UPDATE atlas_activations SET revoked_at=now() WHERE license_id=$1 AND revoked_at IS NULL", cancellationToken, licenseId);
        await ExecuteAsync(connection, transaction, "UPDATE atlas_licenses SET generation=generation+1 WHERE id=$1", cancellationToken, licenseId);
        await ExecuteAsync(connection, transaction, "DELETE FROM atlas_activation_codes WHERE license_id=$1 AND used_at IS NULL", cancellationToken, licenseId);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ProcessWebhookAsync(string rawPayload, AsaasWebhook webhook, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhook.Id) || string.IsNullOrWhiteSpace(webhook.Event))
            throw new LicenseServerException("webhook_invalid", "Webhook inválido.", 400);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var eventCommand = new NpgsqlCommand("INSERT INTO atlas_webhook_events(id,event_type,payload) VALUES ($1,$2,$3::jsonb) ON CONFLICT DO NOTHING", connection, transaction);
        eventCommand.Parameters.AddWithValue(webhook.Id); eventCommand.Parameters.AddWithValue(webhook.Event); eventCommand.Parameters.AddWithValue(rawPayload);
        if (await eventCommand.ExecuteNonQueryAsync(cancellationToken) == 0) { await transaction.RollbackAsync(cancellationToken); return; }

        if (webhook.Event.StartsWith("CHECKOUT_", StringComparison.Ordinal))
        {
            var checkout = webhook.Checkout;
            var checkoutId = checkout?.Id;
            if (string.IsNullOrWhiteSpace(checkoutId))
                throw new LicenseServerException("webhook_invalid", "Webhook de checkout sem identificador.", 400);
            var localStatus = webhook.Event switch
            {
                "CHECKOUT_CREATED" => "pending_checkout",
                "CHECKOUT_PAID" => "pending_payment",
                "CHECKOUT_CANCELED" => "checkout_cancelled",
                "CHECKOUT_EXPIRED" => "checkout_expired",
                _ => string.Empty
            };
            if (localStatus.Length > 0)
            {
                await ExecuteAsync(connection, transaction, """
                    UPDATE atlas_subscriptions
                    SET checkout_status=$1,
                        status=CASE
                            WHEN status IN ('active','cancelled','replaced') THEN status
                            WHEN $2='pending_checkout' AND status<>'provisioning' THEN status
                            WHEN $2 IN ('checkout_cancelled','checkout_expired')
                                AND status NOT IN ('provisioning','pending_checkout') THEN status
                            ELSE $2
                        END,
                        updated_at=now()
                    WHERE asaas_checkout_id=$3
                    """, cancellationToken,
                    string.IsNullOrWhiteSpace(checkout!.Status)
                        ? webhook.Event[9..]
                        : checkout.Status,
                    localStatus,
                    checkoutId);
            }
        }
        else
        {
            var asaasSubscriptionId = webhook.Payment?.SubscriptionId ?? webhook.Subscription?.Id;
            if (webhook.Event == "SUBSCRIPTION_CREATED" && webhook.Subscription != null)
            {
                await AssociateAsaasSubscriptionAsync(
                    connection,
                    transaction,
                    webhook.Subscription.Id,
                    webhook.Subscription.CustomerId,
                    webhook.Subscription.ExternalReference,
                    cancellationToken);
                asaasSubscriptionId = webhook.Subscription.Id;
            }
            else if (!string.IsNullOrWhiteSpace(asaasSubscriptionId) && webhook.Payment != null)
            {
                await AssociateAsaasSubscriptionAsync(
                    connection,
                    transaction,
                    asaasSubscriptionId,
                    webhook.Payment.CustomerId,
                    null,
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(asaasSubscriptionId) && webhook.Event == "PAYMENT_CREATED")
            {
                await ExecuteAsync(connection, transaction, "UPDATE atlas_subscriptions SET asaas_payment_id=$1,payment_url=COALESCE(NULLIF($2,''),payment_url),status=CASE WHEN status='active' THEN status ELSE 'pending_payment' END,updated_at=now() WHERE asaas_subscription_id=$3 AND status NOT IN ('cancelled','replaced')",
                    cancellationToken, webhook.Payment?.Id ?? string.Empty, webhook.Payment?.InvoiceUrl ?? string.Empty, asaasSubscriptionId);
            }
            else if (!string.IsNullOrWhiteSpace(asaasSubscriptionId) && webhook.Event is "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED")
            {
                await using var lookup = new NpgsqlCommand("SELECT id,customer_id,plan_code FROM atlas_subscriptions WHERE asaas_subscription_id=$1 AND status NOT IN ('cancelled','replaced') FOR UPDATE", connection, transaction);
                lookup.Parameters.AddWithValue(asaasSubscriptionId);
                await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var subscriptionId = reader.GetGuid(0); var customerId = reader.GetGuid(1); var planCode = reader.GetString(2);
                    await reader.DisposeAsync();
                    var paymentId = webhook.Payment?.Id;
                    if (string.IsNullOrWhiteSpace(paymentId)) throw new LicenseServerException("webhook_invalid", "Webhook de pagamento sem identificador.", 400);
                    await using var paid = new NpgsqlCommand("INSERT INTO atlas_paid_payments(payment_id,subscription_id) VALUES ($1,$2) ON CONFLICT DO NOTHING", connection, transaction);
                    paid.Parameters.AddWithValue(paymentId); paid.Parameters.AddWithValue(subscriptionId);
                    if (await paid.ExecuteNonQueryAsync(cancellationToken) > 0)
                    {
                        var period = options.Plans.TryGetValue(planCode, out var plan) && plan.Cycle == "YEARLY" ? "1 year" : "1 month";
                        await ExecuteAsync(connection, transaction, $"UPDATE atlas_subscriptions SET status='active',paid_through=GREATEST(COALESCE(paid_through,now()),now())+interval '{period}',asaas_payment_id=$1,payment_url=COALESCE(NULLIF($2,''),payment_url),updated_at=now() WHERE id=$3",
                            cancellationToken, paymentId, webhook.Payment?.InvoiceUrl ?? string.Empty, subscriptionId);
                        await ExecuteAsync(connection, transaction, """
                            INSERT INTO atlas_licenses(id,subscription_id,product_id,customer_reference,generation)
                            VALUES ($1,$2,$3,$4,1) ON CONFLICT (subscription_id) DO NOTHING
                            """, cancellationToken, "lic_" + Guid.NewGuid().ToString("N"), subscriptionId, LicensingProtocol.ProductId, customerId.ToString("N"));
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(asaasSubscriptionId) && webhook.Event is "PAYMENT_OVERDUE" or "PAYMENT_REFUNDED" or "PAYMENT_CHARGEBACK_REQUESTED")
            {
                var status = webhook.Event == "PAYMENT_OVERDUE" ? "past_due" : "suspended";
                if (webhook.Event == "PAYMENT_OVERDUE")
                    await ExecuteAsync(connection, transaction, "UPDATE atlas_subscriptions SET status=$1,updated_at=now() WHERE asaas_subscription_id=$2 AND status NOT IN ('cancelled','replaced') AND (asaas_payment_id IS NULL OR asaas_payment_id=$3)", cancellationToken, status, asaasSubscriptionId, webhook.Payment?.Id ?? string.Empty);
                else
                    await ExecuteAsync(connection, transaction, "UPDATE atlas_subscriptions SET status=$1,updated_at=now() WHERE asaas_subscription_id=$2 AND status NOT IN ('cancelled','replaced')", cancellationToken, status, asaasSubscriptionId);
            }
            else if (!string.IsNullOrWhiteSpace(asaasSubscriptionId) && webhook.Event is "SUBSCRIPTION_DELETED" or "SUBSCRIPTION_INACTIVATED")
            {
                await ExecuteAsync(connection, transaction, "UPDATE atlas_subscriptions SET status='cancelled',updated_at=now() WHERE asaas_subscription_id=$1", cancellationToken, asaasSubscriptionId);
            }
        }
        await ExecuteAsync(connection, transaction, "UPDATE atlas_webhook_events SET processed_at=now() WHERE id=$1", cancellationToken, webhook.Id);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task AssociateAsaasSubscriptionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string asaasSubscriptionId,
        string? asaasCustomerId,
        string? externalReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asaasSubscriptionId)) return;

        var affected = 0;
        if (Guid.TryParseExact(externalReference, "N", out var localSubscriptionId))
        {
            await using var byReference = new NpgsqlCommand("""
                UPDATE atlas_subscriptions
                SET asaas_subscription_id=$1,
                    status=CASE WHEN status='active' THEN status ELSE 'pending_payment' END,
                    updated_at=now()
                WHERE id=$2
                  AND status NOT IN ('cancelled','replaced')
                  AND (asaas_subscription_id IS NULL OR asaas_subscription_id=$1)
                """, connection, transaction);
            byReference.Parameters.AddWithValue(asaasSubscriptionId);
            byReference.Parameters.AddWithValue(localSubscriptionId);
            affected = await byReference.ExecuteNonQueryAsync(cancellationToken);
        }

        if (affected == 0 && !string.IsNullOrWhiteSpace(asaasCustomerId))
        {
            await using var byCustomer = new NpgsqlCommand("""
                UPDATE atlas_subscriptions s
                SET asaas_subscription_id=$1,
                    status=CASE WHEN s.status='active' THEN s.status ELSE 'pending_payment' END,
                    updated_at=now()
                FROM atlas_customers c
                WHERE s.customer_id=c.id
                  AND c.asaas_customer_id=$2
                  AND s.status NOT IN ('cancelled','replaced')
                  AND (s.asaas_subscription_id IS NULL OR s.asaas_subscription_id=$1)
                """, connection, transaction);
            byCustomer.Parameters.AddWithValue(asaasSubscriptionId);
            byCustomer.Parameters.AddWithValue(asaasCustomerId);
            await byCustomer.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<string> RequireInstallerDownloadAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var portal = await GetPortalAsync(customerId, cancellationToken);
        if (!portal.CanDownload)
            throw new LicenseServerException(
                "download_not_entitled",
                "Download liberado somente após confirmação do pagamento.",
                403);
        if (!File.Exists(options.InstallerPath))
            throw new LicenseServerException(
                "download_unavailable",
                "O instalador ainda não foi publicado no servidor.",
                503);
        return options.InstallerPath;
    }

    public async Task<string> RequireUninstallerDownloadAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await GetPortalAsync(customerId, cancellationToken);
        if (!File.Exists(options.UninstallerPath))
            throw new LicenseServerException(
                "download_unavailable",
                "O desinstalador ainda nao foi publicado no servidor.",
                503);
        return options.UninstallerPath;
    }

    private static MachineIdentifierPayload ParseAndVerifyMachine(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !identifier.StartsWith("ATLAS1.", StringComparison.Ordinal))
            throw new LicenseServerException("machine_identifier_invalid", "Identificador de máquina inválido.", 400);
        try
        {
            var payload = JsonSerializer.Deserialize<MachineIdentifierPayload>(LicenseCrypto.Base64UrlDecode(identifier[7..]), WebJson) ?? throw new FormatException();
            if (payload.Version != 1 || string.IsNullOrWhiteSpace(payload.Nonce)) throw new FormatException();
            using var key = ECDsa.Create(LicenseCrypto.PublicParameters(payload.X, payload.Y));
            var bytes = Encoding.UTF8.GetBytes($"ATLAS-DEVICE|1|{payload.X}|{payload.Y}|{payload.Nonce}");
            if (!key.VerifyData(bytes, LicenseCrypto.Base64UrlDecode(payload.Proof), HashAlgorithmName.SHA256)) throw new FormatException();
            return payload;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException)
        { throw new LicenseServerException("machine_identifier_invalid", "Identificador de máquina inválido.", 400); }
    }

    private async Task<(string Token, DateTimeOffset Expires)> CreateSessionAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var token = LicenseCrypto.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expires = DateTimeOffset.UtcNow.AddDays(30);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("INSERT INTO atlas_sessions(token_hash,customer_id,expires_at) VALUES ($1,$2,$3)", connection);
        command.Parameters.AddWithValue(HashToken(token)); command.Parameters.AddWithValue(customerId); command.Parameters.AddWithValue(expires);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (token, expires);
    }

    private async Task<SubscriptionRow> GetSubscriptionAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT id,asaas_subscription_id,asaas_checkout_id,checkout_status FROM atlas_subscriptions WHERE customer_id=$1 AND status NOT IN ('cancelled','replaced') ORDER BY created_at DESC LIMIT 1", connection);
        command.Parameters.AddWithValue(customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LicenseServerException("subscription_not_found", "Assinatura não encontrada.", 404);
        return new SubscriptionRow(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private async Task SaveAsaasCustomerAsync(
        Guid customerId, string asaasCustomerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE atlas_customers SET asaas_customer_id=$1,updated_at=now() WHERE id=$2",
            connection);
        command.Parameters.AddWithValue(asaasCustomerId);
        command.Parameters.AddWithValue(customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CompleteProvisioningAsync(
        Guid subscriptionId, AsaasSubscriptionResult result, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE atlas_subscriptions
            SET asaas_subscription_id=$1,
                asaas_payment_id=NULLIF($2,''),
                payment_url=NULLIF($3,''),
                status='pending_payment',
                updated_at=now()
            WHERE id=$4
            """, connection);
        command.Parameters.AddWithValue(result.SubscriptionId);
        command.Parameters.AddWithValue(result.FirstPayment?.Id ?? string.Empty);
        command.Parameters.AddWithValue(result.FirstPayment?.InvoiceUrl ?? string.Empty);
        command.Parameters.AddWithValue(subscriptionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SaveCheckoutAsync(
        Guid subscriptionId, AsaasCheckoutResult checkout, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE atlas_subscriptions
            SET asaas_checkout_id=$1,
                checkout_status='ACTIVE',
                checkout_expires_at=$2,
                payment_url=$3,
                status='pending_checkout',
                updated_at=now()
            WHERE id=$4
            """, connection);
        command.Parameters.AddWithValue(checkout.CheckoutId);
        command.Parameters.AddWithValue(checkout.ExpiresAt);
        command.Parameters.AddWithValue(checkout.Url);
        command.Parameters.AddWithValue(subscriptionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SetSubscriptionStatusAsync(
        Guid subscriptionId, string status, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE atlas_subscriptions SET status=$1,updated_at=now() WHERE id=$2",
            connection);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(subscriptionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkProvisioningFailedAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("UPDATE atlas_subscriptions SET status='provisioning_failed',updated_at=now() WHERE id=$1", connection);
        command.Parameters.AddWithValue(subscriptionId); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateRegistration(RegisterSubscriptionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 3 || request.Name.Length > 150)
            throw new LicenseServerException("name_invalid", "Informe seu nome completo.", 400);
        try { _ = new MailAddress(request.Email); } catch { throw new LicenseServerException("email_invalid", "E-mail inválido.", 400); }
        var taxId = Digits(request.TaxId);
        if (taxId.Length is not (11 or 14)) throw new LicenseServerException("tax_id_invalid", "Informe um CPF ou CNPJ válido.", 400);
        if (request.Password is null || request.Password.Length < 10 || request.Password.Length > 200)
            throw new LicenseServerException("password_invalid", "A senha deve ter pelo menos 10 caracteres.", 400);
    }

    private static AsaasBillingDetails ValidateBillingDetails(RegisterSubscriptionRequest request) =>
        ValidateBillingDetails(
            request.Phone,
            request.PostalCode,
            request.Address,
            request.AddressNumber,
            request.Province);

    private static AsaasBillingDetails ValidateBillingDetails(RetrySubscriptionRequest request) =>
        ValidateBillingDetails(
            request.Phone,
            request.PostalCode,
            request.Address,
            request.AddressNumber,
            request.Province);

    private static AsaasBillingDetails ValidateBillingDetails(
        string phone,
        string postalCode,
        string address,
        string addressNumber,
        string province)
    {
        var phoneDigits = Digits(phone);
        var postalCodeDigits = Digits(postalCode);
        if (phoneDigits.Length is not (10 or 11))
            throw new LicenseServerException("phone_invalid", "Informe um telefone com DDD.", 400);
        if (postalCodeDigits.Length != 8)
            throw new LicenseServerException("postal_code_invalid", "Informe um CEP válido.", 400);
        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length < 3)
            throw new LicenseServerException("address_invalid", "Informe o logradouro.", 400);
        if (string.IsNullOrWhiteSpace(addressNumber))
            throw new LicenseServerException("address_number_invalid", "Informe o número do endereço.", 400);
        if (string.IsNullOrWhiteSpace(province) || province.Trim().Length < 2)
            throw new LicenseServerException("province_invalid", "Informe o bairro.", 400);
        return new AsaasBillingDetails(
            phoneDigits,
            postalCodeDigits,
            address.Trim(),
            addressNumber.Trim(),
            province.Trim());
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static string Digits(string value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record SubscriptionRow(
        Guid Id,
        string? AsaasId,
        string? CheckoutId,
        string? CheckoutStatus);
    private sealed record ProvisioningRow(
        string Name,
        string Email,
        string TaxIdHash,
        string? AsaasCustomerId,
        Guid SubscriptionId,
        string PlanCode,
        string Status,
        string BillingType,
        string? AsaasSubscriptionId,
        string? AsaasCheckoutId,
        string? CheckoutStatus);
}
