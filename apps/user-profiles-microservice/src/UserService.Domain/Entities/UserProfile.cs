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

    public void ChangeAvatar(AvatarReference? avatar) => Avatar = avatar;

    public void ChangeStatusMessage(StatusMessage statusMessage) => StatusMessage = statusMessage;
}
