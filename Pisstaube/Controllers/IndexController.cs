using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using Pisstaube.CacheDb;
using Pisstaube.CacheDb.Models;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Utils;
using StatsdClient;

namespace Pisstaube.Controllers
{
    [Route("")] 
    [ApiController]
    public class IndexController : ControllerBase
    {
        private readonly APIAccess _apiAccess;
        private readonly Cleaner _cleaner;
        private readonly Storage _storage;
        private readonly Storage _fileStorage;
        private readonly PisstaubeDbContextFactory _contextFactory;
        private readonly PisstaubeCacheDbContextFactory _cache;
        
        public IndexController(APIAccess apiAccess,
            Cleaner cleaner,
            Storage storage,
            PisstaubeCacheDbContextFactory cache,
            PisstaubeDbContextFactory contextFactory)
        {
            _apiAccess = apiAccess;
            _cleaner = cleaner;
            _storage = storage;
            _cache = cache;
            _contextFactory = contextFactory;
            _fileStorage = storage.GetStorageForDirectory("files");
        }
        
        // GET /
        [HttpGet]
        public ActionResult Get()
        {
            var f = _storage.GetStream("pisse.html", FileAccess.ReadWrite);
                
            if (f.Length == 0) {
                f.Write(Encoding.UTF8.GetBytes("Running Pisstaube, a fuck off of cheesegull Written by Mempler available on Github under MIT License!"));
                f.Flush();
                f.Position = 0;
            }

            return Ok(f);
        }

        // GET /osu/:beatmapId
        [HttpGet("osu/{beatmapId:int}")]
        public ActionResult GetBeatmap(int beatmapId)
        {
            var hash = _cache.Get().CacheBeatmaps.Where(bm => bm.BeatmapId == beatmapId).Select(bm => bm.Hash).FirstOrDefault();

            if (hash == null)
                return NotFound("not found");
            
            var info = new osu.Game.IO.FileInfo { Hash = hash };
            
            return File(_fileStorage.GetStream(info.StoragePath), "application/octet-stream", hash);
        }
        
        // GET /osu/:fileMd5
        [HttpGet("osu/{fileMd5}")]
        public ActionResult GetBeatmap(string fileMd5)
        {
            var hash = _cache.Get().CacheBeatmaps.Where(bm => bm.FileMd5 == fileMd5).Select(bm => bm.Hash).FirstOrDefault();

            if (hash == null)
                return NotFound("not found");
            
            var info = new osu.Game.IO.FileInfo { Hash = hash };
            
            return File(_fileStorage.GetStream(info.StoragePath), "application/octet-stream", hash);
        }
        
        // GET /d/:SetId
        [HttpGet("d/{beatmapSetId:int}")]
        public ActionResult GetSet(int beatmapSetId)
        {
            if (_apiAccess.State == APIState.Offline) {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);
                
                return StatusCode(503, "Osu! API is not available.");
            }

            if (!_storage.ExistsDirectory("cache"))
                _storage.GetFullPath("cache", true);

            BeatmapSet set;
            if ((set = _contextFactory.Get().BeatmapSet.FirstOrDefault(bmset => bmset.SetId == beatmapSetId)) == null)
                return NotFound("Set not found");
            
            var cacheStorage = _storage.GetStorageForDirectory("cache");
            var bmFileId = beatmapSetId.ToString("x8");
            CacheBeatmapSet cachedMap;
            
            if (!cacheStorage.Exists(bmFileId)) {
                if (!_cleaner.FreeStorage())
                {
                    Logger.Error(new Exception("Cache Storage is full!"),
                        "Please change the Cleaner Settings!",
                        LoggingTarget.Database);
                
                    return StatusCode(500, "Storage full");
                }
                
                var req = new DownloadBeatmapSetRequest(new BeatmapSetInfo {OnlineBeatmapSetID = beatmapSetId}, true);
                // Video download is not supported, to much traffic. almost no one download videos anyways!
                
                var tmpFile = string.Empty;
                req.Success += c => tmpFile = c;
                req.Perform(_apiAccess);
                
                using (var f = cacheStorage.GetStream(bmFileId, FileAccess.Write)) 
                using (var readStream = System.IO.File.OpenRead(tmpFile))
                    readStream.CopyTo(f);
                
                _cleaner.IncreaseSize(new FileInfo(tmpFile).Length);
                System.IO.File.Delete(tmpFile);

                using (var db = _cache.GetForWrite())
                {
                    if ((cachedMap = db.Context.CacheBeatmapSet.FirstOrDefault(cbm => cbm.SetId == set.SetId)) == null)
                    {
                        db.Context.CacheBeatmapSet.Add(new CacheBeatmapSet
                        {
                            SetId = set.SetId,
                            DownloadCount = 1,
                            LastDownload = DateTime.Now
                        });
                    }
                    else
                    {
                        cachedMap.DownloadCount++;
                        cachedMap.LastDownload = DateTime.Now;
                        db.Context.CacheBeatmapSet.Update(cachedMap);
                    }
                }


                DogStatsd.Increment("beatmap.downloads");
                
                return File(cacheStorage.GetStream(beatmapSetId.ToString("x8")),
                    "application/octet-stream",
                    $"{set.SetId} {set.Artist} - {set.Title}.osz");
            }

            using (var db = _cache.GetForWrite())
            {
                if ((cachedMap = db.Context.CacheBeatmapSet.FirstOrDefault(cbm => cbm.SetId == set.SetId)) == null)
                {
                    db.Context.CacheBeatmapSet.Add(new CacheBeatmapSet
                    {
                        SetId = set.SetId,
                        DownloadCount = 1,
                        LastDownload = DateTime.Now
                    });
                }
                else
                {
                    cachedMap.DownloadCount++;
                    cachedMap.LastDownload = DateTime.Now;
                    db.Context.CacheBeatmapSet.Update(cachedMap);
                }
            }

            DogStatsd.Increment("beatmap.downloads");

            return File(cacheStorage.GetStream(beatmapSetId.ToString("x8")),
                "application/octet-stream",
                $"{set.SetId} {set.Artist} - {set.Title}.osz");
        }
    }
}