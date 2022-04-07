using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autofac;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using osu.Framework.Logging;
using osu.Game.Online.API;
using Pisstaube.CacheDb;
using Pisstaube.Core.Database;
using Pisstaube.Core.Engine;
using Pisstaube.Core.Events;
using Pisstaube.Core.Utils;
using Pisstaube.Crawler.Crawlers;
using Pisstaube.Crawler.Online;
using Pisstaube.Online;
using Pisstaube.Utils;
using StackExchange.Redis;

#nullable enable

namespace Pisstaube.Crawler
{
    internal static class Program
    {
        
        private static void Main(string[] args)
        {
            var startup = new Startup();
            DbContextPool<PisstaubeDbContext>? dbContextPool = null;
            
            var builder = new ContainerBuilder();
            startup.ConfigureServices(builder);
            
            builder.Register(c =>
            {
                var pool = c.Resolve<DbContextPool<PisstaubeDbContext>>();

                var context = pool.Rent();

                return context;
            }).As<PisstaubeDbContext>()
                .OnRelease(c =>
                {
                    dbContextPool?.Return(c);
                });
            
            builder.Register(c => {
                var contextPool = c.Resolve<DbContextPool<PisstaubeDbContext>>();
                dbContextPool = contextPool;
                
                
                var apiProvider = c.Resolve<IAPIProvider>();
                var cacheDbContextFactory = c.Resolve<PisstaubeCacheDbContextFactory>();
                var searchEngine = c.Resolve<IBeatmapSearchEngineProvider>();
                var dbContext = c.Resolve<PisstaubeDbContext>();

                startup.Configure(apiProvider, cacheDbContextFactory, searchEngine, dbContext);
                
                return startup;
            }).AutoActivate();
            
            var container = builder.Build();

            var osuCrawler = container.Resolve<OsuCrawler>();
            var houseKeeper = container.Resolve<DatabaseHouseKeeper>();
            var redis = container.Resolve<ConnectionMultiplexer>();
            var setDownloader = container.Resolve<SetDownloader>();

            if (Environment.GetEnvironmentVariable("CRAWLER_DISABLED")?.ToLower() != "true")
                osuCrawler.Start();
            
            if (Environment.GetEnvironmentVariable("UPDATER_DISABLED")?.ToLower() != "true")
                houseKeeper.Start();

            var db = redis.GetDatabase(int.Parse(Environment.GetEnvironmentVariable("REDIS_DATABASE") ?? "-1"));
            var sub = redis.GetSubscriber();

            sub.Subscribe($"chimu:downloads", (channel, value) =>
            {
                Logger.LogPrint("Download was requested...");

                Logger.LogPrint(channel);
                Logger.LogPrint(value);
                    
                DownloadMapRequest request = null;
                try {
                    request = JsonUtil.Deserialize<DownloadMapRequest>(value);
                } catch (Exception e) {
                    Logger.Error(e, "Failed to download Beatmap");
                }

                try
                {
                    var response = setDownloader.DownloadMap(int.Parse(request.SetId), request.NoVideo == "0");
                    
                    response._ID = request._ID;

                    db.Publish("chimu:s:downloads", JsonUtil.Serialize(response));
                    
                    Logger.LogPrint("Success");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to download Beatmap");
                }
            });
            
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}