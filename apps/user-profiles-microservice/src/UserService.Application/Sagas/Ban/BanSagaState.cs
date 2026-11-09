namespace UserService.Application.Sagas.Ban;

/// <summary>
/// Progress of one ban saga. The orchestrator is the only writer.
/// </summary>
/// <remarks>
/// A step is "decided" when it either succeeded or was given up. The saga is completed when
/// both the channel step and the streaming step are decided. <see cref="NeedsOperator"/> is
/// set when a step was given up, because the ban stands but the side effect did not happen.
/// </remarks>
public sealed class BanSagaState
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }

    public Guid ModeratorId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public bool UserBanned { get; set; }

    public bool ChannelsRemoved { get; set; }

    public int ChannelAttempts { get; set; }

    public bool ChannelsGaveUp { get; set; }

    public bool StreamsTerminated { get; set; }

    public int StreamAttempts { get; set; }

    public bool StreamsGaveUp { get; set; }

    /// <summary>Set when a moderator unban asked for the channel memberships to be restored and the Channel Service confirmed.</summary>
    public bool MembershipRestored { get; set; }

    /// <summary>Set when the Channel Service reported that the memberships could not be restored. It is not retried.</summary>
    public bool MembershipRestoreFailed { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsFailed { get; set; }

    public bool NeedsOperator { get; set; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>True while the saga has neither completed nor failed.</summary>
    public bool IsInProgress => !IsCompleted && !IsFailed;
}
