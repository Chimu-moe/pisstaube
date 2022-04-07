using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Extensions.DependencyInjection;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using dotenv.net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using osu.Framework.Logging;

namespace Pisstaube
{
    internal static class Program
    {
        private static readonly CancellationTokenSource Cts = new();
        
        private static async Task Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("IS_CONTAINER") != "true")
                DotEnv.Config();
            
            var settings = TracerSettings.FromDefaultSources();
            
            settings.ServiceName = "Pisstaube";
            settings.AgentUri = new Uri($"http://{Environment.GetEnvironmentVariable("DD_AGENT_HOST")}:{Environment.GetEnvironmentVariable("DD_DOGSTATSD_PORT")}/");
            
            settings.Integrations["AdoNet"].Enabled = false;

            var tracer = new Tracer(settings);
            
            Tracer.Instance = tracer;


            if (!Directory.Exists("./data"))
                Directory.CreateDirectory("data");

            var host = WebHost.CreateDefaultBuilder(args)
                .UseKestrel(opt =>
                {
                    opt.Limits.MaxRequestBodySize = null;
                    opt.Listen(IPAddress.Any, 5000);
                })
                .ConfigureServices(services => services.AddAutofac())
                .UseContentRoot(Path.Join(Directory.GetCurrentDirectory(), "data"))
                .UseStartup<Startup>()
                .UseShutdownTimeout(TimeSpan.FromSeconds(5))
                .UseSentry(Environment.GetEnvironmentVariable("SENTRY_DNS"))
                .Build();

            await host.RunAsync(Cts.Token);
        }
        
        private static void OnProcessExit(object sender, EventArgs e)
        {
            Logger.LogPrint("Killing everything..", LoggingTarget.Information, LogLevel.Important);
            Cts.Cancel();
        }
    }
}