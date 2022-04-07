using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Internal;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using Pisstaube.Core.Database;
using Pisstaube.Core.Engine;
using Pisstaube.Online;
using Pisstaube.Utils;
using Sentry;

namespace Pisstaube.Crawler.Crawlers
{
    public class DatabaseHouseKeeper : OsuCrawler
    {
        private readonly RequestLimiter _requestLimiter;
        private readonly DbContextPool<PisstaubeDbContext> _dbContextPool;
        
        protected override void ThreadWorker()
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                var dbContext = _dbContextPool.Rent();
                try
                {
                    var beatmaps = dbContext.BeatmapSet
                        .Where(x => !x.Disabled)
                        .AsEnumerable()
                        .Where(x => x.LastChecked != null)
                        .Where(
                            x => (
                                         (
                                             (
                                                x.RankedStatus == BeatmapSetOnlineStatus.None       ||
                                                x.RankedStatus == BeatmapSetOnlineStatus.Loved
                                             ) &&
                                             (x.LastChecked.Value + TimeSpan.FromDays(90)).Subtract(DateTime.Now).TotalMilliseconds < 0
                                         ) ||
                                         (
                                             (
                                                 x.RankedStatus == BeatmapSetOnlineStatus.Pending   ||
                                                 x.RankedStatus == BeatmapSetOnlineStatus.Qualified ||
                                                 x.RankedStatus == BeatmapSetOnlineStatus.WIP
                                             ) &&
                                             (x.LastChecked.Value + TimeSpan.FromDays(30)).Subtract(DateTime.Now).TotalMilliseconds < 0
                                         ) ||
                                         (
                                             (
                                                x.RankedStatus == BeatmapSetOnlineStatus.Graveyard  ||
                                                x.RankedStatus == BeatmapSetOnlineStatus.Ranked     ||
                                                x.RankedStatus == BeatmapSetOnlineStatus.Approved
                                             ) &&
                                             (x.LastChecked.Value + TimeSpan.FromDays(365)).Subtract(DateTime.Now).TotalMilliseconds < 0
                                         )
                                     )
                                 
                            && !x.Disabled
                        );
                    
                    foreach (var beatmap in beatmaps)
                    {
                        if (Tasks.Count > 128) {
                            Task.WaitAll(Tasks.ToArray(), CancellationToken); // wait for all tasks                        
                            Tasks.Clear(); // Remove all previous tasks.
                        }
                        
                        _requestLimiter.Limit();
                        
                        Tasks.Add(Crawl(beatmap.SetId));
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "an Unknown error occured during HouseKeeping", LoggingTarget.Database);
                    SentrySdk.CaptureException(e);
                } finally {
                    _dbContextPool.Return(dbContext);
                }
                
                Thread.Sleep(TimeSpan.FromHours(8)); // Update every 8 hours...
            }
        }

        public DatabaseHouseKeeper(
            Storage storage, RequestLimiter requestLimiter,
            IAPIProvider apiProvider, IBeatmapSearchEngineProvider searchEngine,
            BeatmapDownloader beatmapDownloader, DbContextPool<PisstaubeDbContext> dbContextPool) :
            base(storage, requestLimiter, apiProvider, searchEngine, beatmapDownloader, dbContextPool)
        {
            _requestLimiter = requestLimiter;
            _dbContextPool = dbContextPool;
        }
    }
}