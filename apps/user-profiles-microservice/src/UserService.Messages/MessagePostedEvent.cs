namespace UserService.Messages;

/// <summary>Published by the Messaging Service when a message has been posted in a channel.</summary>
public sealed class MessagePostedEvent
{
    public Guid MessageId { get; init; }

    public Guid ChannelId { get; init; }

    public Guid AuthorId { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTime PostedAt { get; init; }
}
