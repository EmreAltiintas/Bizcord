namespace UserService.Domain.Common;

public abstract class Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected Entity(TId id) => Id = id;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
