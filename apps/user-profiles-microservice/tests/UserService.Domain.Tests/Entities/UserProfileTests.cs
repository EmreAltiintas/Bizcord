using UserService.Domain.Entities;
using UserService.Domain.Events;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Tests.Entities;

public class UserProfileTests
{
    private static readonly DisplayName Emre = DisplayName.Create("Emre");

    [Fact]
    public void Create_sets_the_initial_state()
    {
        var id = UserId.New();

        var profile = UserProfile.Create(id, Emre);

        Assert.Equal(id, profile.Id);
        Assert.Equal(Emre, profile.DisplayName);
        Assert.Equal(PresenceStatus.Offline, profile.PresenceStatus);
        Assert.Equal(StatusMessage.Empty, profile.StatusMessage);
        Assert.Null(profile.Avatar);
    }

    [Fact]
    public void Create_raises_a_UserProfileCreated_event()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);

        var raised = Assert.Single(profile.DomainEvents);
        var created = Assert.IsType<UserProfileCreated>(raised);
        Assert.Equal(profile.Id, created.UserId);
        Assert.Equal(Emre, created.DisplayName);
    }

    [Fact]
    public void ChangeDisplayName_updates_the_value_and_raises_an_event()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.ClearDomainEvents();
        var newName = DisplayName.Create("Emre A");

        profile.ChangeDisplayName(newName);

        Assert.Equal(newName, profile.DisplayName);
        var raised = Assert.Single(profile.DomainEvents);
        var changed = Assert.IsType<DisplayNameChanged>(raised);
        Assert.Equal(Emre, changed.OldDisplayName);
        Assert.Equal(newName, changed.NewDisplayName);
    }

    [Fact]
    public void ChangeDisplayName_is_a_no_op_when_the_name_is_unchanged()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.ClearDomainEvents();

        profile.ChangeDisplayName(DisplayName.Create("Emre"));

        Assert.Empty(profile.DomainEvents);
    }

    [Fact]
    public void ChangePresenceStatus_updates_the_value_and_raises_an_event()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.ClearDomainEvents();

        profile.ChangePresenceStatus(PresenceStatus.Online);

        Assert.Equal(PresenceStatus.Online, profile.PresenceStatus);
        var raised = Assert.Single(profile.DomainEvents);
        var changed = Assert.IsType<PresenceStatusChanged>(raised);
        Assert.Equal(PresenceStatus.Offline, changed.OldStatus);
        Assert.Equal(PresenceStatus.Online, changed.NewStatus);
    }

    [Fact]
    public void ChangePresenceStatus_is_a_no_op_when_the_status_is_unchanged()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.ClearDomainEvents();

        profile.ChangePresenceStatus(PresenceStatus.Offline);

        Assert.Empty(profile.DomainEvents);
    }

    [Fact]
    public void ChangeAvatar_updates_the_avatar_without_raising_an_event()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.ClearDomainEvents();
        var avatar = AvatarReference.Create("https://cdn.bizcord.dev/avatars/emre.png");

        profile.ChangeAvatar(avatar);

        Assert.Equal(avatar, profile.Avatar);
        Assert.Empty(profile.DomainEvents);
    }

    [Fact]
    public void Ban_marks_the_profile_and_raises_a_UserBanned_event()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.ClearDomainEvents();

        profile.Ban("spam");

        Assert.True(profile.IsBanned);
        Assert.Equal("spam", profile.BanReason);
        Assert.NotNull(profile.BannedAtUtc);
        var banned = Assert.IsType<UserBanned>(Assert.Single(profile.DomainEvents));
        Assert.Equal(profile.Id, banned.UserId);
        Assert.Equal("spam", banned.Reason);
    }

    [Fact]
    public void Ban_is_idempotent_and_keeps_the_first_reason_and_timestamp()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.Ban("spam");
        var bannedAt = profile.BannedAtUtc;
        profile.ClearDomainEvents();

        profile.Ban("harassment");

        Assert.True(profile.IsBanned);
        Assert.Equal("spam", profile.BanReason);
        Assert.Equal(bannedAt, profile.BannedAtUtc);
        Assert.Empty(profile.DomainEvents);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ban_requires_a_reason(string? reason)
    {
        var profile = UserProfile.Create(UserId.New(), Emre);

        Assert.ThrowsAny<ArgumentException>(() => profile.Ban(reason!));
        Assert.False(profile.IsBanned);
    }

    [Fact]
    public void Unban_clears_the_ban_and_raises_a_UserUnbanned_event()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.Ban("spam");
        profile.ClearDomainEvents();

        profile.Unban();

        Assert.False(profile.IsBanned);
        Assert.Null(profile.BanReason);
        Assert.Null(profile.BannedAtUtc);
        var unbanned = Assert.IsType<UserUnbanned>(Assert.Single(profile.DomainEvents));
        Assert.Equal(profile.Id, unbanned.UserId);
    }

    [Fact]
    public void Unban_is_idempotent()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);
        profile.ClearDomainEvents();

        profile.Unban();

        Assert.False(profile.IsBanned);
        Assert.Empty(profile.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_collection()
    {
        var profile = UserProfile.Create(UserId.New(), Emre);

        profile.ClearDomainEvents();

        Assert.Empty(profile.DomainEvents);
    }
}
