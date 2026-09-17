using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UserService.Api.Contracts;
using UserService.Api.Controllers;
using UserService.Application.UserProfiles;
using UserService.Messages;

namespace UserService.Api.Tests.Controllers;

public class UserProfilesControllerTests
{
    private static UserProfilesController CreateController(out IUserProfileService service)
    {
        service = new UserProfileService(new InMemoryUserProfileRepository());
        return new UserProfilesController(service);
    }

    [Fact]
    public void Create_returns_201_with_the_created_profile()
    {
        var controller = CreateController(out _);

        var result = controller.Create(new CreateUserProfileRequest("Emre"));

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var contract = Assert.IsType<UserProfileContract>(created.Value);
        Assert.Equal("Emre", contract.DisplayName);
        Assert.Equal(nameof(UserProfilesController.GetById), created.ActionName);
    }

    [Fact]
    public void Create_returns_400_for_an_invalid_display_name()
    {
        var controller = CreateController(out _);

        var result = controller.Create(new CreateUserProfileRequest("a"));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void GetById_returns_200_when_found()
    {
        var controller = CreateController(out var service);
        var created = service.Create(new CreateUserProfileCommand("Emre")).Value!;

        var result = controller.GetById(created.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(created, ok.Value);
    }

    [Fact]
    public void GetById_returns_404_when_not_found()
    {
        var controller = CreateController(out _);

        var result = controller.GetById(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetAll_returns_200_with_every_profile()
    {
        var controller = CreateController(out var service);
        service.Create(new CreateUserProfileCommand("Emre"));
        service.Create(new CreateUserProfileCommand("Alex"));

        var result = controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var contracts = Assert.IsAssignableFrom<IReadOnlyCollection<UserProfileContract>>(ok.Value);
        Assert.Equal(2, contracts.Count);
    }

    [Fact]
    public void Update_returns_200_with_the_updated_profile()
    {
        var controller = CreateController(out var service);
        var created = service.Create(new CreateUserProfileCommand("Emre")).Value!;

        var result = controller.Update(created.Id, new UpdateUserProfileRequest("Emre A", null, "Online"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var contract = Assert.IsType<UserProfileContract>(ok.Value);
        Assert.Equal("Emre A", contract.DisplayName);
    }

    [Fact]
    public void Update_returns_404_when_not_found()
    {
        var controller = CreateController(out _);

        var result = controller.Update(Guid.NewGuid(), new UpdateUserProfileRequest("Emre", null, "Online"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Update_returns_400_for_an_invalid_presence_status()
    {
        var controller = CreateController(out var service);
        var created = service.Create(new CreateUserProfileCommand("Emre")).Value!;

        var result = controller.Update(created.Id, new UpdateUserProfileRequest("Emre", null, "Busy"));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void Delete_returns_204_when_found()
    {
        var controller = CreateController(out var service);
        var created = service.Create(new CreateUserProfileCommand("Emre")).Value!;

        var result = controller.Delete(created.Id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void Delete_returns_404_when_not_found()
    {
        var controller = CreateController(out _);

        var result = controller.Delete(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }
}
