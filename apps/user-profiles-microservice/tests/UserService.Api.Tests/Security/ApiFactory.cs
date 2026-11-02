using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UserService.Api.Tests.Security;

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Vault:Enabled", "false");
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
    }
}
