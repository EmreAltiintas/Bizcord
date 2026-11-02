namespace UserService.Api.Vault;

public sealed class VaultOptions
{
    public const string SectionName = "Vault";

    public bool Enabled { get; set; } = true;

    public string SecretPath { get; set; } = "secret/data/user-profiles";

    public string Key { get; set; } = "jwt-signing-key";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    public int MaxAttempts { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
