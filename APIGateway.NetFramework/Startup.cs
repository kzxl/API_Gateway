using System;
using System.Web.Http;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(APIGateway.NetFramework.Startup))]

namespace APIGateway.NetFramework
{
    /// <summary>
    /// OWIN Startup class for .NET Framework 4.8 API Gateway.
    /// UArch: OWIN pipeline equivalent to ASP.NET Core middleware.
    /// </summary>
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Configure Web API
            var config = new HttpConfiguration();

            // Configure routes
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Configure JSON formatter
            config.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling =
                Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            config.Formatters.JsonFormatter.SerializerSettings.ContractResolver =
                new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();

            // CORS
            app.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);

            // JWT Authentication
            ConfigureAuth(app);

            // Custom Middleware
            app.Use<GlobalExceptionMiddleware>();
            app.Use<MetricsMiddleware>();
            app.Use<GatewayProtectionMiddleware>();
            app.Use<JwtValidationMiddleware>();

            // Web API
            app.UseWebApi(config);

            // Ocelot (must be last)
            app.UseOcelot().Wait();
        }

        private void ConfigureAuth(IAppBuilder app)
        {
            var issuer = System.Configuration.ConfigurationManager.AppSettings["jwt:Issuer"];
            var audience = System.Configuration.ConfigurationManager.AppSettings["jwt:Audience"];
            var secret = System.Configuration.ConfigurationManager.AppSettings["jwt:Secret"];

            app.UseJwtBearerAuthentication(new Microsoft.Owin.Security.Jwt.JwtBearerAuthenticationOptions
            {
                AuthenticationMode = Microsoft.Owin.Security.AuthenticationMode.Active,
                AllowedAudiences = new[] { audience },
                IssuerSecurityKeyProviders = new[]
                {
                    new Microsoft.Owin.Security.Jwt.SymmetricKeyIssuerSecurityKeyProvider(
                        issuer,
                        System.Text.Encoding.UTF8.GetBytes(secret)
                    )
                }
            });
        }
    }
}
