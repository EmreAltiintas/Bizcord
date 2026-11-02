using System.Net;
using System.Text.Json;

namespace UserService.Api.Vault;

public sealed class VaultException : Exception
{
    public VaultException(string message) : base(message)
    {
    }
}

public sealed class VaultSecretClient
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly VaultOptions _options;

    public VaultSecretClient(HttpClient http, string token, VaultOptions options)
    {
        _http = http;
        _token = token;
        _options = options;
    }

    public async Task<string> GetSecretAsync(CancellationToken cancellationToken = default)
    {
        string? lastFailure = null;

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(_options.RetryDelay, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/{_options.SecretPath}");
                request.Headers.Add("X-Vault-Token", _token);

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return await ReadValueAsync(response, cancellationToken).ConfigureAwait(false);
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new VaultException(
                        $"Vault denied read access to '{_options.SecretPath}'. VAULT_TOKEN is invalid or expired, or its policy does not allow reading this path.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new VaultException($"No secret found in Vault at '{_options.SecretPath}'.");
                }

                if ((int)response.StatusCode < 500)
                {
                    throw new VaultException($"Vault returned unexpected status {(int)response.StatusCode}.");
                }

                lastFailure = $"Vault returned status {(int)response.StatusCode} (it may be sealed or starting)";
            }
            catch (HttpRequestException ex)
            {
                lastFailure = $"Vault is unreachable ({ex.GetType().Name})";
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastFailure = $"Vault did not answer within {_options.Timeout}";
            }
        }

        throw new VaultException($"{lastFailure} after {_options.MaxAttempts} attempts.");
    }

    private async Task<string> ReadValueAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("data", out var outer)
            && outer.TryGetProperty("data", out var inner)
            && inner.TryGetProperty(_options.Key, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new VaultException($"The Vault secret at '{_options.SecretPath}' has no value for '{_options.Key}'.");
    }
}
