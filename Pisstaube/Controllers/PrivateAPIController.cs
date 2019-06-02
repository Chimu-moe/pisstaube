using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Pisstaube.CacheDb;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Utils;
using Shared.Helpers;
using Logger = osu.Framework.Logging.Logger;

namespace Pisstaube.Controllers
{
    public enum RecoveryAction
    {
        RepairElastic,
        RecrawlEverything,
        RecrawlUnknown
    }
    
    [Route("api/pisstaube")] 
    [ApiController]
    public class PrivateAPIController : ControllerBase
    {
        private readonly PisstaubeDbContextFactory _contextFactory;
        private readonly Storage _storage;
        private readonly BeatmapSearchEngine _searchEngine;
        private readonly Crawler _crawler;
        private readonly Cleaner _cleaner;
        private readonly PisstaubeCacheDbContextFactory _cache;

        private static readonly object _lock = new object();

        public PrivateAPIController(PisstaubeDbContextFactory contextFactory,
            Storage storage, BeatmapSearchEngine searchEngine, Crawler crawler, Cleaner cleaner,
            PisstaubeCacheDbContextFactory cache)
        {
            _contextFactory = contextFactory;
            _storage = storage;
            _searchEngine = searchEngine;
            _crawler = crawler;
            _cleaner = cleaner;
            _cache = cache;
        }
        
        // GET /api/pisstaube/dump?key={KEY}
        [HttpGet("dump")]
        public ActionResult DumpDatabase(string key)
        {
            if (key != Environment.GetEnvironmentVariable("PRIVATE_API_KEY"))
                return Unauthorized("Key is wrong!");

            lock (_lock) {
                var tmpStorage = _storage.GetStorageForDirectory("tmp");
                
                if (tmpStorage.Exists("dump.piss"))
                    System.IO.File.Delete(tmpStorage.GetFullPath("dump.piss"));
                
                using (var dumpStream = tmpStorage.GetStream("dump.piss", FileAccess.Write))
                using (var sw = new MStreamWriter(dumpStream))
                {
                    sw.Write(_contextFactory.Get().BeatmapSet.Count());
                    foreach (var bmSet in _contextFactory.Get().BeatmapSet)
                    {
                        bmSet.ChildrenBeatmaps = _contextFactory.Get().Beatmaps.Where(bm => bm.ParentSetId == bmSet.SetId).ToList();
                        sw.Write(bmSet);
                    }
                }
                return File(tmpStorage.GetStream("dump.piss"),
                    "application/octet-stream",
                    "dump.piss");
            }
        }
        
        // GET /api/pisstaube/put?key={}
        [HttpPut("put")]
        public ActionResult PutDatabase(
            [FromQuery] string key,
            [FromQuery] bool drop
        )
        {
            if (key != Environment.GetEnvironmentVariable("PRIVATE_API_KEY"))
                return Unauthorized("Key is wrong!");

            if (drop)
            {
                _searchEngine.DeleteAllBeatmaps();
                using (var db = _contextFactory.GetForWrite()) {
                    db.Context.Database.ExecuteSqlCommand
                                             ("SET FOREIGN_KEY_CHECKS = 0;" +
                                              "TRUNCATE TABLE `Beatmaps`;" +
                                              "ALTER TABLE `Beatmaps` AUTO_INCREMENT = 1;" +
                                              "TRUNCATE TABLE `BeatmapSet`;" +
                                              "ALTER TABLE `BeatmapSet` AUTO_INCREMENT = 1;" +
                                              "SET FOREIGN_KEY_CHECKS = 1;");
                }
            }

            lock (_lock) {
                var f = Request.Form.Files["dump.piss"];
                
                using (var stream = f.OpenReadStream())
                using (var sr = new MStreamReader(stream))
                using (var db = _contextFactory.GetForWrite())
                {
                    var count = sr.ReadInt32();
                    Logger.LogPrint($"Count: {count}");

                    for (var i = 0; i < count; i++)
                    {
                        var set = sr.ReadData<BeatmapSet>();
                        
                        Logger.LogPrint($"Importing BeatmapSet {set.SetId} {set.Artist} - {set.Title} ({set.Creator}) of Index {i}", LoggingTarget.Database, LogLevel.Important);

                        if (!drop)
                            if (db.Context.BeatmapSet.Any(s => s.SetId == set.SetId))
                                db.Context.BeatmapSet.Update(set);
                            else
                                db.Context.BeatmapSet.Add(set);
                        else
                            db.Context.BeatmapSet.Add(set);
                       
                    }
                    Logger.LogPrint("Finish importing maps!");
                }
                
                return Ok("Success!");
            }
        }

        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private struct PisstaubeStats
        {
            public int LatestCrawledId;
            public bool IsCrawling;
            public ulong MaxStorage;
            public ulong StorageUsed;
            public float StorageUsagePercent;
        }
        
        
        private int cInt;
        // GET /api/pisstaube/stats
        [HttpGet("stats")]
        public ActionResult GetPisstaubeStats()
        {
            lock (_lock)
                if (!_crawler.IsCrawling && cInt == 0)
                    cInt = _contextFactory.Get().BeatmapSet.LastOrDefault()?.SetId + 1 ?? 0;

            return Ok(new PisstaubeStats
            {
                IsCrawling = _crawler.IsCrawling,
                LatestCrawledId = _crawler.IsCrawling ? _crawler.LatestId : cInt,
                MaxStorage = _cleaner.MaxSize,
                StorageUsed = (ulong) _cleaner.DataDirectorySize,
                StorageUsagePercent = MathF.Round ((ulong)_cleaner.DataDirectorySize / _cleaner.MaxSize * 100, 2)
            });
        }


