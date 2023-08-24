using System;
using System.IO;
using Amazon.CloudWatchLogs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.AwsCloudWatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.Options;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Api.Logging;

namespace Cohere.Api
{
    public class Program
    {
        private static string _environmentName;

        public static void Main(string[] args)
        {
            var webHost = CreateHostBuilder(args).Build();

            //read configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json")
                        .AddJsonFile($"appsettings.{_environmentName}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();

            var sp = webHost.Services;
            var creds = sp.GetService<AWSOptions>().Credentials.GetCredentials();
            var logGroup = sp.GetService<IOptions<LoggingSettings>>().Value;

            var options = new CloudWatchSinkOptions
            {
                LogGroupName = logGroup.CloudWatchLogGroup,
                MinimumLogEventLevel = (LogEventLevel)logGroup.MinimumLogEventLevel,
                TextFormatter = new AWSTextFormatter(),
            };

            // setup AWS CloudWatch client
            var client = new AmazonCloudWatchLogsClient(creds.AccessKey, creds.SecretKey, Amazon.RegionEndpoint.USEast1);

            // Attach the sink to the logger configuration
            Log.Logger = new LoggerConfiguration()
              .WriteTo.AmazonCloudWatch(options, client)
              .CreateLogger();

            //Start webHost
            try
            {
                Log.Information("Starting web host");
                webHost.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureLogging((hostingContext, config) =>
                {
                    config.ClearProviders();  //Disabling default integrated logger
                    _environmentName = hostingContext.HostingEnvironment.EnvironmentName;
                })
            .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>().UseSerilog();
                });

        //private static void SetupLogglyConfiguration(LogglySettings logglySettings)
        //{
        //    //Configure Loggly
        //    var config = LogglyConfig.Instance;
        //    config.CustomerToken = logglySettings.CustomerToken;
        //    config.ApplicationName = logglySettings.ApplicationName;

        //    config.Transport = new TransportConfiguration
        //    {
        //        EndpointHostname = logglySettings.EndpointHostname,
        //        EndpointPort = logglySettings.EndpointPort,
        //        LogTransport = logglySettings.LogTransport
        //    };
        //    config.ThrowExceptions = logglySettings.ThrowExceptions;

        //    //Define Tags sent to Loggly
        //    config.TagConfig.Tags.AddRange(new ITag[]
        //    {
        //        new ApplicationNameTag { Formatter = "Application-{0}" },
        //        new HostnameTag { Formatter = "Host-{0}" }
        //    });
        //}
    }
}