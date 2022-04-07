using System;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Scoring.Legacy;
using Pisstaube.Allocation;
using Pisstaube.CacheDb;
using Pisstaube.CacheDb.Models;
using Pisstaube.Core.Database;
using Pisstaube.Core.Database.Models;
using Pisstaube.Core.Engine;
using Pisstaube.Core.Events;
using Pisstaube.Utils;

namespace Pisstaube.Crawler.Online
{
    public class SetDownloader
    {
        private readonly Storage _storage;
        private readonly IAPIProvider _apiProvider;
        private readonly PisstaubeDbContext _dbContext;
        private readonly object _dbContextMutes = new();
        private readonly PisstaubeCacheDbContextFactory _cacheFactory;
        private readonly RequestLimiter _limiter;
        private readonly IBeatmapSearchEngineProvider _search;
        private readonly IpfsCache _ipfsCache;

        public SetDownloader(Storage storage,
            IAPIProvider apiProvider,
            PisstaubeDbContext dbContext,
            PisstaubeCacheDbContextFactory cacheFactory,
            RequestLimiter limiter,
            IBeatmapSearchEngineProvider search,
            IpfsCache ipfsCache
        )
        {
            _storage = storage;
            _apiProvider = apiProvider;
            _dbContext = dbContext;
            _cacheFactory = cacheFactory;
            _limiter = limiter;
            _search = search;
            _ipfsCache = ipfsCache;
        }

        public DownloadMapResponse DownloadMap(int beatmapSetId, bool dlVideo = false)
        {
            if (_apiProvider.State == APIState.Offline)
            {
                Logger.Error(new NotSupportedException("API is not Authenticated!"),
                    "API is not Authenticated! check your Login Details!",
                    LoggingTarget.Network);

                throw new UnauthorizedAccessException("API Is not Authorized!");
            }

            if (!_storage.ExistsDirectory("cache"))
                _storage.GetFullPath("cache", true);

            BeatmapSet set;
            lock (_dbContextMutes)
            {
                if ((set = _dbContext.BeatmapSet
                        .FirstOrDefault(bmSet => bmSet.SetId == beatmapSetId && !bmSet.Disabled)) == null)
                    throw new LegacyScoreDecoder.BeatmapNotFoundException();

                if (!string.IsNullOrEmpty(set.IpfsHash))
                {
                    return new DownloadMapResponse {
                        File = $"{set.SetId} {set.Artist} - {set.Title}.osz",
                        IpfsHash = set.IpfsHash,
                    };
                }
            }
                
            var cacheStorage = _storage.GetStorageForDirectory("cache");
            var bmFileId = beatmapSetId.ToString("x8") + (dlVideo ? "" : "_novid");

            CacheBeatmapSet cachedMap;
            if (!cacheStorage.Exists(bmFileId))
            {
                var req = new DownloadBeatmapSetRequest(new BeatmapSetInfo {OnlineBeatmapSetID = beatmapSetId},
                    !dlVideo);
                
                var tmpFile = string.Empty;
                req.Success += c => tmpFile = c;
                _limiter.Limit();
                req.Perform(_apiProvider);

                using (var f = cacheStorage.GetStream(bmFileId, FileAccess.Write))
                {
                    using var readStream = File.OpenRead(tmpFile);
                    readStream.CopyTo(f);
                }

                File.Delete(tmpFile);

                using var db = _cacheFactory.GetForWrite();
                if ((cachedMap = db.Context.CacheBeatmapSet.FirstOrDefault(cbm => cbm.SetId == set.SetId)) ==
                    null)
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
                
                var cac = _ipfsCache.CacheFile("data/cache/" + bmFileId);
                
                return new DownloadMapResponse {
                    File = $"{set.SetId} {set.Artist} - {set.Title}.osz",
                    IpfsHash = cac.Result,
                };
            }

            using (var db = _cacheFactory.GetForWrite())
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
            
            var cache = _ipfsCache.CacheFile("data/cache/" + bmFileId);
            
            return new DownloadMapResponse {
                File = $"{set.SetId} {set.Artist} - {set.Title}.osz",
                IpfsHash = cache.Result,
            };
        }
    }
}