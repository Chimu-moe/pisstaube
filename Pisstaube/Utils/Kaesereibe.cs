using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Game.Rulesets;
using Pisstaube.CacheDb;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using Pisstaube.Online;
using StatsdClient;

namespace Pisstaube.Utils
{
    public class Kaesereibe : ICrawler
    {
        private bool _should_stop;
        private bool _force_stop;
        private int _fail_count;

        public int LatestId { get; private set; }
        public bool IsCrawling { get; private set; }
        
        private object _lock = new object();
        private List<Thread> _pool;
        
        private readonly PisstaubeDbContextFactory _contextFactory;
        private readonly int _workerThreads;
        private Thread _thread_restarter;
        private Thread _dd_thread;

        public Kaesereibe(PisstaubeDbContextFactory contextFactory)
        {
            _pool = new List<Thread>();
            _contextFactory = contextFactory;
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
            lock (_lock)
                if (LatestId == 0)
                    LatestId = _contextFactory.Get().BeatmapSet.LastOrDefault()?.SetId + 1 ?? 0;
                
            while (!_should_stop)
            {
                int id;
                lock (_lock)
                    id = LatestId++;
                using (var db = _contextFactory.GetForWrite())
                {
                    if (!Crawl(id, db.Context))
                        _fail_count++;
                    else
                        _fail_count = 0;
                }

                if (_fail_count > 1000)
                    _should_stop = true;
            }
        }

        public bool Crawl(int id, PisstaubeDbContext _context)
        {
            try
            {
                var setRequest = new JsonWebRequest<BeatmapSet>($"https://{Environment.GetEnvironmentVariable("CHEESEGULL_API")}/api/s/{id}");

                try
                {
                    setRequest.Perform();
                }
                catch
                {
                    if (!setRequest.ResponseString.StartsWith("null"))
                        throw;
                }

                var apiSet = setRequest.ResponseObject;
                if (apiSet == null)
                    return false;

                lock (_lock)
                {
                    _context.BeatmapSet.Add(apiSet);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unknown Error occured while crawling Id {id}!");
                
                // lock (_lock)
                    // Thread.Sleep(TimeSpan.FromMinutes(1));
                return false;
            }
            
            return true;
        }
    }
}