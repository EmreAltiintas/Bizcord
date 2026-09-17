using System.Collections.Concurrent;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;

namespace UserService.Application.UserProfiles;

public sealed class InMemoryUserProfileRepository : IUserProfileRepository
{
    private readonly ConcurrentDictionary<Guid, UserProfile> _profiles = new();

    public void Add(UserProfile profile) => _profiles[profile.Id.Value] = profile;

    public UserProfile? GetById(UserId id) => _profiles.GetValueOrDefault(id.Value);

    public IReadOnlyCollection<UserProfile> GetAll() => _profiles.Values.ToList();

    public void Update(UserProfile profile) => _profiles[profile.Id.Value] = profile;

    public bool Delete(UserId id) => _profiles.TryRemove(id.Value, out _);
}
