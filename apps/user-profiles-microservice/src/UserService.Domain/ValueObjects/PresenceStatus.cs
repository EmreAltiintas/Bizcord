namespace UserService.Domain.ValueObjects;

public sealed class PresenceStatus : IEquatable<PresenceStatus>
{
    public static readonly PresenceStatus Online = new(nameof(Online));
    public static readonly PresenceStatus Idle = new(nameof(Idle));
    public static readonly PresenceStatus DoNotDisturb = new(nameof(DoNotDisturb));
    public static readonly PresenceStatus Offline = new(nameof(Offline));

    private static readonly IReadOnlyDictionary<string, PresenceStatus> All =
        new[] { Online, Idle, DoNotDisturb, Offline }.ToDictionary(status => status.Name);

    public string Name { get; }

    private PresenceStatus(string name) => Name = name;

    public static PresenceStatus FromName(string name) =>
        All.TryGetValue(name, out var status)
            ? status
            : throw new ArgumentException($"'{name}' is not a known presence status.", nameof(name));

    public bool Equals(PresenceStatus? other) => other is not null && Name == other.Name;

    public override bool Equals(object? obj) => Equals(obj as PresenceStatus);

    public override int GetHashCode() => Name.GetHashCode();

    public override string ToString() => Name;
}
