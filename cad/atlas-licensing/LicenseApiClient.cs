using System;
using System.IO;
using System.Net.Http;
using System.Text;
using Atlas.Licensing;

namespace Atlas.AutoCAD.Adapter;

internal sealed class LicenseApiClient : IDisposable
{
    private readonly HttpClient _client;

    public LicenseApiClient()
    {
        var configured = Environment.GetEnvironmentVariable("ATLAS_LICENSE_API");
        _client = new HttpClient
        {
            BaseAddress = new Uri(string.IsNullOrWhiteSpace(configured) ? "http://127.0.0.1:5080/" : configured.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(12)
        };
    }

    public EntitlementResponse Activate(ActivationRequest request) => Post("api/v1/activations", request, typeof(ActivationRequest));
    public EntitlementResponse Renew(RenewalRequest request) => Post("api/v1/renewals", request, typeof(RenewalRequest));
    public CatalogKeyResponse GetCatalogKey(CatalogKeyRequest request) => Post<CatalogKeyResponse>("api/v1/catalog-keys", request, typeof(CatalogKeyRequest));

    private EntitlementResponse Post(string path, object request, Type requestType) => Post<EntitlementResponse>(path, request, requestType);

    private T Post<T>(string path, object request, Type requestType)
    {
        var json = Encoding.UTF8.GetString(LicenseCrypto.Serialize(request, requestType));
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = _client.PostAsync(path, content).GetAwaiter().GetResult();
        var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var error = (LicenseApiError)LicenseCrypto.Deserialize(bytes, typeof(LicenseApiError));
                throw new LicenseOperationException(error.ErrorCode, error.Message);
            }
            catch (LicenseOperationException) { throw; }
            catch (Exception) { throw new LicenseOperationException("license_server_error", "Licensing server rejected the request."); }
        }
        return (T)LicenseCrypto.Deserialize(bytes, typeof(T));
    }

    public void Dispose() => _client.Dispose();
}

internal sealed class LicenseOperationException : Exception
{
    public LicenseOperationException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}
