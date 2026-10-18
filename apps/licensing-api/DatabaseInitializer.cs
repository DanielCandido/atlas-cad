using Npgsql;

namespace Atlas.Licensing.Api;

public sealed class DatabaseInitializer(NpgsqlDataSource dataSource) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(Schema);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS atlas_customers (
            id uuid PRIMARY KEY,
            name text NOT NULL,
            email text NOT NULL UNIQUE,
            tax_id_hash text NOT NULL,
            password_hash text NOT NULL,
            asaas_customer_id text UNIQUE,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS atlas_sessions (
            token_hash text PRIMARY KEY,
            customer_id uuid NOT NULL REFERENCES atlas_customers(id) ON DELETE CASCADE,
            expires_at timestamptz NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS ix_atlas_sessions_customer ON atlas_sessions(customer_id);
        CREATE TABLE IF NOT EXISTS atlas_subscriptions (
            id uuid PRIMARY KEY,
            customer_id uuid NOT NULL REFERENCES atlas_customers(id),
            plan_code text NOT NULL,
            status text NOT NULL,
            billing_type text NOT NULL,
            asaas_checkout_id text,
            checkout_status text,
            checkout_expires_at timestamptz,
            asaas_subscription_id text UNIQUE,
            asaas_payment_id text,
            payment_url text,
            paid_through timestamptz,
            cancel_at_period_end boolean NOT NULL DEFAULT false,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now()
        );
        ALTER TABLE atlas_subscriptions ADD COLUMN IF NOT EXISTS asaas_checkout_id text;
        ALTER TABLE atlas_subscriptions ADD COLUMN IF NOT EXISTS checkout_status text;
        ALTER TABLE atlas_subscriptions ADD COLUMN IF NOT EXISTS checkout_expires_at timestamptz;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_atlas_subscription_asaas_checkout
            ON atlas_subscriptions(asaas_checkout_id) WHERE asaas_checkout_id IS NOT NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_atlas_subscription_current
            ON atlas_subscriptions(customer_id) WHERE status NOT IN ('cancelled', 'replaced');
        CREATE TABLE IF NOT EXISTS atlas_licenses (
            id text PRIMARY KEY,
            subscription_id uuid UNIQUE REFERENCES atlas_subscriptions(id),
            product_id text NOT NULL,
            customer_reference text NOT NULL,
            generation integer NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        ALTER TABLE atlas_licenses ADD COLUMN IF NOT EXISTS revoked_at timestamptz;
        ALTER TABLE atlas_licenses ADD COLUMN IF NOT EXISTS manual_valid_until timestamptz;
        ALTER TABLE atlas_licenses ADD COLUMN IF NOT EXISTS manual_grant_reason text;
        CREATE TABLE IF NOT EXISTS atlas_activation_codes (
            code_hash text PRIMARY KEY,
            license_id text NOT NULL REFERENCES atlas_licenses(id),
            generation integer NOT NULL,
            device_fingerprint text NOT NULL,
            expires_at timestamptz NOT NULL,
            used_at timestamptz
        );
        CREATE TABLE IF NOT EXISTS atlas_activations (
            id text PRIMARY KEY,
            license_id text NOT NULL REFERENCES atlas_licenses(id),
            generation integer NOT NULL,
            device_public_key_x text NOT NULL,
            device_public_key_y text NOT NULL,
            device_fingerprint text NOT NULL,
            activated_at timestamptz NOT NULL,
            last_renewed_at timestamptz NOT NULL,
            revoked_at timestamptz
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_atlas_active_device
            ON atlas_activations(license_id, generation) WHERE revoked_at IS NULL;
        CREATE TABLE IF NOT EXISTS atlas_activation_nonces (
            activation_id text NOT NULL REFERENCES atlas_activations(id) ON DELETE CASCADE,
            nonce text NOT NULL,
            kind text NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (activation_id, nonce, kind)
        );
        CREATE TABLE IF NOT EXISTS atlas_installer_pairings (
            id text PRIMARY KEY,
            user_code_hash text NOT NULL UNIQUE,
            device_public_key_x text NOT NULL,
            device_public_key_y text NOT NULL,
            device_fingerprint text NOT NULL,
            detected_versions jsonb NOT NULL,
            status text NOT NULL,
            customer_id uuid REFERENCES atlas_customers(id) ON DELETE SET NULL,
            license_id text REFERENCES atlas_licenses(id) ON DELETE SET NULL,
            replace_existing boolean NOT NULL DEFAULT false,
            activation_id text REFERENCES atlas_activations(id) ON DELETE SET NULL,
            expires_at timestamptz NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now(),
            approved_at timestamptz,
            completed_at timestamptz
        );
        CREATE INDEX IF NOT EXISTS ix_atlas_installer_pairings_expiry ON atlas_installer_pairings(expires_at);
        CREATE TABLE IF NOT EXISTS atlas_webhook_events (
            id text PRIMARY KEY,
            event_type text NOT NULL,
            payload jsonb NOT NULL,
            received_at timestamptz NOT NULL DEFAULT now(),
            processed_at timestamptz
        );
        CREATE TABLE IF NOT EXISTS atlas_paid_payments (
            payment_id text PRIMARY KEY,
            subscription_id uuid NOT NULL REFERENCES atlas_subscriptions(id),
            paid_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS atlas_admin_users (
            id uuid PRIMARY KEY,
            name text NOT NULL,
            email text NOT NULL UNIQUE,
            password_hash text NOT NULL,
            active boolean NOT NULL DEFAULT true,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS atlas_admin_sessions (
            token_hash text PRIMARY KEY,
            admin_user_id uuid NOT NULL REFERENCES atlas_admin_users(id) ON DELETE CASCADE,
            expires_at timestamptz NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS ix_atlas_admin_sessions_user ON atlas_admin_sessions(admin_user_id);
        CREATE INDEX IF NOT EXISTS ix_atlas_admin_sessions_expiry ON atlas_admin_sessions(expires_at);
        CREATE TABLE IF NOT EXISTS atlas_admin_audit_logs (
            id uuid PRIMARY KEY,
            admin_user_id uuid REFERENCES atlas_admin_users(id) ON DELETE SET NULL,
            admin_email text NOT NULL,
            action text NOT NULL,
            target_type text NOT NULL,
            target_id text NOT NULL,
            reason text NOT NULL,
            before_state jsonb,
            after_state jsonb,
            outcome text NOT NULL,
            ip_address text,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS ix_atlas_admin_audit_target
            ON atlas_admin_audit_logs(target_type,target_id,created_at DESC);
        """;
}
