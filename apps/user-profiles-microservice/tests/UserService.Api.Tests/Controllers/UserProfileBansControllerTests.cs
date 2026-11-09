using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UserService.Api.Tests.Security;
using UserService.Messages.Bans;
using UserService.Messaging;

namespace UserService.Api.Tests.Controllers;

public class UserProfileBansControllerTests : IClassFixture<UserProfileBansControllerTests.BanFactory>
{
    public sealed class BanFactory : ApiFactory
    {
        public FakeMessageClient Client { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMessageClient>();
                services.AddSingleton<IMessageClient>(Client);
            });
        }
    }

    private readonly BanFactory _factory;
    private readonly HttpClient _http;
    private readonly Guid _moderatorId = Guid.NewGuid();

    public UserProfileBansControllerTests(BanFactory factory)
    {
        _factory = factory;
        _http = factory.CreateClient();
    }

    private HttpRequestMessage Request(HttpMethod method, string url, string? role = "internal-service", object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (role is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", TestTokens.Create(subject: _moderatorId, role: role));
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    [Fact]
    public async Task Ban_publishes_a_command_and_returns_202_with_the_saga_id()
    {
        var userId = Guid.NewGuid();

        var response = await _http.SendAsync(Request(HttpMethod.Post, $"/api/user-profiles/{userId}/ban", body: new { reason = "spam" }));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var command = _factory.Client.Published.OfType<BanUserCommand>().Single(c => c.UserId == userId);
        Assert.Equal(_moderatorId, command.ModeratorId);
        Assert.Equal("spam", command.Reason);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        Assert.Equal(command.SagaId, body!["sagaId"]);
        Assert.EndsWith($"/api/user-profiles/sagas/{command.SagaId}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Ban_without_a_reason_returns_400()
    {
        var response = await _http.SendAsync(Request(HttpMethod.Post, $"/api/user-profiles/{Guid.NewGuid()}/ban", body: new { }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ban_without_token_returns_401()
    {
        var response = await _http.SendAsync(Request(HttpMethod.Post, $"/api/user-profiles/{Guid.NewGuid()}/ban", role: null, body: new { reason = "spam" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ban_without_the_internal_service_role_returns_403()
    {
        var response = await _http.SendAsync(Request(HttpMethod.Post, $"/api/user-profiles/{Guid.NewGuid()}/ban", role: "member", body: new { reason = "spam" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unban_publishes_a_command_and_returns_202()
    {
        var userId = Guid.NewGuid();

        var response = await _http.SendAsync(Request(HttpMethod.Post, $"/api/user-profiles/{userId}/unban"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var command = _factory.Client.Published.OfType<UnbanUserCommand>().Single(c => c.UserId == userId);
        Assert.Equal(_moderatorId, command.ModeratorId);
    }

    [Fact]
    public async Task GetSaga_returns_404_for_an_unknown_saga()
    {
        var response = await _http.SendAsync(Request(HttpMethod.Get, $"/api/user-profiles/sagas/{Guid.NewGuid()}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
