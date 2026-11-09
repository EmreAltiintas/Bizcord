namespace SagaStubs.Worker;

public sealed class StubOptions
{
    public const string SectionName = "Stub";

    public const string ChannelRole = "channel";

    public const string StreamingRole = "streaming";

    /// <summary>Which service to simulate: <c>channel</c> or <c>streaming</c>.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Fails the first N attempts per saga id and operation. 0 never fails, a large number such as 99 always fails.
    /// </summary>
    public int FailFirstAttempts { get; set; }
}
