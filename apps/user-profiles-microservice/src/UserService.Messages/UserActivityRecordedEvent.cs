namespace UserService.Messages;

/// <summary>Published by the User Service after an author's last active timestamp was updated.</summary>
public sealed class UserActivityRecordedEvent
{
    public Guid MessageId { get; init; }

    public Guid UserId { get; init; }

    public DateTime LastActiveAt { get; init; }

    public DateTime ProcessedAt { get; init; }
}
