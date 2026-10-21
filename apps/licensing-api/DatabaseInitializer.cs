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
        CREATE TABLE IF NOT EXISTS atlas_libraries (
            id uuid PRIMARY KEY,
            slug text NOT NULL UNIQUE,
            name text NOT NULL,
            product_id text NOT NULL UNIQUE,
            module_code text NOT NULL UNIQUE,
            catalog_id text,
            summary text NOT NULL,
            description text NOT NULL,
            features jsonb NOT NULL DEFAULT '[]'::jsonb,
            requirements jsonb NOT NULL DEFAULT '[]'::jsonb,
            image_path text,
            status text NOT NULL DEFAULT 'draft',
            active_release_id uuid,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            published_at timestamptz,
            CONSTRAINT ck_atlas_library_status CHECK (status IN ('draft','published','archived'))
        );
        INSERT INTO atlas_libraries(id,slug,name,product_id,module_code,catalog_id,summary,description,features,requirements,status,published_at)
        VALUES (
            'a7100000-0000-4000-8000-000000000001','steelflow','SteelFlow','atlas-steelflow','steelflow','steelflow',
            'Automacao de perfis metalicos no AutoCAD.',
            'Automatize a representacao de perfis metalicos no AutoCAD com um fluxo pensado para quem projeta.',
            '["Conversao de linhas e polilinhas em MLINE","Associacao de identificacao de perfis no desenho","Estilos para representacao em planta e corte","Padronizacao do fluxo de detalhamento"]'::jsonb,
            '["Windows 10 ou 11 de 64 bits","AutoCAD 2022 a 2026","Internet para ativacao e renovacao periodica"]'::jsonb,
            'published',now())
        ON CONFLICT (slug) DO UPDATE SET
            name='SteelFlow', product_id='atlas-steelflow', module_code='steelflow', catalog_id='steelflow';
        CREATE TABLE IF NOT EXISTS atlas_library_plans (
            id uuid PRIMARY KEY,
            library_id uuid NOT NULL REFERENCES atlas_libraries(id) ON DELETE CASCADE,
            code text NOT NULL,
            name text NOT NULL,
            price numeric(12,2) NOT NULL,
            cycle text NOT NULL,
            active boolean NOT NULL DEFAULT true,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            UNIQUE(library_id,code),
            CONSTRAINT ck_atlas_plan_cycle CHECK (cycle IN ('MONTHLY','YEARLY')),
            CONSTRAINT ck_atlas_plan_price CHECK (price > 0)
        );
        INSERT INTO atlas_library_plans(id,library_id,code,name,price,cycle)
        VALUES
            ('a7200000-0000-4000-8000-000000000001','a7100000-0000-4000-8000-000000000001','monthly','SteelFlow Mensal',79.90,'MONTHLY'),
            ('a7200000-0000-4000-8000-000000000002','a7100000-0000-4000-8000-000000000001','yearly','SteelFlow Anual',799.00,'YEARLY')
        ON CONFLICT (library_id,code) DO UPDATE SET name=EXCLUDED.name;
        CREATE TABLE IF NOT EXISTS atlas_library_releases (
            id uuid PRIMARY KEY,
            library_id uuid NOT NULL REFERENCES atlas_libraries(id) ON DELETE CASCADE,
            version text NOT NULL,
            artifact_path text NOT NULL,
            sha256 text NOT NULL,
            size_bytes bigint NOT NULL,
            signature_present boolean NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now(),
            published_at timestamptz,
            UNIQUE(library_id,version)
        );
        ALTER TABLE atlas_libraries DROP CONSTRAINT IF EXISTS fk_atlas_library_active_release;
        ALTER TABLE atlas_libraries ADD CONSTRAINT fk_atlas_library_active_release
            FOREIGN KEY (active_release_id) REFERENCES atlas_library_releases(id) ON DELETE SET NULL;
        CREATE TABLE IF NOT EXISTS atlas_library_docs (
            id uuid PRIMARY KEY,
            library_id uuid NOT NULL REFERENCES atlas_libraries(id) ON DELETE CASCADE,
            slug text NOT NULL,
            title text NOT NULL,
            summary text NOT NULL,
            group_name text NOT NULL,
            position integer NOT NULL DEFAULT 0,
            markdown text NOT NULL,
            status text NOT NULL DEFAULT 'draft',
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            UNIQUE(library_id,slug),
            CONSTRAINT ck_atlas_doc_status CHECK (status IN ('draft','published'))
        );
        INSERT INTO atlas_library_docs(id,library_id,slug,title,summary,group_name,position,markdown,status)
        VALUES
          ('a7300000-0000-4000-8000-000000000001','a7100000-0000-4000-8000-000000000001','visao-geral','Visao geral','O que e o SteelFlow e como funciona o fluxo completo.','Comece aqui',10,'## O que e\n\nO SteelFlow e a biblioteca do ATLAS CAD para automatizar representacoes de perfis metalicos no AutoCAD.\n\n## Fluxo completo\n\nAssine pelo site, aguarde a confirmacao do pagamento e execute o instalador da biblioteca.','published'),
          ('a7300000-0000-4000-8000-000000000002','a7100000-0000-4000-8000-000000000001','compra-e-conta','Compra e area do cliente','Assinatura, pagamento, download e gerenciamento do plano.','Primeiros passos',20,'## Criar a assinatura\n\nEscolha o plano mensal ou anual e conclua o pagamento seguro pelo Asaas.\n\n## Liberacao\n\nO download aparece na Area do Cliente depois da confirmacao do pagamento.','published'),
          ('a7300000-0000-4000-8000-000000000003','a7100000-0000-4000-8000-000000000001','instalacao','Instalacao e ativacao','Do instalador ao primeiro uso no AutoCAD.','Primeiros passos',30,'## Instalar\n\nFeche o AutoCAD, baixe o instalador da SteelFlow e execute-o.\n\n## Ativar\n\nSiga o vinculo seguro exibido pelo instalador para associar a maquina a sua conta.','published'),
          ('a7300000-0000-4000-8000-000000000004','a7100000-0000-4000-8000-000000000001','licenciamento','Licenciamento','Vinculo por dispositivo, renovacao e funcionamento offline.','Referencia',40,'## Vinculo seguro\n\nA licenca e vinculada a uma chave criptografica criada para o usuario atual do Windows.\n\n## Uso offline\n\nDepois de uma validacao online, a biblioteca funciona por ate 72 horas sem conexao.','published'),
          ('a7300000-0000-4000-8000-000000000005','a7100000-0000-4000-8000-000000000001','primeiro-uso','Primeiro uso','Valide a instalacao e carregue o catalogo protegido.','Uso',50,'## Diagnostico\n\nExecute `ATLAS_DIAGNOSTICS` e confirme adaptador, licenca e catalogo.\n\n## Catalogo\n\nExecute `ATLAS_INSTALL_CATALOG` depois da ativacao.','published'),
          ('a7300000-0000-4000-8000-000000000006','a7100000-0000-4000-8000-000000000001','solucao-de-problemas','Solucao de problemas','Respostas para os erros mais comuns.','Suporte',60,'## Comando nao aparece\n\nFeche o AutoCAD e execute novamente o instalador.\n\n## Licenca expirada\n\nConecte o computador a internet e execute `ATLAS_LICENSE_RENEW`.','published')
        ON CONFLICT (library_id,slug) DO NOTHING;
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
        ALTER TABLE atlas_subscriptions ADD COLUMN IF NOT EXISTS library_id uuid REFERENCES atlas_libraries(id);
        UPDATE atlas_subscriptions SET library_id='a7100000-0000-4000-8000-000000000001' WHERE library_id IS NULL;
        ALTER TABLE atlas_subscriptions ALTER COLUMN library_id SET NOT NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_atlas_subscription_asaas_checkout
            ON atlas_subscriptions(asaas_checkout_id) WHERE asaas_checkout_id IS NOT NULL;
        DROP INDEX IF EXISTS ux_atlas_subscription_current;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_atlas_subscription_current_library
            ON atlas_subscriptions(customer_id,library_id) WHERE status NOT IN ('cancelled', 'replaced');
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
        ALTER TABLE atlas_installer_pairings ADD COLUMN IF NOT EXISTS library_id uuid REFERENCES atlas_libraries(id);
        UPDATE atlas_installer_pairings SET library_id='a7100000-0000-4000-8000-000000000001' WHERE library_id IS NULL;
        ALTER TABLE atlas_installer_pairings ALTER COLUMN library_id SET NOT NULL;
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
