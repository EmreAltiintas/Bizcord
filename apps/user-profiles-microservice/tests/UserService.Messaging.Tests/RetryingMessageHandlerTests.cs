using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UserService.Messaging.Tests;

public class RetryingMessageHandlerTests
{
    private sealed class FlakyHandler : IMessageHandler<string>
    {
        private readonly int _failures;

        public FlakyHandler(int failures) => _failures = failures;

        public int Calls { get; private set; }

        public Task Handle(string message, CancellationToken cancellationToken)
        {
            Calls++;
            return Calls <= _failures ? throw new InvalidOperationException("transient") : Task.CompletedTask;
        }
    }

    private static RetryingMessageHandler<string> Create(IMessageHandler<string> inner, int maxRetries)
    {
        var options = new ResilienceOptions();
        options.Handler.MaxRetryAttempts = maxRetries;
        options.Handler.Delay = TimeSpan.FromMilliseconds(1);

        return new RetryingMessageHandler<string>(
            inner, Options.Create(options), NullLogger<RetryingMessageHandler<string>>.Instance);
    }

    [Fact]
    public async Task Handle_Retries_UntilHandlerSucceeds()
    {
        var inner = new FlakyHandler(failures: 2);

        await Create(inner, maxRetries: 3).Handle("m", CancellationToken.None);

        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task Handle_Rethrows_AfterMaxRetries_SoBrokerCanDeadLetter()
    {
        var inner = new FlakyHandler(failures: int.MaxValue);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Create(inner, maxRetries: 3).Handle("m", CancellationToken.None));

        Assert.Equal(4, inner.Calls);
    }

    [Fact]
    public async Task Handle_DoesNotRetry_WhenFirstAttemptSucceeds()
    {
        var inner = new FlakyHandler(failures: 0);

        await Create(inner, maxRetries: 3).Handle("m", CancellationToken.None);

        Assert.Equal(1, inner.Calls);
    }
}
