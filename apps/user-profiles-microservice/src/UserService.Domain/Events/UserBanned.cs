using UserService.Domain.Common;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Events;

public sealed record UserBanned(UserId UserId, string Reason, DateTimeOffset OccurredOnUtc) : IDomainEvent;
