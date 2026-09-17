using Microsoft.AspNetCore.Mvc;
using UserService.Api.Contracts;
using UserService.Application.Common;
using UserService.Application.UserProfiles;
using UserService.Messages;

namespace UserService.Api.Controllers;

[ApiController]
[Route("api/user-profiles")]
public sealed class UserProfilesController : ControllerBase
{
    private readonly IUserProfileService _userProfileService;

    public UserProfilesController(IUserProfileService userProfileService) =>
        _userProfileService = userProfileService;

    [HttpPost]
    [ProducesResponseType(typeof(UserProfileContract), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Create([FromBody] CreateUserProfileRequest request)
    {
        var result = _userProfileService.Create(new CreateUserProfileCommand(request.DisplayName));

        if (!result.IsSuccess)
        {
            return Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserProfileContract), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(Guid id)
    {
        var contract = _userProfileService.GetById(id);

        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserProfileContract>), StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(_userProfileService.GetAll());

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserProfileContract), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(Guid id, [FromBody] UpdateUserProfileRequest request)
    {
        var result = _userProfileService.Update(
            id, new UpdateUserProfileCommand(request.DisplayName, request.AvatarUrl, request.PresenceStatus));

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.ErrorType == ResultErrorType.NotFound
            ? NotFound()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id) => _userProfileService.Delete(id) ? NoContent() : NotFound();
}
