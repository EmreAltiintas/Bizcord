using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;

namespace UserService.Application.UserProfiles;

public interface IUserProfileRepository
{
    void Add(UserProfile profile);

    UserProfile? GetById(UserId id);

    IReadOnlyCollection<UserProfile> GetAll();

    void Update(UserProfile profile);

    bool Delete(UserId id);
}
