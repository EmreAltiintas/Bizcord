using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Api.Contracts;
using UserService.Application.Common;
using UserService.Application.UserProfiles;
using UserService.Messages;

namespace UserService.Api.Controllers;

[ApiController]
[Route("api/user-profiles")]
[Authorize]
public sealed class UserProfilesController : ControllerBase
{
    private readonly IUserProfileService _userProfileService;

    public UserProfilesController(IUserProfileService userProfileService) =>
        _userProfileService = userProfileService;

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ProfilesWrite)]
    [ProducesResponseType(typeof(UserProfileContract), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create(
        [FromBody] CreateUserProfileRequest request,
        [FromHeader(Name = "X-UserId")] string? userId)
    {
        var error = ResolveCaller(userId, out var callerId);
        if (error is not null)
        {
            return error;
        }

        var result = _userProfileService.Create(new CreateUserProfileCommand(request.DisplayName, callerId));

        if (!result.IsSuccess)
        {
            return Problem(
                detail: result.Error,
                statusCode: result.ErrorType == ResultErrorType.Conflict
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // The API gateway validates the JWT and sets X-UserId from its "sub" claim. The service
    // validates the token itself and requires the header to match the token subject.
    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.ProfilesRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult Me([FromHeader(Name = "X-UserId")] string? userId)
    {
        var error = ResolveCaller(userId, out var callerId);

        return error ?? Ok(new { userId = callerId });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.InternalService)]
    [ProducesResponseType(typeof(UserProfileContract), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(Guid id)
    {
        var contract = _userProfileService.GetById(id);

        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.InternalService)]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserProfileContract>), StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(_userProfileService.GetAll());

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ProfilesWrite)]
    [ProducesResponseType(typeof(UserProfileContract), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(Guid id, [FromBody] UpdateUserProfileRequest request)
    {
        if (!IsOwner(id))
        {
            return Problem(detail: "Token subject does not match the profile.", statusCode: StatusCodes.Status403Forbidden);
        }

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
    [Authorize(Policy = AuthorizationPolicies.ProfilesWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id)
    {
        if (!IsOwner(id))
        {
            return Problem(detail: "Token subject does not match the profile.", statusCode: StatusCodes.Status403Forbidden);
        }

        return _userProfileService.Delete(id) ? NoContent() : NotFound();
    }

    private bool IsOwner(Guid id) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var subject) && subject == id;

    private IActionResult? ResolveCaller(string? headerUserId, out Guid callerId)
    {
        callerId = Guid.Empty;

        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var subject))
        {
            return Problem(detail: "Token has no valid sub claim.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(headerUserId))
        {
            return Problem(detail: "Missing X-UserId header.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Guid.TryParse(headerUserId, out var headerId))
        {
            return Problem(detail: "X-UserId is not a valid Guid.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (headerId != subject)
        {
            return Problem(detail: "X-UserId does not match the token subject.", statusCode: StatusCodes.Status403Forbidden);
        }

        callerId = subject;
        return null;
    }
}
