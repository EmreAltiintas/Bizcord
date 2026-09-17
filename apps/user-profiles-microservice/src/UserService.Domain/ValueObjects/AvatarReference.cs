namespace UserService.Domain.ValueObjects;

public sealed record AvatarReference
{
    public Uri Value { get; }

    private AvatarReference(Uri value) => Value = value;

    public static AvatarReference Create(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Avatar reference must be an absolute URL.", nameof(url));
        }

        return new AvatarReference(uri);
    }

    public override string ToString() => Value.ToString();
}
