using System.Globalization;

namespace Atlas.Licensing.Api;

public sealed class CommerceOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public string AsaasBaseUrl { get; init; } = string.Empty;
    public string AsaasCheckoutBaseUrl { get; init; } = string.Empty;
    public string AsaasApiKey { get; init; } = string.Empty;
    public string AsaasWebhookToken { get; init; } = string.Empty;
    public string PublicWebUrl { get; init; } = string.Empty;
    public string InstallerPath { get; init; } = string.Empty;
    public string UninstallerPath { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, PlanDefinition> Plans { get; init; } = new Dictionary<string, PlanDefinition>();

    public static CommerceOptions FromEnvironment(string contentRoot)
    {
        static string Required(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{name} is required.");
            return value.Trim();
        }

        var monthly = ParseMoney(Environment.GetEnvironmentVariable("ATLAS_PLAN_MONTHLY_PRICE") ?? "79.90", "ATLAS_PLAN_MONTHLY_PRICE");
        var yearly = ParseMoney(Environment.GetEnvironmentVariable("ATLAS_PLAN_YEARLY_PRICE") ?? "799.00", "ATLAS_PLAN_YEARLY_PRICE");
        var asaasBaseUrl = (Environment.GetEnvironmentVariable("ASAAS_BASE_URL") ?? "https://api-sandbox.asaas.com/v3").TrimEnd('/');
        var defaultCheckoutBaseUrl = asaasBaseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
            ? "https://sandbox.asaas.com/checkoutSession/show"
            : "https://asaas.com/checkoutSession/show";
        var asaasCheckoutBaseUrl = (Environment.GetEnvironmentVariable("ASAAS_CHECKOUT_BASE_URL") ?? defaultCheckoutBaseUrl).TrimEnd('/');
        var publicWebUrl = Required("ATLAS_PUBLIC_WEB_URL").TrimEnd('/');
        var webhookToken = Required("ASAAS_WEBHOOK_TOKEN");
        if (!Uri.TryCreate(asaasBaseUrl, UriKind.Absolute, out var asaasUri) || asaasUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("ASAAS_BASE_URL must use HTTPS.");
        if (!Uri.TryCreate(asaasCheckoutBaseUrl, UriKind.Absolute, out var asaasCheckoutUri) || asaasCheckoutUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("ASAAS_CHECKOUT_BASE_URL must use HTTPS.");
        if (!Uri.TryCreate(publicWebUrl, UriKind.Absolute, out _)) throw new InvalidOperationException("ATLAS_PUBLIC_WEB_URL must be absolute.");
        if (webhookToken.Length < 32) throw new InvalidOperationException("ASAAS_WEBHOOK_TOKEN must contain at least 32 characters.");
        return new CommerceOptions
        {
            ConnectionString = Required("ConnectionStrings__Atlas"),
            AsaasBaseUrl = asaasBaseUrl,
            AsaasCheckoutBaseUrl = asaasCheckoutBaseUrl,
            AsaasApiKey = Required("ASAAS_API_KEY"),
            AsaasWebhookToken = webhookToken,
            PublicWebUrl = publicWebUrl,
            InstallerPath = Path.GetFullPath(Environment.GetEnvironmentVariable("ATLAS_INSTALLER_PATH") ??
                Path.Combine(contentRoot, "App_Data", "Instalador-AtlasCad.exe")),
            UninstallerPath = Path.GetFullPath(Environment.GetEnvironmentVariable("ATLAS_UNINSTALLER_PATH") ??
                Path.Combine(contentRoot, "App_Data", "Desinstalar-AtlasCad.exe")),
            Plans = new Dictionary<string, PlanDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["monthly"] = new("monthly", "SteelFlow Mensal", monthly, "MONTHLY"),
                ["yearly"] = new("yearly", "SteelFlow Anual", yearly, "YEARLY")
            }
        };
    }

    private static decimal ParseMoney(string value, string name) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) && result > 0
            ? result
            : throw new InvalidOperationException($"{name} must be a positive decimal using a dot separator.");
}

public sealed record PlanDefinition(string Code, string Name, decimal Price, string Cycle);
