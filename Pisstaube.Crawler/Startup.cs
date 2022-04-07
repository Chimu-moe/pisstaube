using System;
using System.Threading;
using Autofac;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Online.API;
using Pisstaube.Allocation;
using Pisstaube.CacheDb;
using Pisstaube.Core.Database;
using Pisstaube.Core.Engine;
using Pisstaube.Core.Utils;
using Pisstaube.Crawler.Crawlers;
using Pisstaube.Crawler.Online;
using Pisstaube.Online;
using Pisstaube.Utils;
using Pomelo.EntityFrameworkCore.MySql.Storage;
using StackExchange.Redis;
using ServerType = Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType;

namespace Pisstaube.Crawler
{
    public class Startup : IStartable
    {
        private readonly Storage _dataStorage = new NativeStorage("data");
        private readonly DatabaseContextFactory _osuContextFactory;
        
        public Startup()
        {
            _osuContextFactory = new DatabaseContextFactory(_dataStorage);
        }
        
        public void ConfigureServices(ContainerBuilder builder)
        {
            var dbOptionsBuilder = new DbContextOptionsBuilder();
            {
                var host = Environment.GetEnvironmentVariable("MARIADB_HOST");
                var port = Environment.GetEnvironmentVariable("MARIADB_PORT");
                var username = Environment.GetEnvironmentVariable("MARIADB_USERNAME");
                var password = Environment.GetEnvironmentVariable("MARIADB_PASSWORD");
                var db = Environment.GetEnvironmentVariable("MARIADB_DATABASE");

                dbOptionsBuilder.UseMySql(
                    $"Server={host};Database={db};User={username};Password={password};Port={port};CharSet=utf8mb4;SslMode=none;",
                    mysqlOptions =>
                    {
                        mysqlOptions.ServerVersion(new Version(10, 4, 12), ServerType.MariaDb);
                        mysqlOptions.CharSet(CharSet.Utf8Mb4);
                    }
                );
            }
            var pool = new DbContextPool<PisstaubeDbContext>(dbOptionsBuilder.Options);
            builder.RegisterInstance(pool).AsSelf();

            var redisOptions = new RedisCacheOptions();
            {
                var host = Environment.GetEnvironmentVariable("REDIS_HOST");
                var port = Environment.GetEnvironmentVariable("REDIS_PORT");
                var pass = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
                var database = Environment.GetEnvironmentVariable("REDIS_DATABASE");
                
                var connString = $"{host}";
                if (!string.IsNullOrEmpty(port))
                    connString += $":{port}";
                if (!string.IsNullOrEmpty(pass))
                    connString += $",password={pass}";
                if (!string.IsNullOrEmpty(database))
                    connString += $",defaultDatabase={database}";
                
                redisOptions.Configuration = connString;
            }
            var redisCache = new RedisCache(redisOptions);
            builder.RegisterInstance(redisCache).As<IDistributedCache>();
            builder.RegisterInstance(ConnectionMultiplexer.Connect(redisOptions.Configuration));

            builder.RegisterType<IpfsCache>().SingleInstance();
            
            builder.RegisterInstance(new RequestLimiter(1200, TimeSpan.FromMinutes(1)));
            builder.RegisterInstance(_dataStorage).As<Storage>();
            builder.RegisterInstance(_osuContextFactory).As<IDatabaseContextFactory>();
            builder.RegisterType<FileStore>();
            builder.RegisterType<PisstaubeCacheDbContextFactory>().AsSelf();
            builder.RegisterType<SetDownloader>().AsSelf();

            builder.RegisterType<MeiliBeatmapSearchEngine>().As<IBeatmapSearchEngineProvider>();
            builder.RegisterType<BeatmapDownloader>();

            builder.RegisterType<OsuConfigManager>();
            builder.RegisterType<APIAccess>().As<IAPIProvider>().SingleInstance();

            builder.RegisterType<OsuCrawler>();
            builder.RegisterType<DatabaseHouseKeeper>();
        }
        
        [UsedImplicitly]
        public void Configure(IAPIProvider apiProvider,
            PisstaubeCacheDbContextFactory cacheDbContextFactory, IBeatmapSearchEngineProvider searchEngine,
            PisstaubeDbContext dbContext)
        {
            Logger.Enabled = true;
            Logger.Level = LogLevel.Debug;
            Logger.GameIdentifier = "Pisstaube";
            Logger.Storage = _dataStorage.GetStorageForDirectory("logs");

            dbContext.Database.Migrate();
            
            while (!searchEngine.IsConnected)
            {
                Logger.LogPrint("Search Engine is not yet Connected!", LoggingTarget.Database, LogLevel.Important);
                Thread.Sleep(1000);
            }

            cacheDbContextFactory.Get().Migrate();
            _osuContextFactory.Get().Migrate();

            JsonUtil.Initialize();

            apiProvider.Login(Environment.GetEnvironmentVariable("OSU_USERNAME"),
                Environment.GetEnvironmentVariable("OSU_PASSWORD"));

            while (true)
            {
                if (!apiProvider.IsLoggedIn)
                {
                    Logger.LogPrint("Not Logged in yet...");
                    Thread.Sleep(1000);
                    continue;
                }
                if (apiProvider.State == APIState.Failing)
                {   
                    Logger.LogPrint($"Failed to Login using Username {Environment.GetEnvironmentVariable("OSU_USERNAME")}", LoggingTarget.Network, LogLevel.Error);
                    Environment.Exit(1);
                }

                break;
            }
        }

        public void Start()
        {
            throw new NotImplementedException();
        }
    }
}