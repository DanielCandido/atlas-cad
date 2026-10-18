using System.Net.Http;
using System.Text;
using Atlas.Licensing;

namespace Atlas.Installer;

internal sealed class InstallerApiClient : IDisposable
{
    private readonly HttpClient client;

    public InstallerApiClient(string baseUrl) => client = new HttpClient
    {
        BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromSeconds(20)
    };

    public Task<InstallerPairingCreateResponse> CreatePairingAsync(InstallerPairingCreateRequest request, CancellationToken cancellationToken) =>
        PostAsync<InstallerPairingCreateResponse>("api/v1/installer/pairings", request, typeof(InstallerPairingCreateRequest), cancellationToken);

    public Task<InstallerPairingStatusResponse> PollAsync(string id, InstallerPairingProofRequest request, CancellationToken cancellationToken) =>
        PostAsync<InstallerPairingStatusResponse>($"api/v1/installer/pairings/{Uri.EscapeDataString(id)}/poll", request, typeof(InstallerPairingProofRequest), cancellationToken);

    public Task<EntitlementResponse> CompleteAsync(string id, InstallerPairingProofRequest request, CancellationToken cancellationToken) =>
        PostAsync<EntitlementResponse>($"api/v1/installer/pairings/{Uri.EscapeDataString(id)}/complete", request, typeof(InstallerPairingProofRequest), cancellationToken);

    public Task<CatalogKeyResponse> GetCatalogKeyAsync(CatalogKeyRequest request, CancellationToken cancellationToken) =>
        PostAsync<CatalogKeyResponse>("api/v1/catalog-keys", request, typeof(CatalogKeyRequest), cancellationToken);

    private async Task<T> PostAsync<T>(string path, object request, Type requestType, CancellationToken cancellationToken)
    {
        var json = Encoding.UTF8.GetString(LicenseCrypto.Serialize(request, requestType));
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var error = (LicenseApiError)LicenseCrypto.Deserialize(bytes, typeof(LicenseApiError));
                throw new InstallerException(error.ErrorCode, error.Message);
            }
            catch (InstallerException) { throw; }
            catch { throw new InstallerException("license_server_error", "O servidor recusou a solicitação."); }
        }
        return (T)LicenseCrypto.Deserialize(bytes, typeof(T));
    }

    public void Dispose() => client.Dispose();
}

internal sealed class InstallerException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
