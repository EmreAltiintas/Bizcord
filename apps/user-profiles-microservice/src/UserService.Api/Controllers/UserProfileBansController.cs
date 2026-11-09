using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Api.Contracts;
using UserService.Application.Sagas.Ban;
using UserService.Messages.Bans;
using UserService.Messaging;

namespace UserService.Api.Controllers;

/// <summary>
/// Starts and inspects the ban saga. The endpoints only publish commands; the saga itself
/// runs in <see cref="BanSagaOrchestrator"/>.
/// </summary>
[ApiController]
[Route("api/user-profiles")]
[Authorize(Policy = AuthorizationPolicies.InternalService)]
public sealed class UserProfileBansController : ControllerBase
{
    private readonly IMessageClient _messageClient;
    private readonly ISagaStateRepository _sagaStates;

    public UserProfileBansController(IMessageClient messageClient, ISagaStateRepository sagaStates)
    {
        _messageClient = messageClient;
        _sagaStates = sagaStates;
    }

    [HttpPost("{id:guid}/ban")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ban(
        Guid id, [FromBody] BanUserProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetModeratorId(out var moderatorId))
        {
            return Problem(detail: "Token has no valid sub claim.", statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new BanUserCommand { UserId = id, ModeratorId = moderatorId, Reason = request.Reason };

        var failure = await TryPublish(command, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        return AcceptedAtAction(nameof(GetSaga), new { sagaId = command.SagaId }, new { sagaId = command.SagaId });
    }

    [HttpPost("{id:guid}/unban")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Unban(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetModeratorId(out var moderatorId))
        {
            return Problem(detail: "Token has no valid sub claim.", statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new UnbanUserCommand { UserId = id, ModeratorId = moderatorId };

        var failure = await TryPublish(command, cancellationToken);

        return failure ?? Accepted(new { sagaId = command.SagaId });
    }

    [HttpGet("sagas/{sagaId:guid}")]
    [ProducesResponseType(typeof(BanSagaState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSaga(Guid sagaId)
    {
        var state = await _sagaStates.GetById(sagaId);

        return state is null ? NotFound() : Ok(state);
    }

    private bool TryGetModeratorId(out Guid moderatorId) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out moderatorId);

    private async Task<IActionResult?> TryPublish<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class
    {
        try
        {
            await _messageClient.PublishAsync(message, cancellationToken);
            return null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                detail: "The message broker is unavailable. Try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
