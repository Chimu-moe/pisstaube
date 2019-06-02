using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using dotenv.net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using opi.v1;
using osu.Framework.Platform;
using Pisstaube.Database;
using Sentry;

namespace Pisstaube
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DotEnv.Config();
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseSentry(Environment.GetEnvironmentVariable("SENTRY_DNS"))
                .UseKestrel(opt =>
                {
                    opt.Limits.MaxRequestBodySize = null;
                })
                .UseStartup<Startup>();
    }
}