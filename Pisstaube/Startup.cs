﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Online.API;
using Pisstaube.CacheDb;
using Pisstaube.Database;
using Pisstaube.Utils;

namespace Pisstaube
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //Logger.Enabled = false;
            
            services
                .AddDbContext<PisstaubeDbContext>();

            services
                .AddSingleton<PisstaubeDbContext>()
                .AddSingleton<BeatmapSearchEngine>()
                .AddSingleton<Storage>(new NativeStorage("data"))
                .AddSingleton<PisstaubeCacheDbContext>()
                .AddSingleton<OsuConfigManager>()
                .AddSingleton<APIAccess>()
                .AddSingleton<Cleaner>()
                .AddSingleton<Crawler.Crawler>();
            
            services
                .AddMvc(options =>
                {
                    options.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            
            services.AddRouting();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, Crawler.Crawler crawler, APIAccess apiv2)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseHsts();

            apiv2.Login(Environment.GetEnvironmentVariable("OSU_EMAIL"), Environment.GetEnvironmentVariable("OSU_PASSWORD"));

            if (Environment.GetEnvironmentVariable("CRAWLER_DISABLED") != "true")
                crawler.BeginCrawling();

            app.UseHttpsRedirection();
            app.UseMvc(routes =>
            {
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}