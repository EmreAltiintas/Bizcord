using UserService.Domain.Common;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Events;

public sealed record PresenceStatusChanged(
    UserId UserId,
    PresenceStatus OldStatus,
    PresenceStatus NewStatus,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
