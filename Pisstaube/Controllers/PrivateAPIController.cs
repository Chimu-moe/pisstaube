using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Pisstaube.CacheDb;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Utils;

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
                using (var dumpStream = tmpStorage.GetStream("dump.piss", FileAccess.Write))
                {
                    dumpStream.Write(BitConverter.GetBytes(db.BeatmapSet.Count()));
                    foreach (var bmSet in db.BeatmapSet)
                    {
                        bmSet.ChildrenBeatmaps = db.Beatmaps.Where(bm => bm.ParentSetId == bmSet.SetId).ToList();
                        LZ4MessagePackSerializer.Serialize(dumpStream, bmSet);
                        dumpStream.Flush();
                    }
                }
                return File(tmpStorage.GetStream("dump.piss"),
                    "application/octet-stream",
                    "dump.piss");
            }
        }
        
        // GET /api/pisstaube/put?key={}
        [HttpPut("put")]
        public ActionResult DumpDatabase(
            [FromServices] PisstaubeDbContext db,
            [FromServices] Storage storage,
            [FromQuery] string key
        )
        {
            if (key != Environment.GetEnvironmentVariable("PRIVATE_API_KEY"))
                return Unauthorized("Key is wrong!");
            
            lock (_lock) {
                var f = Request.Form.Files["dump.piss"];
                
                using (var stream = f.OpenReadStream())
                {
                    var b = new byte[4];
                    
                    stream.Read(b);
                    var setCount = BitConverter.ToInt32(b);

                    for (var i = 0; i < setCount; i++)
                    {
                        db.BeatmapSet.Add(LZ4MessagePackSerializer.Deserialize<BeatmapSet>(stream));
                        db.SaveChanges();
                    }
                }
                
                return Ok("Success!");
            }
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