using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.AspNetCore.Mvc;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.IO;
using osu.Game.Online.API;
using osu.Game.Scoring.Legacy;
using Pisstaube.CacheDb;
using Pisstaube.Core.Database;
using Pisstaube.Core.Utils;
using Pisstaube.Online;
using Pisstaube.Utils;
using StatsdClient;

namespace Pisstaube.Controllers
{
    [Route("")]
    [ApiController]
    public class IndexController : ControllerBase
    {
        private readonly IAPIProvider _apiProvider;
        private readonly Storage _fileStorage;
        private readonly PisstaubeDbContext _dbContext;
        private readonly object _dbContextLock = new();
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly BeatmapDownloader _downloader;
        private readonly SetDownloader _setDownloader;
        private readonly FileStore _fileStore;

        public IndexController(IAPIProvider apiProvider,
            Storage storage,
            PisstaubeCacheDbContextFactory cache,
            BeatmapDownloader downloader,
            SetDownloader setDownloader,
            PisstaubeDbContext dbContext,
            FileStore fileStore)
        {
            _apiProvider = apiProvider;
            _cache = cache;
            _downloader = downloader;
            _setDownloader = setDownloader;
            _dbContext = dbContext;
            _fileStorage = storage.GetStorageForDirectory("files");
            _fileStore = fileStore;
        }
        
        // GET /osu/:beatmapId
        [HttpGet("osu/{beatmapId:int}")]
        public ActionResult GetBeatmap(int beatmapId)
        {
            DogStatsd.Increment("osu.beatmap.download");
            var hash = _cache.Get()
                    .CacheBeatmaps.Where(bm => bm.BeatmapId == beatmapId)
                    .Select(bm => bm.Hash)
                .FirstOrDefault();

            osu.Game.IO.FileInfo info = null;
            if (hash == null)
                lock (_dbContextLock)
                {
                    foreach (var map in _dbContext.Beatmaps.Where(bm => bm.BeatmapId == beatmapId))
                    {
                        var (fileInfo, fileMd5) = _downloader.Download(map);

                        map.FileMd5 = fileMd5;

                        info = fileInfo;
                    }
                }
            else
                info = new osu.Game.IO.FileInfo {Hash = hash};
            
            if (info == null)
                return NotFound("Beatmap not Found!");

            return File(_fileStorage.GetStream(info.StoragePath), "application/octet-stream", hash);
        }

        // GET /osu/:mapFile
        [HttpGet("osu/{mapFile}"),
         HttpGet("b/{mapFile}")]
        public ActionResult GetBeatmap(string mapFile)
        {
            DogStatsd.Increment("osu.beatmap.download");
            var hash = _cache.Get()
                .CacheBeatmaps
                .Where(bm => bm.FileMd5 == mapFile || bm.File == mapFile)
                .Select(bm => bm.Hash)
                .FirstOrDefault();

            osu.Game.IO.FileInfo info = null;
            if (hash == null)
                lock (_dbContextLock)
                {
                    foreach (var map in _dbContext.Beatmaps.Where(bm => bm.FileMd5 == mapFile || bm.File == mapFile))
                    {
                        var (fileInfo, pFileMd5) = _downloader.Download(map);

                        map.FileMd5 = pFileMd5;
                        info = fileInfo;
                    }

                    if (info == null) {
                        var (fileInfo, _) = _downloader.Download(mapFile);

                        info = fileInfo;
                    }
                }
            else
                info = new osu.Game.IO.FileInfo {Hash = hash};

            if (info == null)
                return NotFound("Beatmap not Found!");
            
            return File(_fileStorage.GetStream(info.StoragePath), "application/octet-stream", hash);
        }

        // GET /d/:SetId
        [HttpGet("d/{beatmapSetId:int}"),
         HttpGet("m/{beatmapSetId:int}"),
         HttpGet("s/{beatmapSetId:int}")]
        public ActionResult GetSet(int beatmapSetId, bool ipfs = false)
        {
            DogStatsd.Increment("osu.set.download");
            
            if (_apiProvider.State == APIState.Offline)
            {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                return StatusCode(503, "Osu! API is not available.");
            }

            SetDownloader.DownloadMapResponse r;
            try
            {
                r = _setDownloader.DownloadMap(beatmapSetId, !Request.Query.ContainsKey("novideo"), ipfs);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(503, "Osu! API is not available.");
            }
            catch (LegacyScoreDecoder.BeatmapNotFoundException)
            {
                return StatusCode(404, "Beatmap not Found!");
            }
            catch (IOException)
            {
                return StatusCode(500, "Storage Full!");
            }
            catch (NotSupportedException)
            {
                return StatusCode(404, "Beatmap got DMCA'd!");
            }
            
            if (ipfs && r.IpfsHash != "")
                return Ok(JsonUtil.Serialize(new
                {
                    Name = r.File,
                    Hash = r.IpfsHash
                }));
            
            if (r.FileStream != null)
                return File (r.FileStream,
                    "application/octet-stream",
                    r.File);

            return Ok("Failed to open stream!");
        }
        
