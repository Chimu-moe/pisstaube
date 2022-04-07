using System.IO;
using System.Linq;
using osu.Framework.Extensions;
using osu.Framework.IO.Network;
using osu.Framework.Platform;
using osu.Game.IO;
using Pisstaube.CacheDb;
using Pisstaube.CacheDb.Models;
using Pisstaube.Core.Database;
using Pisstaube.Core.Database.Models;
using Pisstaube.Utils;
using FileInfo = osu.Game.IO.FileInfo;

namespace Pisstaube.Online
{
    public class BeatmapDownloader
    {
        private readonly FileStore _store;
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly RequestLimiter _limiter;
        private readonly Storage _tmpStorage;
        private readonly PisstaubeDbContext _dbContext;

        public BeatmapDownloader(FileStore store, Storage dataStorage, PisstaubeCacheDbContextFactory cache,
            RequestLimiter limiter, PisstaubeDbContext dbContext)
        {
            _store = store;
            _cache = cache;
            _limiter = limiter;
            _tmpStorage = dataStorage.GetStorageForDirectory("tmp");
            _dbContext = dbContext;
        }

        public (FileInfo, string) Download(ChildrenBeatmap beatmap)
        {
            var req = new FileWebRequest(
                _tmpStorage.GetFullPath(beatmap.BeatmapId.ToString(), true),
                $"https://osu.ppy.sh/osu/{beatmap.BeatmapId}");

            _limiter.Limit();

            req.Perform();

            string fileMd5;
            FileInfo info;
            using (var f = _tmpStorage.GetStream(beatmap.BeatmapId.ToString(), FileAccess.Read, FileMode.Open))
            {
                using var db = _cache.GetForWrite();
                info = _store.Add(f);

                fileMd5 = f.ComputeMD5Hash();
                if (db.Context.CacheBeatmaps.Any(bm => bm.BeatmapId == beatmap.BeatmapId))
                    db.Context.CacheBeatmaps.Update(new Beatmap
                        {BeatmapId = beatmap.BeatmapId, Hash = info.Hash, FileMd5 = fileMd5});
                else
                    db.Context.CacheBeatmaps.Add(new Beatmap
                        {BeatmapId = beatmap.BeatmapId, Hash = info.Hash, FileMd5 = fileMd5});
            }

            _tmpStorage.Delete(beatmap.BeatmapId.ToString());

            return (info, fileMd5);
        }

        public (FileInfo, string) Download(string beatmap)
        {
            var req = new FileWebRequest(
                _tmpStorage.GetFullPath(beatmap, true),
                $"https://osu.ppy.sh/osu/{beatmap}");

            _limiter.Limit();

            req.Perform();

            string fileMd5;
            FileInfo info;
            using (var f = _tmpStorage.GetStream(beatmap, FileAccess.Read, FileMode.Open))
            {
                using var db = _cache.GetForWrite();
                info = _store.Add(f);

                fileMd5 = f.ComputeMD5Hash();
                var beatmapId = _dbContext.Beatmaps.Where(i => i.FileMd5 == fileMd5).Select(i => i.BeatmapId).FirstOrDefault();
                if (db.Context.CacheBeatmaps.Any(bm => bm.File == beatmap))
                    db.Context.CacheBeatmaps.Update(new Beatmap
                        {BeatmapId = beatmapId, Hash = info.Hash, FileMd5 = fileMd5});
                else
                    db.Context.CacheBeatmaps.Add(new Beatmap
                        {BeatmapId = beatmapId, Hash = info.Hash, FileMd5 = fileMd5});
            }

            _tmpStorage.Delete(beatmap.ToString());

            return (info, fileMd5);
        }
    }
}