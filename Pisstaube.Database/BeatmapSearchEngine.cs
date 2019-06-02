using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Nest;
using opi.v1;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using Pisstaube.Database.Models;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace Pisstaube.Database
{
    public class BeatmapSearchEngine
    {
        private readonly PisstaubeDbContextFactory _contextFactory;
        private readonly ElasticClient _elasticClient;
        private static object _lock = new object();
        
        public BeatmapSearchEngine(PisstaubeDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
            var settings = new ConnectionSettings(new Uri($"http://{Environment.GetEnvironmentVariable("ELASTIC_HOSTNAME")}:{Environment.GetEnvironmentVariable("ELASTIC_PORT")}"))
                .DefaultIndex("pisstaube");

            _elasticClient = new ElasticClient(settings);
            _elasticClient.CreateIndex("pisstaube");
        }

        public void IndexBeatmap(BeatmapSet set)
        {
            lock (_lock)
            {
                _elasticClient.DeleteByQuery<ElasticBeatmap>(x =>
                    x.Query(query => query.Exists(exists => exists.Field(field => field.SetId == set.SetId))));
                var map = ElasticBeatmap.GetElasticBeatmap(set);

                Logger.LogPrint($"Index ElasticBeatmap of Id {set.SetId}");
                
                var result = _elasticClient.IndexDocument(map);
                if (!result.IsValid)
                    Logger.LogPrint(result.DebugInformation, LoggingTarget.Network, LogLevel.Important);
            }
        }

        public void DeleteAllBeatmaps()
        {
            Logger.LogPrint("Deleting all Beatmaps from ElasticSearch!");
            lock (_lock)
            {
                _elasticClient.DeleteByQuery<ElasticBeatmap>(x => x);
            }
        }

        public List<BeatmapSet> Search(string query,
            int amount = 100,
            int offset = 0,
            BeatmapSetOnlineStatus? rankedStatus = null,
            PlayMode mode = PlayMode.All)
        {
            if (amount > 100 || amount <= -1)
                amount = 100;
            
            var sets = new List<BeatmapSet>();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var result = _elasticClient.Search<ElasticBeatmap>(s =>
                {
                    var ret = s
                        .From(offset)
                        .Size(amount)
                        .MinScore(10)
                        .Query(q =>
                            q.Bool(b => b
                                    .Must(must =>
                                        {
                                            QueryContainer res = must;
                                            if (rankedStatus != null)
                                                res = must.Term(term => term.Field(p => p.RankedStatus)
                                                    .Value(rankedStatus));
                                            
                                            if (mode != PlayMode.All)
                                                res &= must.Term(term => term.Field(p => p.Mode)
                                                    .Value(mode));

                                            return res;
                                        }
                                    )
                                    .Should(should =>
                                        should.Match(match => match.Field(p => p.Creator).Query(query).Boost(5)) ||
                                        should.Match(match => match.Field(p => p.Artist).Query(query)) ||
                                        should.Match(match => match.Field(p => p.DiffName).Query(query)) ||
                                        should.Match(match => match.Field(p => p.Tags).Query(query)) ||
                                        should.Match(match => match.Field(p => p.Title).Query(query).Boost(2))
                                    )
                            )
                        );
                    Logger.LogPrint(_elasticClient.RequestResponseSerializer.SerializeToString(ret),
                        LoggingTarget.Network, LogLevel.Debug);
                    return ret;
                });

                if (!result.IsValid)
                    Logger.LogPrint(result.DebugInformation, LoggingTarget.Network, LogLevel.Important);

                sets.AddRange(result.Hits.Select(hit =>
                {
                    var map = _contextFactory.Get().BeatmapSet.First(set => set.SetId == hit.Source.SetId);
                    map.ChildrenBeatmaps = _contextFactory.Get().Beatmaps.Where(cb => cb.Parent == map).ToList();
                    return map;
                }));
            }
            else
            {
                sets = _contextFactory.Get().BeatmapSet.Where(set => (rankedStatus == null || set.RankedStatus == rankedStatus) &&
                                                                     _contextFactory.Get().Beatmaps.Where(cb => cb.ParentSetId == set.SetId)
                                                            .FirstOrDefault(cb => mode == PlayMode.All || cb.Mode == mode) != null)
                    .OrderByDescending(x => x.ApprovedDate)
                    .Skip(offset)
                    .Take(amount)
                    .ToList();

                foreach (var set in sets)
                    set.ChildrenBeatmaps = _contextFactory.Get().Beatmaps.Where(cb => cb.Parent == set).ToList();
            }

            return sets;
        }
    }
}