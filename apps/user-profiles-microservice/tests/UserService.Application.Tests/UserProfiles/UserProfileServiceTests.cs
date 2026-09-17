using UserService.Application.Common;
using UserService.Application.UserProfiles;

namespace UserService.Application.Tests.UserProfiles;

public class UserProfileServiceTests
{
    private static UserProfileService CreateService(out InMemoryUserProfileRepository repository)
    {
        repository = new InMemoryUserProfileRepository();
        return new UserProfileService(repository);
    }

    [Fact]
    public void Create_persists_and_returns_the_new_profile()
    {
        var service = CreateService(out var repository);

        var result = service.Create(new CreateUserProfileCommand("Emre"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Emre", result.Value!.DisplayName);
        Assert.NotNull(repository.GetById(new Domain.ValueObjects.UserId(result.Value.Id)));
    }

    [Fact]
    public void Create_returns_a_validation_failure_for_an_invalid_display_name()
    {
        var service = CreateService(out _);

        var result = service.Create(new CreateUserProfileCommand("a"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public void GetById_returns_null_when_not_found()
    {
        var service = CreateService(out _);

        Assert.Null(service.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void GetById_returns_the_contract_when_found()
    {
        var service = CreateService(out _);
        var created = service.Create(new CreateUserProfileCommand("Emre")).Value!;

        var contract = service.GetById(created.Id);

        Assert.Equal(created, contract);
    }

    [Fact]
    public void GetAll_returns_every_created_profile()
    {
        var service = CreateService(out _);
        service.Create(new CreateUserProfileCommand("Emre"));
        service.Create(new CreateUserProfileCommand("Alex"));

        Assert.Equal(2, service.GetAll().Count);
    }

    [Fact]
    public void Update_changes_the_profile_and_returns_the_updated_contract()
    {
        var service = CreateService(out _);
        var created = service.Create(new CreateUserProfileCommand("Emre")).Value!;

        var result = service.Update(
            created.Id,
            new UpdateUserProfileCommand("Emre Altintas", "https://cdn.bizcord.dev/avatars/emre.png", "Online"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Emre Altintas", result.Value!.DisplayName);
        Assert.Equal("https://cdn.bizcord.dev/avatars/emre.png", result.Value.AvatarUrl);
        Assert.Equal("Online", result.Value.PresenceStatus);
    }

    [Fact]
    public void Update_returns_NotFound_for_an_unknown_id()
    {
        var service = CreateService(out _);

        var result = service.Update(Guid.NewGuid(), new UpdateUserProfileCommand("Emre", null, "Online"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void Update_returns_a_validation_failure_for_an_unknown_presence_status()
    {
        var service = CreateService(out _);
        var created = service.Create(new CreateUserProfileCommand("Emre")).Value!;

        var result = service.Update(created.Id, new UpdateUserProfileCommand("Emre", null, "Busy"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public void Delete_removes_an_existing_profile_and_returns_true()
    {
        var service = CreateService(out _);
        var created = service.Create(new CreateUserProfileCommand("Emre")).Value!;

        Assert.True(service.Delete(created.Id));
        Assert.Null(service.GetById(created.Id));
    }

    [Fact]
    public void Delete_returns_false_for_an_unknown_id()
    {
        var service = CreateService(out _);

        Assert.False(service.Delete(Guid.NewGuid()));
    }
}
