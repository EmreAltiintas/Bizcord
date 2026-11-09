using UserService.Domain.Common;
using UserService.Domain.Events;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Entities;

public sealed class UserProfile : Entity<UserId>
{
    public DisplayName DisplayName { get; private set; }

    public AvatarReference? Avatar { get; private set; }

    public StatusMessage StatusMessage { get; private set; }

    public PresenceStatus PresenceStatus { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? LastActiveAtUtc { get; private set; }

    public bool IsBanned { get; private set; }

    public string? BanReason { get; private set; }

    public DateTimeOffset? BannedAtUtc { get; private set; }

    private UserProfile(UserId id, DisplayName displayName, DateTimeOffset createdAtUtc)
        : base(id)
    {
        DisplayName = displayName;
        StatusMessage = StatusMessage.Empty;
        PresenceStatus = PresenceStatus.Offline;
        CreatedAtUtc = createdAtUtc;
    }

    public static UserProfile Create(UserId id, DisplayName displayName, DateTimeOffset? createdAtUtc = null)
    {
        var timestamp = createdAtUtc ?? DateTimeOffset.UtcNow;
        var profile = new UserProfile(id, displayName, timestamp);

        profile.Raise(new UserProfileCreated(id, displayName, timestamp));

        return profile;
    }

    public void ChangeDisplayName(DisplayName newDisplayName)
    {
        if (DisplayName == newDisplayName)
        {
            return;
        }

        var oldDisplayName = DisplayName;
        DisplayName = newDisplayName;

        Raise(new DisplayNameChanged(Id, oldDisplayName, newDisplayName, DateTimeOffset.UtcNow));
    }

    public void ChangePresenceStatus(PresenceStatus newStatus)
    {
        if (PresenceStatus.Equals(newStatus))
        {
            return;
        }

        var oldStatus = PresenceStatus;
        PresenceStatus = newStatus;

        Raise(new PresenceStatusChanged(Id, oldStatus, newStatus, DateTimeOffset.UtcNow));
    }

    public void RecordActivity(DateTimeOffset activeAtUtc)
    {
        if (LastActiveAtUtc is null || activeAtUtc > LastActiveAtUtc)
        {
            LastActiveAtUtc = activeAtUtc;
        }
    }

    /// <summary>Marks the profile as banned. Does nothing if the profile is already banned.</summary>
    public void Ban(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsBanned)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        IsBanned = true;
        BanReason = reason;
        BannedAtUtc = now;

        Raise(new UserBanned(Id, reason, now));
    }

    /// <summary>Removes the ban. Does nothing if the profile is not banned.</summary>
    public void Unban()
    {
        if (!IsBanned)
        {
            return;
        }

        IsBanned = false;
        BanReason = null;
        BannedAtUtc = null;

        Raise(new UserUnbanned(Id, DateTimeOffset.UtcNow));
    }

    public void ChangeAvatar(AvatarReference? avatar) => Avatar = avatar;

    public void ChangeStatusMessage(StatusMessage statusMessage) => StatusMessage = statusMessage;
}

