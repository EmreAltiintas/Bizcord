using UserService.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Messaging: registers IMessageClient (EasyNetQ-backed) behind our broker-agnostic
// abstraction. Points at the local docker-compose RabbitMQ by default; override via the
// "RabbitMq" connection string (appsettings.json / environment variables).
builder.Services.AddMessageClient(options =>
    options.ConnectionString =
        builder.Configuration.GetConnectionString("RabbitMq") ?? "host=localhost");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
