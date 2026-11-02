using UserService.Application.Common;
using UserService.Application.Mapping;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;
using UserService.Messages;

namespace UserService.Application.UserProfiles;

public sealed class UserProfileService : IUserProfileService
{
    private readonly IUserProfileRepository _repository;

    public UserProfileService(IUserProfileRepository repository) => _repository = repository;

    public Result<UserProfileContract> Create(CreateUserProfileCommand command)
    {
        DisplayName displayName;
        try
        {
            displayName = DisplayName.Create(command.DisplayName);
        }
        catch (ArgumentException ex)
        {
            return Result<UserProfileContract>.ValidationFailure(ex.Message);
        }

        var id = command.UserId is { } userId ? new UserId(userId) : UserId.New();
        var profile = UserProfile.Create(id, displayName);
        if (!_repository.Add(profile))
        {
            return Result<UserProfileContract>.Conflict("A profile for this user already exists.");
        }

        return Result<UserProfileContract>.Success(profile.ToContract());
    }

    public UserProfileContract? GetById(Guid id) => _repository.GetById(new UserId(id))?.ToContract();

    public IReadOnlyCollection<UserProfileContract> GetAll() =>
        _repository.GetAll().Select(profile => profile.ToContract()).ToList();

    public Result<UserProfileContract> Update(Guid id, UpdateUserProfileCommand command)
    {
        var profile = _repository.GetById(new UserId(id));
        if (profile is null)
        {
            return Result<UserProfileContract>.NotFound();
        }

        try
        {
            profile.ChangeDisplayName(DisplayName.Create(command.DisplayName));
            profile.ChangeAvatar(
                string.IsNullOrWhiteSpace(command.AvatarUrl) ? null : AvatarReference.Create(command.AvatarUrl));
            profile.ChangePresenceStatus(PresenceStatus.FromName(command.PresenceStatus));
        }
        catch (ArgumentException ex)
        {
            return Result<UserProfileContract>.ValidationFailure(ex.Message);
        }

        _repository.Update(profile);

        return Result<UserProfileContract>.Success(profile.ToContract());
    }

    public bool Delete(Guid id) => _repository.Delete(new UserId(id));
}
