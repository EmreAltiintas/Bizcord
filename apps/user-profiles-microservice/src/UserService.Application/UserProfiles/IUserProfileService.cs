using UserService.Application.Common;
using UserService.Messages;

namespace UserService.Application.UserProfiles;

public interface IUserProfileService
{
    Result<UserProfileContract> Create(CreateUserProfileCommand command);

    UserProfileContract? GetById(Guid id);

    IReadOnlyCollection<UserProfileContract> GetAll();

    Result<UserProfileContract> Update(Guid id, UpdateUserProfileCommand command);

    bool Delete(Guid id);
}
