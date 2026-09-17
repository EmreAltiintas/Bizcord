using UserService.Domain.Common;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Events;

public sealed record DisplayNameChanged(
    UserId UserId,
    DisplayName OldDisplayName,
    DisplayName NewDisplayName,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
