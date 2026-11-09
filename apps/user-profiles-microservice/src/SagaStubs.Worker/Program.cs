using SagaStubs.Worker;
using UserService.Messaging;

var builder = Host.CreateApplicationBuilder(args);

var stub = builder.Configuration.GetSection(StubOptions.SectionName).Get<StubOptions>() ?? new StubOptions();

builder.Services.AddMessageClient(options =>
    options.ConnectionString =
        builder.Configuration.GetConnectionString("RabbitMq") ?? "host=localhost");

builder.Services.Configure<ResilienceOptions>(
    builder.Configuration.GetSection(ResilienceOptions.SectionName));

builder.Services.AddSingleton(new FailureTracker(stub.FailFirstAttempts));

switch (stub.Role.ToLowerInvariant())
{
    case StubOptions.ChannelRole:
        builder.Services.AddHostedService<ChannelStub>();
        break;
    case StubOptions.StreamingRole:
        builder.Services.AddHostedService<StreamingStub>();
        break;
    default:
        throw new InvalidOperationException(
            $"{StubOptions.SectionName}:Role must be '{StubOptions.ChannelRole}' or '{StubOptions.StreamingRole}', but was '{stub.Role}'.");
}

builder.Build().Run();
