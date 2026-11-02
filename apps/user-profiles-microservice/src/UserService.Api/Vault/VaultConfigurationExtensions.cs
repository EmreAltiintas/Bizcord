using Microsoft.Extensions.Logging;

namespace UserService.Api.Vault;

public static class VaultConfigurationExtensions
{
    public static ConfigurationManager AddVaultSecrets(this ConfigurationManager configuration)
    {
        var options = configuration.GetSection(VaultOptions.SectionName).Get<VaultOptions>() ?? new VaultOptions();
        if (!options.Enabled)
        {
            return configuration;
        }

        var address = configuration["VAULT_ADDR"]
            ?? throw new InvalidOperationException("VAULT_ADDR is required to read secrets from Vault.");
        var token = configuration["VAULT_TOKEN"]
            ?? throw new InvalidOperationException("VAULT_TOKEN is required to read secrets from Vault.");

        using var http = new HttpClient
        {
            BaseAddress = new Uri(address.TrimEnd('/') + "/"),
            Timeout = options.Timeout
        };

        var value = new VaultSecretClient(http, token, options).GetSecretAsync().GetAwaiter().GetResult();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = value });

        using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        loggerFactory.CreateLogger("Vault").LogInformation(
            "Loaded Jwt:SigningKey from Vault path {SecretPath}", options.SecretPath);

        return configuration;
    }
}
