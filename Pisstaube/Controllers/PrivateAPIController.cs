using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
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
        private static readonly object _lock = new object();
        // GET /api/pisstaube/dump
        [HttpGet("dump")]
        public ActionResult DumpDatabase(
            [FromServices] PisstaubeDbContext db,
            [FromServices] Storage storage
            )
        {
            lock (_lock) {
                var tmpStorage = storage.GetStorageForDirectory("tmp");
                
                if (tmpStorage.Exists("dump.piss"))
                    System.IO.File.Delete(tmpStorage.GetFullPath("dump.piss"));
                
                using (var dumpStream = tmpStorage.GetStream("dump.piss", FileAccess.Write))
                using (var sw = new MStreamWriter(dumpStream))
                {
                    sw.Write(db.BeatmapSet.Count());
                    foreach (var bmSet in db.BeatmapSet)
                    {
                        bmSet.ChildrenBeatmaps = db.Beatmaps.Where(bm => bm.ParentSetId == bmSet.SetId).ToList();
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
            [FromServices] PisstaubeDbContext db,
            [FromServices] BeatmapSearchEngine searchEngine,
            [FromQuery] string key,
            [FromQuery] bool drop
        )
        {
            if (key != Environment.GetEnvironmentVariable("PRIVATE_API_KEY"))
                return Unauthorized("Key is wrong!");

            if (drop)
            {
                searchEngine.DeleteAllBeatmaps();
                db.Database.ExecuteSqlCommand("SET FOREIGN_KEY_CHECKS = 0;" +
                                              "TRUNCATE TABLE `Beatmaps`;" +
                                              "ALTER TABLE `Beatmaps` AUTO_INCREMENT = 1;" +
                                              "TRUNCATE TABLE `BeatmapSet`;" +
                                              "ALTER TABLE `BeatmapSet` AUTO_INCREMENT = 1;" +
                                              "SET FOREIGN_KEY_CHECKS = 1;");
            }

            lock (_lock) {
                var f = Request.Form.Files["dump.piss"];
                
                using (var stream = f.OpenReadStream())
                using (var sr = new MStreamReader(stream))
                {
                    var count = sr.ReadInt32();
                    Logger.LogPrint($"Count: {count}");

                    for (var i = 0; i < count; i++)
                    {
                        var set = sr.ReadData<BeatmapSet>();
                        
                        Logger.LogPrint($"Importing BeatmapSet {set.SetId} {set.Artist} - {set.Title} ({set.Creator}) of Index {i}", LoggingTarget.Database, LogLevel.Important);
                        if (db.BeatmapSet.Any(s => s.SetId == set.SetId)) {
                            db.BeatmapSet.Update(set);
                        } else {
                            db.BeatmapSet.Add(set);
                        }
                    }
                    
                    db.SaveChanges();
                    
                    /*
                    
                    var b = new byte[4];
                    
                    stream.Read(b);
                    var setCount = BitConverter.ToInt32(b);

                    var bf = new BinaryFormatter();
                    for (var i = 0; i < setCount; i++)
                    {
                        var set = (BeatmapSet) bf.Deserialize(stream);
                        
                        Logger.LogPrint($"Importing BeatmapSet {set.SetId} {set.Artist} - {set.Title} ({set.Creator}) of Index {i}", LoggingTarget.Database, LogLevel.Important);
                        if (db.BeatmapSet.Any(s => s.SetId == set.SetId)) {
                            db.BeatmapSet.Update(set);
                        } else {
                            db.BeatmapSet.Add(set);
                        }
                        searchEngine.IndexBeatmap(set);
                        db.SaveChanges();
                    }
                    
                    */
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
        public ActionResult GetPisstaubeStats(
            [FromServices] Crawler crawler,
            [FromServices] Cleaner cleaner,
            [FromServices] PisstaubeDbContext context
        )
        {
            if (!crawler.IsCrawling && cInt == 0)
                cInt = context.BeatmapSet.LastOrDefault()?.SetId + 1 ?? 0;
            
            return Ok(new PisstaubeStats
            {
                IsCrawling = crawler.IsCrawling,
                LatestCrawledId = crawler.IsCrawling ? crawler.LatestId : cInt,
                MaxStorage = cleaner.MaxSize,
                StorageUsed = (ulong) cleaner.DataDirectorySize,
                StorageUsagePercent = MathF.Round ((ulong)cleaner.DataDirectorySize / cleaner.MaxSize * 100, 2)
            });
        }


        [HttpGet("recovery")]
        public ActionResult Recovery(
            [FromServices] PisstaubeDbContext db,
            [FromServices] BeatmapSearchEngine searchEngine,
            [FromServices] Crawler crawler,
            [FromServices] PisstaubeCacheDbContextFactory _cache,
            [FromQuery] string key,
            [FromQuery] RecoveryAction action
            )
        {
            if (key != Environment.GetEnvironmentVariable("PRIVATE_API_KEY"))
                return Unauthorized("Key is wrong!");

            switch (action)
            {
                case RecoveryAction.RepairElastic:
                    new Thread(() =>
                    {
                        Logger.LogPrint("Repairing ElasticSearch");
                        crawler.Stop();

                        searchEngine.DeleteAllBeatmaps();
                    
                        foreach (var beatmapSet in db.BeatmapSet)
                        {
                            beatmapSet.ChildrenBeatmaps = db.Beatmaps.Where(b => b.ParentSetId == beatmapSet.SetId).ToList();
                            searchEngine.IndexBeatmap(beatmapSet);
                        }
                    
                        if (Environment.GetEnvironmentVariable("CRAWLER_DISABLED") != "true")
                            crawler.BeginCrawling();
                    }).Start();
                    break;
                case RecoveryAction.RecrawlEverything:
                    new Thread(() =>
                    {
                        Logger.LogPrint("Recrawl Everything!");
                    
                        crawler.Stop();
                        searchEngine.DeleteAllBeatmaps();
                        db.Database.ExecuteSqlCommand("SET FOREIGN_KEY_CHECKS = 0;" +
                                                      "TRUNCATE TABLE `Beatmaps`;" +
                                                      "ALTER TABLE `Beatmaps` AUTO_INCREMENT = 1;" +
                                                      "TRUNCATE TABLE `BeatmapSet`;" +
                                                      "ALTER TABLE `BeatmapSet` AUTO_INCREMENT = 1;" +
                                                      "SET FOREIGN_KEY_CHECKS = 1;");

                        using (var cacheDb = _cache.GetForWrite())
                        {
                            cacheDb.Context.Database.ExecuteSqlCommand(
                                "DELETE FROM `CacheBeatmaps`;" +
                                "DELETE FROM `CacheBeatmapSet`;");
                        }
                        crawler.BeginCrawling();
                    }).Start();
                    break;
                case RecoveryAction.RecrawlUnknown:
                    new Thread(() =>
                    {
                        Logger.LogPrint("Recrawl All unknown maps!");
                    
                        crawler.Stop();
                        for (var i = 0; i < db.BeatmapSet.Last().SetId; i++)
                        {
                            if (!db.BeatmapSet.Any(set => set.SetId == i))
                                crawler.Crawl(i, db);
                        }
                        crawler.BeginCrawling();
                    }).Start();
                    break;
                default:
                    return BadRequest("Unknown Action type!");
            }

            return Ok("Success!");
        }
    }
}