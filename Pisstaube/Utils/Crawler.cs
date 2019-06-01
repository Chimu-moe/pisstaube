using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets;
using Pisstaube.CacheDb;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Online;
using StatsdClient;

namespace Pisstaube.Utils
{
    public class Crawler
    {
        private bool _should_stop;
        private bool _force_stop;
        private int _fail_count;
        private int _request_count;
        
        public int LatestId { get; private set; }
        public bool IsCrawling { get; private set; }


        private object _lock = new object();
        private List<Thread> _pool;

        private readonly BeatmapSearchEngine _search;
        private readonly APIAccess _apiAccess;
        private readonly RulesetStore _store;
        private readonly BeatmapDownloader _downloader;
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly int _workerThreads;
        private Thread _thread_restarter;
        private Thread _dd_thread;

        public Crawler(BeatmapSearchEngine search,
            APIAccess apiAccess,
            RulesetStore store,
            BeatmapDownloader downloader,
            PisstaubeCacheDbContextFactory cache)
        {
            _pool = new List<Thread>();
            _search = search;
            _apiAccess = apiAccess;
            _store = store;
            _downloader = downloader;
            _cache = cache;
            _workerThreads = int.Parse(Environment.GetEnvironmentVariable("CRAWLER_THREADS"));
        }

        public void BeginCrawling()
        {
            Logger.LogPrint("Begin Crawling!");
            _force_stop = false;
            _should_stop = false;
            IsCrawling = true;

            if (_dd_thread != null) {
                _dd_thread = new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        lock (_lock) DogStatsd.Set("crawler.latest_id", LatestId);
                    }
                });
                _dd_thread.Start();
            }

            while (true)
            {
                if (_thread_restarter == null)
                    _thread_restarter = new Thread(() =>
                    {
                        Logger.LogPrint("Thread Restarter begun it's work!");
                        _should_stop = false;

                        while (!_force_stop)
                        {
                            DogStatsd.ServiceCheck("crawler.is_crawling", Status.OK);
                            for (var i = 0; i < _workerThreads; i++)
                            {
                                _pool.Add(new Thread(_crawl));

                                _pool.Last().Start();
                            }

                            while (!_should_stop)
                            {
                                Thread.Sleep(50);
                                if (_force_stop) break;
                            }
                            
                            if (_force_stop) break;
                            
                            DogStatsd.ServiceCheck("crawler.is_crawling", Status.CRITICAL);
                            Logger.LogPrint("Crawler finished! Gonna retry again in 1d", LoggingTarget.Information);
                            Thread.Sleep(TimeSpan.FromDays(1));
                        }
                    });
                else
                {
                    _thread_restarter = null;
                    continue;
                }

                if (_force_stop)
                {
                    IsCrawling = false;
                    break;
                }

                _thread_restarter.Start();
                break;
            }
        }

        public void Stop()
        {
            Logger.LogPrint("Stop Crawling");
            _force_stop = true;
            _should_stop = true;
            Wait();
            
            _pool = new List<Thread>();
            
            LatestId = 0;
        }

        public void Wait()
        {
            foreach (var thread in _pool)
                thread.Join();
        }

        private void _crawl()
        {
            using(var _context = new PisstaubeDbContext()) {
                lock (_lock)
                    if (LatestId == 0)
                        LatestId = _context.BeatmapSet.LastOrDefault()?.SetId + 1 ?? 0;
                
                while (!_should_stop)
                {
                    int id;
                    lock (_lock)
                        id = LatestId++;

                    if (!Crawl(id, _context))
                        _fail_count++;
                    else
                        _fail_count = 0;

                    if (_fail_count > 50) // We failed 50 times, lets try tomorrow again! maybe there are new exciting beatmaps!
                        _should_stop = true;
                }
            }
        }

        public bool Crawl(int id, PisstaubeDbContext _context)
        {
            try
            {
                while (!_apiAccess.IsLoggedIn)
                    Thread.Sleep(1000);

                var setRequest = new GetBeatmapSetRequest(id);

                lock (_lock)
                    _request_count++;

                setRequest.Perform(_apiAccess);
                
                lock (_lock)
                    if (_request_count > int.Parse(Environment.GetEnvironmentVariable("CRAWLER_REQUESTS_PER_MINUTE")))
                        Thread.Sleep(TimeSpan.FromMinutes(1));

                var apiSet = setRequest.Result;

                var localSet = apiSet?.ToBeatmapSet(_store);
                if (localSet == null)
                    return false;

                var dbSet = BeatmapSet.FromBeatmapSetInfo(localSet);
                if (dbSet == null)
                    return false;
                
                foreach (var map in dbSet.ChildrenBeatmaps)
                {
                    var fileInfo = _downloader.Download(map);
                    
                    map.FileMd5 = _cache.Get()
                        .CacheBeatmaps
                        .Where(cmap => cmap.Hash == fileInfo.Hash)
                        .Select(cmap => cmap.FileMd5)
                        .FirstOrDefault();
                }
                
                lock (_lock)
                {
                    _context.BeatmapSet.Add(dbSet);
                    _context.SaveChanges();
                }

                _search.IndexBeatmap(dbSet);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unknown Error occured while crawling Id {id}!");
                
                lock (_lock)
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                return false;
            }
            
            return true;
        }
    }
}