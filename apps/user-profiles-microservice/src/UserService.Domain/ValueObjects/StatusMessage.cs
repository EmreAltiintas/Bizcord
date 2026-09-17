namespace UserService.Domain.ValueObjects;

public sealed record StatusMessage
{
    public const int MaxLength = 128;

    public static StatusMessage Empty { get; } = new(string.Empty);

    public string Value { get; }

    public bool IsEmpty => Value.Length == 0;

    private StatusMessage(string value) => Value = value;

    public static StatusMessage Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Status message cannot exceed {MaxLength} characters.", nameof(value));
        }

        return trimmed.Length == 0 ? Empty : new StatusMessage(trimmed);
    }

    public override string ToString() => Value;
}
