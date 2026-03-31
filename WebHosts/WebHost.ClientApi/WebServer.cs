using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Web;
using Shared.Web.Rin;
using WebHost.ClientApi.Characters;

namespace WebHost.ClientApi;

public class WebServer : BaseWebServer
{
    public WebServer(IConfiguration configuration)
        : base(configuration)
    {
    }

    protected override void ConfigureChildServices(IServiceCollection services)
    {
        services.Configure<RinSettings>(Configuration.GetSection("RinSettings"));
        services.AddHttpClient<IRinClient, RinClient>();
        services.AddScoped<ICharactersRepository, CharactersRepository>();
    }

    protected override void ConfigureChild(IApplicationBuilder app, IWebHostEnvironment env)
    {
    }
}