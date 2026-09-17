using UserService.Domain.Common;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Events;

public sealed record UserProfileCreated(
    UserId UserId,
    DisplayName DisplayName,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
