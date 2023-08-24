using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace IndentityServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseUrls(new[] { "http://*:5000/" })
            .UseStartup<Startup>();
    }
}
