using System;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using Owin;

namespace GatewayNet48
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://+:8887/";

            using (WebApp.Start<Startup>(url: baseAddress))
            {
                Console.WriteLine($"Gateway running on {baseAddress}");
                Console.WriteLine("Press Enter to exit...");
                Console.ReadLine();
            }
        }
    }

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            HttpConfiguration config = new HttpConfiguration();

            // Enable CORS
            config.EnableCors(new System.Web.Http.Cors.EnableCorsAttribute("*", "*", "*"));

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            app.UseWebApi(config);

            // Ocelot configuration will be added here
            Console.WriteLine("Gateway initialized");
        }
    }
}
