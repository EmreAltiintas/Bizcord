namespace UserService.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}
