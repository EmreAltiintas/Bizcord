using UserService.Application.Mapping;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;

namespace UserService.Application.Tests.Mapping;

public class UserProfileMappingExtensionsTests
{
    [Fact]
    public void ToContract_maps_the_public_fields()
    {
        var id = UserId.New();
        var profile = UserProfile.Create(id, DisplayName.Create("Emre"), createdAtUtc: DateTimeOffset.UtcNow);
        profile.ChangeAvatar(AvatarReference.Create("https://cdn.bizcord.dev/avatars/emre.png"));
        profile.ChangePresenceStatus(PresenceStatus.Online);

        var contract = profile.ToContract();

        Assert.Equal(id.Value, contract.Id);
        Assert.Equal("Emre", contract.DisplayName);
        Assert.Equal("https://cdn.bizcord.dev/avatars/emre.png", contract.AvatarUrl);
        Assert.Equal("Online", contract.PresenceStatus);
        Assert.Equal(profile.CreatedAtUtc, contract.CreatedAt);
    }

    [Fact]
    public void ToContract_maps_a_missing_avatar_to_null()
    {
        var profile = UserProfile.Create(UserId.New(), DisplayName.Create("Emre"));

        var contract = profile.ToContract();

        Assert.Null(contract.AvatarUrl);
    }
}
