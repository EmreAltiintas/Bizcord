using UserService.Domain.Common;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Events;

public sealed record UserUnbanned(UserId UserId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
