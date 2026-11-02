using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using UserService.Api.Tests.Security;
using UserService.Messages;
using UserService.Messaging;

namespace UserService.Api.Tests;

/// <summary>Needs a running RabbitMQ (docker compose up -d rabbitmq).</summary>
public class MessagePostedIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MessagePostedIntegrationTests(ApiFactory factory) => _factory = factory;

    // Integration test: real broker, real handler, no HTTP for the event itself
    [Fact]
    public async Task MessagePostedEvent_IsConsumed_AndResultEventPublished()
    {
        var http = _factory.CreateClient();
        var userId = Guid.NewGuid();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(userId, scope: "user-profiles.write"));
        http.DefaultRequestHeaders.Add("X-UserId", userId.ToString());
        var created = await http.PostAsJsonAsync("/api/user-profiles", new { DisplayName = "Emre" });
        var profile = await created.Content.ReadFromJsonAsync<UserProfileContract>();

        var client = _factory.Services.GetRequiredService<IMessageClient>();
        var messageId = Guid.NewGuid();
        var capture = new MessageCapture<UserActivityRecordedEvent>(client);

        await client.PublishAsync(new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = profile!.Id,
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        });

        var result = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(messageId, result.MessageId);
    }
}
