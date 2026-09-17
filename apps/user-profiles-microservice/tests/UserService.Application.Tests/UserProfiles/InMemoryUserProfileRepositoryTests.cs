using UserService.Application.UserProfiles;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;

namespace UserService.Application.Tests.UserProfiles;

public class InMemoryUserProfileRepositoryTests
{
    [Fact]
    public void GetById_returns_null_when_not_found()
    {
        var repository = new InMemoryUserProfileRepository();

        Assert.Null(repository.GetById(UserId.New()));
    }

    [Fact]
    public void Add_then_GetById_returns_the_stored_profile()
    {
        var repository = new InMemoryUserProfileRepository();
        var profile = UserProfile.Create(UserId.New(), DisplayName.Create("Emre"));

        repository.Add(profile);

        Assert.Same(profile, repository.GetById(profile.Id));
    }

    [Fact]
    public void GetAll_returns_every_stored_profile()
    {
        var repository = new InMemoryUserProfileRepository();
        var first = UserProfile.Create(UserId.New(), DisplayName.Create("Emre"));
        var second = UserProfile.Create(UserId.New(), DisplayName.Create("Alex"));
        repository.Add(first);
        repository.Add(second);

        var all = repository.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Contains(first, all);
        Assert.Contains(second, all);
    }

    [Fact]
    public void Delete_removes_the_profile_and_returns_true()
    {
        var repository = new InMemoryUserProfileRepository();
        var profile = UserProfile.Create(UserId.New(), DisplayName.Create("Emre"));
        repository.Add(profile);

        var deleted = repository.Delete(profile.Id);

        Assert.True(deleted);
        Assert.Null(repository.GetById(profile.Id));
    }

    [Fact]
    public void Delete_returns_false_when_not_found()
    {
        var repository = new InMemoryUserProfileRepository();

        Assert.False(repository.Delete(UserId.New()));
    }
}
