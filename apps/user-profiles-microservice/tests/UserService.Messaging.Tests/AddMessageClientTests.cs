using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UserService.Messaging.Tests;

/// <summary>
/// Covers <see cref="MessagingServiceCollectionExtensions.AddMessageClient"/> DI wiring and
/// options binding. Nothing here resolves <see cref="IMessageClient"/> itself, so no
/// RabbitMQ connection is ever attempted.
/// </summary>
public class AddMessageClientTests
{
    [Fact]
    public void Registers_IMessageClient_as_singleton_backed_by_EasyNetQMessageClient()
    {
        var services = new ServiceCollection();

        services.AddMessageClient(_ => { });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IMessageClient));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(EasyNetQMessageClient), descriptor.ImplementationType);
    }

    [Fact]
    public void Registers_resolvable_MessageClientOptions()
    {
        var services = new ServiceCollection();

        services.AddMessageClient(_ => { });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IOptions<MessageClientOptions>>());
    }

    [Fact]
    public void Applies_the_configureOptions_delegate()
    {
        var services = new ServiceCollection();
        const string connectionString = "host=rabbitmq;username=u;password=p;virtualHost=/bizcord";

        services.AddMessageClient(o => o.ConnectionString = connectionString);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MessageClientOptions>>().Value;
        Assert.Equal(connectionString, options.ConnectionString);
    }

    [Fact]
    public void Defaults_ConnectionString_to_local_rabbitmq_when_not_overridden()
    {
        var services = new ServiceCollection();

        services.AddMessageClient(_ => { });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MessageClientOptions>>().Value;
        Assert.Equal("host=localhost", options.ConnectionString);
    }

    [Fact]
    public void Returns_the_same_service_collection_for_chaining()
    {
        var services = new ServiceCollection();

        var result = services.AddMessageClient(_ => { });

        Assert.Same(services, result);
    }

    [Fact]
    public void Throws_when_services_is_null()
    {
        Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddMessageClient(_ => { }));
    }

    [Fact]
    public void Throws_when_configureOptions_is_null()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddMessageClient(null!));
    }
}
