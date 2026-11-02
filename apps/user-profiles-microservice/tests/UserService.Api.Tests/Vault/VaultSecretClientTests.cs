using System.Net;
using System.Text;
using UserService.Api.Vault;

namespace UserService.Api.Tests.Vault;

public class VaultSecretClientTests
{
    private const string Token = "test-app-token";
    private const string SecretValue = "test-secret-value";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
            _respond = respond;

        public int Calls { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            return _respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Secret(string json = "{\"data\":{\"data\":{\"jwt-signing-key\":\"" + SecretValue + "\"}}}") =>
        Json(HttpStatusCode.OK, json);

    private static VaultSecretClient Create(StubHandler handler, TimeSpan? timeout = null)
    {
        var options = new VaultOptions { MaxAttempts = 3, RetryDelay = TimeSpan.FromMilliseconds(1) };
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://vault.test/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };

        return new VaultSecretClient(http, Token, options);
    }

    [Fact]
    public async Task GetSecret_ReturnsValue_AndSendsTokenToTheKv2Path()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Secret()));

        var value = await Create(handler).GetSecretAsync();

        Assert.Equal(SecretValue, value);
        Assert.Equal("/v1/secret/data/user-profiles", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(Token, Assert.Single(handler.LastRequest.Headers.GetValues("X-Vault-Token")));
    }

    [Fact]
    public async Task GetSecret_Forbidden_FailsOnceWithoutRetry_AndDoesNotLeakTheToken()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.Forbidden, "{\"errors\":[\"permission denied\"]}")));

        var ex = await Assert.ThrowsAsync<VaultException>(() => Create(handler).GetSecretAsync());

        Assert.Equal(1, handler.Calls);
        Assert.Contains("denied", ex.Message);
        Assert.DoesNotContain(Token, ex.Message);
    }

    [Fact]
    public async Task GetSecret_NotFound_Fails()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.NotFound, "{}")));

        var ex = await Assert.ThrowsAsync<VaultException>(() => Create(handler).GetSecretAsync());

        Assert.Equal(1, handler.Calls);
        Assert.Contains("No secret found", ex.Message);
    }

    [Theory]
    [InlineData("{\"data\":{\"data\":{}}}")]
    [InlineData("{\"data\":{\"data\":{\"jwt-signing-key\":\"\"}}}")]
    [InlineData("{\"data\":{\"data\":{\"jwt-signing-key\":null}}}")]
    [InlineData("{}")]
    public async Task GetSecret_MissingOrEmptyValue_Fails(string body)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Secret(body)));

        var ex = await Assert.ThrowsAsync<VaultException>(() => Create(handler).GetSecretAsync());

        Assert.Contains("has no value", ex.Message);
    }

    [Fact]
    public async Task GetSecret_SealedThenAvailable_RetriesAndSucceeds()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) => Task.FromResult(
            ++calls < 2 ? Json(HttpStatusCode.ServiceUnavailable, "{}") : Secret()));

        var value = await Create(handler).GetSecretAsync();

        Assert.Equal(SecretValue, value);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetSecret_AlwaysSealed_FailsAfterMaxAttempts()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{}")));

        var ex = await Assert.ThrowsAsync<VaultException>(() => Create(handler).GetSecretAsync());

        Assert.Equal(3, handler.Calls);
        Assert.Contains("sealed", ex.Message);
    }

    [Fact]
    public async Task GetSecret_NetworkError_RetriesThenFails()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<VaultException>(() => Create(handler).GetSecretAsync());

        Assert.Equal(3, handler.Calls);
        Assert.Contains("unreachable", ex.Message);
    }

    [Fact]
    public async Task GetSecret_Timeout_RetriesThenFails()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Secret();
        });

        var ex = await Assert.ThrowsAsync<VaultException>(
            () => Create(handler, TimeSpan.FromMilliseconds(50)).GetSecretAsync());

        Assert.Equal(3, handler.Calls);
        Assert.Contains("did not answer", ex.Message);
    }
}