        /*
         * People started to Reverse proxy /d/* to this Server, so we should take advantage of that and give the osu! Client a NoVideo option
         * WITHOUT ?novideo as the osu!client handles downloads like /d/{id}{novid ? n : ""}?us=.....&ha=.....
         */
        // GET /d/:SetId
        [HttpGet("d/{beatmapSetId:int}n"),
         HttpGet("m/{beatmapSetId:int}n"),
         HttpGet("s/{beatmapSetId:int}n")]
        public ActionResult GetSetNoVid(int beatmapSetId, bool ipfs = false)
        {
            DogStatsd.Increment("osu.set.download.no_video");
            
            if (_apiProvider.State == APIState.Offline)
            {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                return StatusCode(503, "Osu! API is not available.");
            }

            SetDownloader.DownloadMapResponse r;
            try
            {
                r = _setDownloader.DownloadMap(beatmapSetId, false, ipfs);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(503, "Osu! API is not available.");
            }
            catch (LegacyScoreDecoder.BeatmapNotFoundException)
            {
                return StatusCode(404, "Beatmap not Found!");
            }
            catch (IOException)
            {
                return StatusCode(500, "Storage Full!");
            }
            catch (NotSupportedException)
            {
                return StatusCode(404, "Beatmap got DMCA'd!");
            }
            
            if (ipfs && r.IpfsHash != "")
                return Ok(JsonUtil.Serialize(new
                {
                    Name = r.File,
                    Hash = r.IpfsHash
                }));
            
            if (r.FileStream != null)
                return File (r.FileStream,
                    "application/octet-stream",
                    r.File);

            return Ok("Failed to open stream!");
        }

        [HttpGet("a/{beatmapId}")]
        public ActionResult GetMapAudio(int beatmapId)
        { 
            int setId;
            string beatmapFileName;
            lock (_dbContextLock)
            {
                var bm = _dbContext
                    .Beatmaps
                    .FirstOrDefault(bm => bm.BeatmapId == beatmapId);
                
                setId = bm?.ParentSetId ?? -1;
                beatmapFileName = bm?.File;
            }
            
            var hash = _cache.Get()
                .CacheBeatmaps.Where(bm => bm.BeatmapId == beatmapId)
                .Select(bm => bm.Hash)
                .FirstOrDefault();


            if (setId == -1)
                return NotFound("Beatmap doesn't exists!");

            osu.Game.IO.FileInfo info = null;
            // Make sure that our file exists
            if (beatmapFileName == null)
            {
                lock (_dbContextLock)
                {
                    foreach (var map in _dbContext.Beatmaps.Where(bm => bm.File == beatmapFileName))
                    {
                        var (fileInfo, pFileMd5) = _downloader.Download(map);

                        map.FileMd5 = pFileMd5;
                        info = fileInfo;
                    }

                    if (info == null) {
                        var (fileInfo, _) = _downloader.Download(beatmapId.ToString());

                        info = fileInfo;
                    }
                }
            }
            else
                info = new osu.Game.IO.FileInfo {Hash = hash};


            if (info == null)
                return NotFound("Beatmap not Found!");
            
            var beatmapExtractor = new BeatmapExtractor(setId);
            
            return Ok(beatmapExtractor.GrabAudio(_fileStorage.GetStream(info.StoragePath)));
        }

        [HttpGet("i/{beatmapId}")]
        public ActionResult GetMapThumbnail(int beatmapId)
        {
            int setId;
            string beatmapFileName;
            lock (_dbContextLock)
            {
                var bm = _dbContext
                    .Beatmaps
                    .FirstOrDefault(bm => bm.BeatmapId == beatmapId);
                
                setId = bm?.ParentSetId ?? -1;
                beatmapFileName = bm?.File;
            }
            
            var hash = _cache.Get()
                .CacheBeatmaps.Where(bm => bm.BeatmapId == beatmapId)
                .Select(bm => bm.Hash)
                .FirstOrDefault();


            if (setId == -1)
                return NotFound("Beatmap doesn't exists!");

            osu.Game.IO.FileInfo info = null;
            // Make sure that our file exists
            if (beatmapFileName == null)
            {
                lock (_dbContextLock)
                {
                    foreach (var map in _dbContext.Beatmaps.Where(bm => bm.File == beatmapFileName))
                    {
                        var (fileInfo, pFileMd5) = _downloader.Download(map);

                        map.FileMd5 = pFileMd5;
                        info = fileInfo;
                    }

                    if (info == null) {
                        var (fileInfo, _) = _downloader.Download(beatmapId.ToString());

                        info = fileInfo;
                    }
                }
            }
            else
                info = new osu.Game.IO.FileInfo {Hash = hash};


            if (info == null)
                return NotFound("Beatmap not Found!");
            
            var beatmapExtractor = new BeatmapExtractor(setId);
            
            return Ok(beatmapExtractor.GrabThumbnail(_fileStorage.GetStream(info.StoragePath)));
        }
        
        [HttpPost("archive")]
        public ActionResult PostSetsArchive()
        {
            var memory = new MemoryStream();
            var archive = new ZipOutputStream(memory);
            
            archive.SetLevel(0);

            var setIds = Request.Form["set_ids"];
            foreach (var setIdS in setIds)
            {
                if (!int.TryParse(setIdS, out var setId))
                    continue;
                
                SetDownloader.DownloadMapResponse r;
                try
                {
                    r = _setDownloader.DownloadMap(setId);
                }
                catch (UnauthorizedAccessException)
                {
                    return StatusCode(503, "Osu! API is not available.");
                }
                catch (LegacyScoreDecoder.BeatmapNotFoundException)
                {
                    return StatusCode(404, "Beatmap not Found!");
                }
                catch (IOException)
                {
                    return StatusCode(500, "Storage Full!");
                }
                catch (NotSupportedException)
                {
                    return StatusCode(404, "Beatmap got DMCA'd!");
                }

                var entry = new ZipEntry(setId + ".osz")
                {
                    DateTime = DateTime.UtcNow,
                    Size = r.FileStream.Length
                };

                archive.PutNextEntry(entry);
                r.FileStream.CopyTo(archive);
                archive.CloseEntry();
            }
            
            archive.Finish();
            memory.Position = 0;
            
            // TODO: Cache the result

            return File(memory, "application/zip", $"osu!BeatmapMirror-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.zip");
        }
    }
}
