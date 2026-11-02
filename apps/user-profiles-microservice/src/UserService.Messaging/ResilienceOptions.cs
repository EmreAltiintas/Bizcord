namespace UserService.Messaging;

public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    public SubscribeOptions Subscribe { get; set; } = new();

    public HandlerOptions Handler { get; set; } = new();

    public PublishOptions Publish { get; set; } = new();

    public sealed class SubscribeOptions
    {
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(2);

        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    }

    public sealed class HandlerOptions
    {
        public int MaxRetryAttempts { get; set; } = 3;

        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
    }

    public sealed class PublishOptions
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

        public double FailureRatio { get; set; } = 0.5;

        public int MinimumThroughput { get; set; } = 5;

        public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(15);
    }
}
