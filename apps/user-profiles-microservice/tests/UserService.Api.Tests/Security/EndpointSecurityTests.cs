using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UserService.Messages;
using UserService.Messaging;

namespace UserService.Api.Tests.Security;

public class EndpointSecurityTests : IClassFixture<EndpointSecurityTests.FakeBrokerFactory>
{
    public sealed class FakeBrokerFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMessageClient>();
                services.AddSingleton<IMessageClient>(new FakeMessageClient());
            });
        }
    }

    private readonly HttpClient _http;

    public EndpointSecurityTests(FakeBrokerFactory factory) => _http = factory.CreateClient();

    private static HttpRequestMessage Request(HttpMethod method, string url, string? token = null, Guid? userIdHeader = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (userIdHeader is { } id)
        {
            request.Headers.Add("X-UserId", id.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<HttpStatusCode> Send(HttpRequestMessage request) => (await _http.SendAsync(request)).StatusCode;

    private async Task<Guid> CreateProfile(Guid? subject = null)
    {
        var sub = subject ?? Guid.NewGuid();
        var token = TestTokens.Create(sub, scope: "user-profiles.write");
        var response = await _http.SendAsync(
            Request(HttpMethod.Post, "/api/user-profiles", token, sub, new { DisplayName = "Emre" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return sub;
    }

    // Internal endpoints (role internal-service)

    [Fact]
    public async Task Internal_NoToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Send(Request(HttpMethod.Get, "/api/user-profiles")));

    [Fact]
    public async Task Internal_TokenSignedWithWrongKey_Returns401()
    {
        var token = TestTokens.Create(Guid.NewGuid(), role: "internal-service", signingKey: "another-key-another-key-another-key-0123456789");

        Assert.Equal(HttpStatusCode.Unauthorized, await Send(Request(HttpMethod.Get, "/api/user-profiles", token)));
    }

    [Fact]
    public async Task Internal_ExpiredToken_Returns401()
    {
        var token = TestTokens.Create(Guid.NewGuid(), role: "internal-service", expires: DateTime.UtcNow.AddMinutes(-5));

        Assert.Equal(HttpStatusCode.Unauthorized, await Send(Request(HttpMethod.Get, "/api/user-profiles", token)));
    }

    [Fact]
    public async Task Internal_ValidTokenWithoutRole_Returns403()
    {
        var token = TestTokens.Create(Guid.NewGuid(), role: "member", scope: "user-profiles.read user-profiles.write");

        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(HttpMethod.Get, "/api/user-profiles", token)));
        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(HttpMethod.Get, $"/api/user-profiles/{Guid.NewGuid()}", token)));
    }

    [Fact]
    public async Task Internal_ValidTokenWithRole_Returns200()
    {
        var sub = await CreateProfile();
        var token = TestTokens.Create(Guid.NewGuid(), role: "internal-service");

        Assert.Equal(HttpStatusCode.OK, await Send(Request(HttpMethod.Get, "/api/user-profiles", token)));
        Assert.Equal(HttpStatusCode.OK, await Send(Request(HttpMethod.Get, $"/api/user-profiles/{sub}", token)));
    }

    // GET /me (scope user-profiles.read, X-UserId must match sub)

    [Fact]
    public async Task Me_NoToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Send(Request(HttpMethod.Get, "/api/user-profiles/me", userIdHeader: Guid.NewGuid())));

    [Fact]
    public async Task Me_ValidTokenWithoutReadScope_Returns403()
    {
        var sub = Guid.NewGuid();
        var token = TestTokens.Create(sub, scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(HttpMethod.Get, "/api/user-profiles/me", token, sub)));
    }

    [Fact]
    public async Task Me_ScopeIsSpaceSeparated_Returns200()
    {
        var sub = Guid.NewGuid();
        var token = TestTokens.Create(sub, scope: "openid user-profiles.read other");

        Assert.Equal(HttpStatusCode.OK, await Send(Request(HttpMethod.Get, "/api/user-profiles/me", token, sub)));
    }

    [Fact]
    public async Task Me_HeaderDiffersFromSubject_Returns403()
    {
        var token = TestTokens.Create(Guid.NewGuid(), scope: "user-profiles.read");

        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(HttpMethod.Get, "/api/user-profiles/me", token, Guid.NewGuid())));
    }

    [Fact]
    public async Task Me_MissingHeader_Returns400()
    {
        var token = TestTokens.Create(Guid.NewGuid(), scope: "user-profiles.read");

        Assert.Equal(HttpStatusCode.BadRequest, await Send(Request(HttpMethod.Get, "/api/user-profiles/me", token)));
    }

    [Fact]
    public async Task Me_InvalidHeader_Returns400()
    {
        var request = Request(HttpMethod.Get, "/api/user-profiles/me", TestTokens.Create(Guid.NewGuid(), scope: "user-profiles.read"));
        request.Headers.Add("X-UserId", "not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, await Send(request));
    }

    [Fact]
    public async Task Me_TokenWithoutSubject_Returns400()
    {
        var token = TestTokens.Create(scope: "user-profiles.read");

        Assert.Equal(HttpStatusCode.BadRequest, await Send(Request(HttpMethod.Get, "/api/user-profiles/me", token, Guid.NewGuid())));
    }

    // POST (scope user-profiles.write, id = sub)

    [Fact]
    public async Task Create_NoToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Send(Request(HttpMethod.Post, "/api/user-profiles", userIdHeader: Guid.NewGuid(), body: new { DisplayName = "Emre" })));

    [Fact]
    public async Task Create_ValidTokenWithoutWriteScope_Returns403()
    {
        var sub = Guid.NewGuid();
        var token = TestTokens.Create(sub, scope: "user-profiles.read");

        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(HttpMethod.Post, "/api/user-profiles", token, sub, new { DisplayName = "Emre" })));
    }

    [Fact]
    public async Task Create_UsesSubjectAsId_AndIgnoresIdInBody()
    {
        var sub = Guid.NewGuid();
        var token = TestTokens.Create(sub, scope: "user-profiles.write");

        var response = await _http.SendAsync(Request(
            HttpMethod.Post, "/api/user-profiles", token, sub, new { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DisplayName = "Emre" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileContract>();
        Assert.Equal(sub, profile!.Id);
    }

    [Fact]
    public async Task Create_Duplicate_Returns409()
    {
        var sub = await CreateProfile();
        var token = TestTokens.Create(sub, scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.Conflict, await Send(Request(HttpMethod.Post, "/api/user-profiles", token, sub, new { DisplayName = "Emre" })));
    }

    [Fact]
    public async Task Create_HeaderDiffersFromSubject_Returns403()
    {
        var token = TestTokens.Create(Guid.NewGuid(), scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(HttpMethod.Post, "/api/user-profiles", token, Guid.NewGuid(), new { DisplayName = "Emre" })));
    }

    [Fact]
    public async Task Create_MissingHeader_Returns400()
    {
        var token = TestTokens.Create(Guid.NewGuid(), scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.BadRequest, await Send(Request(HttpMethod.Post, "/api/user-profiles", token, body: new { DisplayName = "Emre" })));
    }

    [Fact]
    public async Task Create_SubjectIsNotAGuid_Returns400()
    {
        var token = TestTokens.Create(scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.BadRequest, await Send(Request(HttpMethod.Post, "/api/user-profiles", token, Guid.NewGuid(), new { DisplayName = "Emre" })));
    }

    // PUT and DELETE (scope user-profiles.write, sub must equal the profile id)

    [Fact]
    public async Task Update_Owner_Returns200()
    {
        var sub = await CreateProfile();
        var token = TestTokens.Create(sub, scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.OK, await Send(Request(
            HttpMethod.Put, $"/api/user-profiles/{sub}", token, body: new { DisplayName = "Emre A", PresenceStatus = "Online" })));
    }

    [Fact]
    public async Task Update_AnotherUsersProfile_Returns403()
    {
        var victim = await CreateProfile();
        var token = TestTokens.Create(Guid.NewGuid(), scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(
            HttpMethod.Put, $"/api/user-profiles/{victim}", token, body: new { DisplayName = "Hacked", PresenceStatus = "Online" })));
    }

    [Fact]
    public async Task Update_WithoutWriteScope_Returns403()
    {
        var sub = await CreateProfile();
        var token = TestTokens.Create(sub, scope: "user-profiles.read");

        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(
            HttpMethod.Put, $"/api/user-profiles/{sub}", token, body: new { DisplayName = "Emre A", PresenceStatus = "Online" })));
    }

    [Fact]
    public async Task Delete_AnotherUsersProfile_Returns403()
    {
        var victim = await CreateProfile();
        var token = TestTokens.Create(Guid.NewGuid(), scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.Forbidden, await Send(Request(HttpMethod.Delete, $"/api/user-profiles/{victim}", token)));
    }

    [Fact]
    public async Task Delete_Owner_Returns204()
    {
        var sub = await CreateProfile();
        var token = TestTokens.Create(sub, scope: "user-profiles.write");

        Assert.Equal(HttpStatusCode.NoContent, await Send(Request(HttpMethod.Delete, $"/api/user-profiles/{sub}", token)));
    }

    [Fact]
    public async Task Delete_NoToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Send(Request(HttpMethod.Delete, $"/api/user-profiles/{Guid.NewGuid()}")));

    // Health

    [Fact]
    public async Task Health_IsOpenWithoutToken() =>
        Assert.Equal(HttpStatusCode.OK, await Send(Request(HttpMethod.Get, "/health/live")));
}
