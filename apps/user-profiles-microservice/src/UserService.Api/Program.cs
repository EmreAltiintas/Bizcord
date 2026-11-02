using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using UserService.Api;
using UserService.Application;
using UserService.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required (environment variable Jwt__SigningKey).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization(AuthorizationPolicies.Register);

builder.Services.AddControllers();
builder.Services.AddApplication();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Messaging: registers IMessageClient (EasyNetQ-backed) behind our broker-agnostic
// abstraction. Points at the local docker-compose RabbitMQ by default; override via the
// "RabbitMq" connection string (appsettings.json / environment variables).
builder.Services.AddMessageClient(options =>
    options.ConnectionString =
        builder.Configuration.GetConnectionString("RabbitMq") ?? "host=localhost");

builder.Services.Configure<ResilienceOptions>(
    builder.Configuration.GetSection(ResilienceOptions.SectionName));

builder.Services.AddSingleton<MessageSubscriptionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MessageSubscriptionService>());

builder.Services.AddHealthChecks()
    .AddCheck<MessagingHealthCheck>("messaging", tags: new[] { "ready" });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();


public partial class Program;

