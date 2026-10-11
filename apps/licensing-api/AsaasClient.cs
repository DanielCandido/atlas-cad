using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Licensing.Api;

public sealed class AsaasClient(HttpClient http, CommerceOptions options)
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> CreateCustomerAsync(
        Guid customerId,
        string name,
        string email,
        string taxId,
        AsaasBillingDetails? billing,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "customers", new
        {
            name = name.Trim(),
            cpfCnpj = Digits(taxId),
            email = email.Trim().ToLowerInvariant(),
            phone = billing?.Phone,
            mobilePhone = billing?.Phone,
            postalCode = billing?.PostalCode,
            address = billing?.Address,
            addressNumber = billing?.AddressNumber,
            province = billing?.Province,
            externalReference = customerId.ToString("N")
        }, cancellationToken);
        return (await ReadAsync<AsaasId>(response, cancellationToken)).Id;
    }

    public async Task UpdateCustomerBillingAsync(
        string customerId, AsaasBillingDetails billing, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Put,
            $"customers/{Uri.EscapeDataString(customerId)}",
            new
            {
                phone = billing.Phone,
                mobilePhone = billing.Phone,
                postalCode = billing.PostalCode,
                address = billing.Address,
                addressNumber = billing.AddressNumber,
                province = billing.Province
            },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AsaasSubscriptionResult> CreateSubscriptionAsync(
        Guid subscriptionId, string customerId, PlanDefinition plan, string billingType, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "subscriptions", new
        {
            customer = customerId,
            billingType,
            value = plan.Price,
            nextDueDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            cycle = plan.Cycle,
            description = $"ATLAS CAD - {plan.Name}",
            externalReference = subscriptionId.ToString("N"),
            callback = new { successUrl = options.PublicWebUrl + "/minha-conta", autoRedirect = true }
        }, cancellationToken);
        var subscription = await ReadAsync<AsaasId>(response, cancellationToken);
        return new AsaasSubscriptionResult(subscription.Id, await FindFirstPaymentAsync(subscription.Id, cancellationToken));
    }

    public async Task<AsaasCheckoutResult> CreateRecurringCheckoutAsync(
        Guid subscriptionId, string customerId, PlanDefinition plan, CancellationToken cancellationToken)
    {
        const int minutesToExpire = 1440;
        using var response = await SendAsync(HttpMethod.Post, "checkouts", new
        {
            billingTypes = new[] { "CREDIT_CARD" },
            chargeTypes = new[] { "RECURRENT" },
            minutesToExpire,
            externalReference = subscriptionId.ToString("N"),
            customer = customerId,
            callback = new
            {
                successUrl = options.PublicWebUrl + "/minha-conta?checkout=success",
                cancelUrl = options.PublicWebUrl + "/minha-conta?checkout=cancelled",
                expiredUrl = options.PublicWebUrl + "/minha-conta?checkout=expired"
            },
            items = new[]
            {
                new
                {
                    name = plan.Name,
                    description = $"Assinatura ATLAS CAD - {plan.Name}",
                    quantity = 1,
                    value = plan.Price
                }
            },
            subscription = new
            {
                cycle = plan.Cycle,
                nextDueDate = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            }
        }, cancellationToken);
        var checkout = await ReadAsync<AsaasId>(response, cancellationToken);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(minutesToExpire);
        return new AsaasCheckoutResult(
            checkout.Id,
            $"{options.AsaasCheckoutBaseUrl}?id={Uri.EscapeDataString(checkout.Id)}",
            expiresAt);
    }

    public async Task UpdateSubscriptionAsync(string subscriptionId, PlanDefinition plan, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}", new
        {
            value = plan.Price,
            cycle = plan.Cycle,
            description = $"ATLAS CAD - {plan.Name}",
            updatePendingPayments = true
        }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task CancelCheckoutAsync(string checkoutId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"checkouts/{Uri.EscapeDataString(checkoutId)}/cancel",
            new { },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<AsaasPayment?> FindFirstPaymentAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await SendAsync(HttpMethod.Get, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/payments?limit=1", null, cancellationToken);
            var list = await ReadAsync<AsaasList<AsaasPayment>>(response, cancellationToken);
            if (list.Data.FirstOrDefault() is { } payment) return payment;
            await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
        }
        return null;
    }

    public Task<AsaasPayment?> GetFirstPaymentAsync(string subscriptionId, CancellationToken cancellationToken) =>
        FindFirstPaymentAsync(subscriptionId, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, $"{options.AsaasBaseUrl}/{path}");
        request.Headers.TryAddWithoutValidation("access_token", options.AsaasApiKey);
        request.Headers.TryAddWithoutValidation("User-Agent", "AtlasCad/1.0");
        request.Headers.Accept.ParseAdd("application/json");
        if (body != null) request.Content = JsonContent.Create(body, options: _json);
        return await http.SendAsync(request, cancellationToken);
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(_json, cancellationToken) ??
            throw new LicenseServerException("asaas_response_invalid", "A resposta do Asaas está vazia.", 502);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new LicenseServerException("asaas_request_failed", $"O Asaas rejeitou a operação ({(int)response.StatusCode}): {detail}", 502);
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());

    private sealed class AsaasId { [JsonPropertyName("id")] public string Id { get; set; } = string.Empty; }
    private sealed class AsaasList<T> { [JsonPropertyName("data")] public List<T> Data { get; set; } = []; }
}

public sealed record AsaasSubscriptionResult(string SubscriptionId, AsaasPayment? FirstPayment);
public sealed record AsaasCheckoutResult(string CheckoutId, string Url, DateTimeOffset ExpiresAt);
public sealed record AsaasBillingDetails(
    string Phone,
    string PostalCode,
    string Address,
    string AddressNumber,
    string Province);
