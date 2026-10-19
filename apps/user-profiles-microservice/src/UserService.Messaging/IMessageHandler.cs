namespace UserService.Messaging;

/// <summary>Handles messages of type <typeparamref name="TMessage"/> received from the broker.</summary>
public interface IMessageHandler<in TMessage>
    where TMessage : class
{
    Task Handle(TMessage message, CancellationToken cancellationToken);
}