        [HttpGet("recovery")]
        public ActionResult Recovery(
            [FromQuery] string key,
            [FromQuery] RecoveryAction action
            )
        {
            if (key != Environment.GetEnvironmentVariable("PRIVATE_API_KEY"))
                return Unauthorized("Key is wrong!");

            switch (action)
            {
                case RecoveryAction.RepairElastic:
                    Logger.LogPrint("Repairing ElasticSearch");
                    _crawler.Stop();

                    _searchEngine.DeleteAllBeatmaps();
                    
                    foreach (var beatmapSet in _contextFactory.Get().BeatmapSet)
                    {
                        beatmapSet.ChildrenBeatmaps = _contextFactory.Get().Beatmaps.Where(b => b.ParentSetId == beatmapSet.SetId).ToList();
                        _searchEngine.IndexBeatmap(beatmapSet);
                    }
                    
                    if (Environment.GetEnvironmentVariable("CRAWLER_DISABLED") != "true")
                        _crawler.BeginCrawling();
                    break;
                case RecoveryAction.RecrawlEverything:
                    Logger.LogPrint("Recrawl Everything!");
                    
                    _crawler.Stop();
                    _searchEngine.DeleteAllBeatmaps();
                    using (var db = _contextFactory.GetForWrite()) {
                        db.Context.Database.ExecuteSqlCommand("SET FOREIGN_KEY_CHECKS = 0;" +
                                                      "TRUNCATE TABLE `Beatmaps`;" +
                                                      "ALTER TABLE `Beatmaps` AUTO_INCREMENT = 1;" +
                                                      "TRUNCATE TABLE `BeatmapSet`;" +
                                                      "ALTER TABLE `BeatmapSet` AUTO_INCREMENT = 1;" +
                                                      "SET FOREIGN_KEY_CHECKS = 1;");
                    }
                    using (var cacheDb = _cache.GetForWrite())
                    {
                        cacheDb.Context.Database.ExecuteSqlCommand(
                            "DELETE FROM `CacheBeatmaps`;" +
                            "DELETE FROM `CacheBeatmapSet`;");
                    }
                    _crawler.BeginCrawling();
                    break;
                case RecoveryAction.RecrawlUnknown:
                    Logger.LogPrint("Recrawl All unknown maps!");
                    
                    _crawler.Stop();
                    using (var db = _contextFactory.GetForWrite()) {
                        for (var i = 0; i < db.Context.BeatmapSet.Last().SetId; i++)
                        {
                            if (!db.Context.BeatmapSet.Any(set => set.SetId == i))
                                _crawler.Crawl(i, db.Context);
                        }
                    }
                    _crawler.BeginCrawling();
                    break;
                default:
                    return BadRequest("Unknown Action type!");
            }

            return Ok("Success!");
        }
    }
}