using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using osu.Framework.Logging;
using Pisstaube.Core.Database;
using Pisstaube.Core.Engine;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Pomelo.EntityFrameworkCore.MySql.Storage;

namespace Pisstaube.DatabaseRotator
{
    // a little tool to rotate the upstream database with ElasticSearch
    internal static class Program
    {
        private static PisstaubeDbContext _dbContext;
        private static MeiliBeatmapSearchEngine _searchEngine = new(_dbContext);
        
        private static async Task Main(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            var host = Environment.GetEnvironmentVariable("MARIADB_HOST");
            var port = Environment.GetEnvironmentVariable("MARIADB_PORT");
            var username = Environment.GetEnvironmentVariable("MARIADB_USERNAME");
            var password = Environment.GetEnvironmentVariable("MARIADB_PASSWORD");
            var db = Environment.GetEnvironmentVariable("MARIADB_DATABASE");
            
            optionsBuilder.UseMySql(
                $"Server={host};Database={db};User={username};Password={password};Port={port};CharSet=utf8mb4;SslMode=none;",
                mysqlOptions =>
                {
                    mysqlOptions.ServerVersion(new Version(10, 4, 12), ServerType.MariaDb);
                    mysqlOptions.CharSet(CharSet.Utf8Mb4);
                }
            );

            _dbContext = new PisstaubeDbContext(optionsBuilder.Options);
            Logger.Level = LogLevel.Debug;
            
            while (!_searchEngine.IsConnected)
            {
                Logger.LogPrint("Search Engine is not yet Connected!", LoggingTarget.Database, LogLevel.Important);
                Thread.Sleep(1000);
            }
            
            Logger.LogPrint("Fetching all beatmap sets...");
            var beatmapSets = _dbContext.BeatmapSet
                .Include(o => o.ChildrenBeatmaps);
            Logger.LogPrint($"{await beatmapSets.CountAsync()} Beatmap sets to index.", LoggingTarget.Database);
            
            await _searchEngine.Index(beatmapSets);
        }
    }
}