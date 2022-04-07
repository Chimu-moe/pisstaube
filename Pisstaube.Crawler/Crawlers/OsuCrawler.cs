using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Internal;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using Pisstaube.Core.Database;
using Pisstaube.Core.Database.Models;
using Pisstaube.Core.Engine;
using Pisstaube.Online;
using Pisstaube.Utils;
using Sentry;

namespace Pisstaube.Crawler.Crawlers
{
    /// <summary>
    /// an Crawler that Crawls down osu!API V2
    /// </summary>
    public class OsuCrawler : ICrawler, IDisposable
    {
        private readonly Storage _storage;
        private readonly BeatmapDownloader _beatmapDownloader;
        private DbContextPool<PisstaubeDbContext> _dbContextPool { get; }
        private IBeatmapSearchEngineProvider SearchEngine { get; }
        private IAPIProvider ApiProvider { get; }
        private RequestLimiter RequestLimiter { get; }
        
        public int LatestId { get; private set; }
        public bool IsCrawling { get; private set; }
        
        private Thread _workingThread;
        private CancellationTokenSource _cancellationTokenSource;
        
        protected List<Task> Tasks { get; } = new();
        protected CancellationToken CancellationToken { get; private set; }
        
        public OsuCrawler(Storage storage, RequestLimiter requestLimiter, IAPIProvider apiProvider,
            IBeatmapSearchEngineProvider searchEngine, BeatmapDownloader beatmapDownloader, DbContextPool<PisstaubeDbContext> dbContextPool)
        {
            _storage = storage;
            _beatmapDownloader = beatmapDownloader;
            _dbContextPool = dbContextPool;
            SearchEngine = searchEngine;
            ApiProvider = apiProvider;
            RequestLimiter = requestLimiter;
        }

        public void Start()
        {
            if (_workingThread?.IsAlive == true)
                return;

            _workingThread = new Thread(ThreadWorker);
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = _cancellationTokenSource.Token;
            
            _workingThread.Start();
        }

        public void Stop()
        {
            if (_workingThread == null)
                throw new NotSupportedException($"It's not possible to Stop {nameof(OsuCrawler)} before it has even Started!");
            
            _cancellationTokenSource.Cancel();
            _workingThread.Join();
        }

        public void Wait()
        {
            _workingThread.Join();
        }

        private int _errorCount;
        public virtual async Task<bool> Crawl(int id)
        {
            var dbContext = _dbContextPool.Rent();
            Logger.LogPrint($"Crawling BeatmapId {id}...", LoggingTarget.Network, LogLevel.Debug);
            
            try
            {
                var beatmapSetRequest = new GetBeatmapSetRequest(id);
                beatmapSetRequest.Perform(ApiProvider);

                var beatmapSetInfo = beatmapSetRequest.Result;

                if (beatmapSetInfo == null) {
                    _errorCount++;
                    return false;
                }

                var beatmapSet = BeatmapSet.FromBeatmapSetInfo(beatmapSetInfo.ToBeatmapSet());

                if (beatmapSet == null){
                    _errorCount++;
                    return false;
                }

                var cacheStorage = _storage.GetStorageForDirectory("cache");
                
                if (cacheStorage.Exists(beatmapSet.SetId.ToString("x8") + "_novid"))
                    cacheStorage.Delete(beatmapSet.SetId.ToString("x8") + "_novid");
                if (cacheStorage.Exists(beatmapSet.SetId.ToString("x8")))
                    cacheStorage.Delete(beatmapSet.SetId.ToString("x8"));

                foreach (var childrenBeatmap in beatmapSet.ChildrenBeatmaps)
                {
                    var fileInfo = _beatmapDownloader.Download(childrenBeatmap);

                    childrenBeatmap.FileMd5 = fileInfo.Item2;
                }
                    
                dbContext.BeatmapSet.AddOrUpdate(beatmapSet);
  
                await dbContext.SaveChangesAsync();

                SearchEngine.Index(new []{ beatmapSet });

                _errorCount = 0;
                return true;
            }
            catch (WebException) // Don't worry about WebException exceptions, we can safely ignore those.
            {
                _errorCount++;
                return false;
            } 
            catch (Exception e) // Everything else, redo the Crawl.
            {
                Logger.Error(e, "Unknown error during crawling occured!");

                SentrySdk.CaptureException(e);

                if (_errorCount > 1024)
                {
                    Logger.LogPrint("Error count too high! canceling crawl...", LoggingTarget.Network, LogLevel.Important);
                    return false;
                }

                _errorCount++;
                return await Crawl(id);
            } finally {
                _dbContextPool.Return(dbContext);
            }
        }
        
        protected virtual void ThreadWorker()
        {
            var dbContext = _dbContextPool.Rent();
            try {
                LatestId = dbContext.BeatmapSet
                    .OrderByDescending(bs => bs.SetId)
                    .Take(1)
                    .ToList()
                    .FirstOrDefault()?.SetId + 1 ?? 1;

                while (!CancellationToken.IsCancellationRequested)
                {
                    if (_errorCount > 1024)
                    {
                        LatestId = dbContext.BeatmapSet
                            .OrderByDescending(bs => bs.SetId)
                            .Take(1)
                            .ToList()
                            .FirstOrDefault()?.SetId + 1 ?? 1;

                        _dbContextPool.Return(dbContext);

                        Logger.LogPrint("Error count too high! will continue tomorrow...", LoggingTarget.Network, LogLevel.Important);
                        Thread.Sleep(TimeSpan.FromDays(1));

                        dbContext = _dbContextPool.Rent();
                    }
                    if (Tasks.Count > 32) {
                        Task.WaitAll(Tasks.ToArray(), CancellationToken); // wait for all tasks
                        Tasks.Clear(); // Remove all previous tasks.
                    }
                    
                    RequestLimiter.Limit();

                    Tasks.Add(Crawl(LatestId++));
                }
            } finally {
                _dbContextPool.Return(dbContext);
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}