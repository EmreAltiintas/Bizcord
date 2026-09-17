namespace UserService.Domain.ValueObjects;

public sealed record DisplayName
{
    public const int MinLength = 2;
    public const int MaxLength = 32;

    public string Value { get; }

    private DisplayName(string value) => Value = value;

    public static DisplayName Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var trimmed = value.Trim();

        if (trimmed.Length is < MinLength or > MaxLength)
        {
            throw new ArgumentException(
                $"Display name must be between {MinLength} and {MaxLength} characters.", nameof(value));
        }

        return new DisplayName(trimmed);
    }

    public override string ToString() => Value;
}
