using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Microsoft.EntityFrameworkCore;
using Nest;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using Pisstaube.Core.Database;
using Pisstaube.Core.Database.Models;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace Pisstaube.Core.Engine
{
    public class ElasticBeatmapSearchEngine : IBeatmapSearchEngineProvider
    {
        private readonly PisstaubeDbContext _dbContext;
        private readonly object _dbContextMutex = new();
        private readonly ElasticClient _elasticClient;

        public ElasticBeatmapSearchEngine(PisstaubeDbContext dbContext)
        {
            _dbContext = dbContext;
            
            var settings = new ConnectionSettings(
                    new Uri(
                        $"http://{Environment.GetEnvironmentVariable("ELASTIC_HOSTNAME")}:{Environment.GetEnvironmentVariable("ELASTIC_PORT")}"
                    )
                )
                .DefaultMappingFor<ElasticBeatmap>(m => m
                    .IdProperty(p => p.Id)
                )
                .EnableHttpCompression()
                .RequestTimeout(new TimeSpan(0, 10, 0))
                .DefaultIndex("pisstaube");
            
            _elasticClient = new ElasticClient(settings);
        }

        public bool IsConnected => _elasticClient.Ping().ApiCall.Success;

        public async Task Index(IEnumerable<BeatmapSet> sets)
        {
            var elasticBeatmaps = sets.Select(ElasticBeatmap.GetElasticBeatmap).ToList();

            var c = 0;
            while (c < elasticBeatmaps.Count)
            {
                var truncatedBeatmaps = elasticBeatmaps.Skip(c).Take(50_000).ToList(); // Submit beatmaps in Chunks

                // Delete if exists.
                var r = await _elasticClient.DeleteByQueryAsync<ElasticBeatmap>(q => q.Query(
                        query => query
                            .Terms(s => s.Field(f => f.Id).Terms(truncatedBeatmaps.Select(bm => bm.Id))))
                );
                if (!r.IsValid)
                    Logger.LogPrint(r.DebugInformation, LoggingTarget.Network, LogLevel.Important);
                
                var result = await _elasticClient.IndexManyAsync(truncatedBeatmaps); // Index all truncated maps at once.
                if (!result.IsValid)
                    Logger.LogPrint(result.DebugInformation, LoggingTarget.Network, LogLevel.Important);
                
                Logger.LogPrint($"{(c + truncatedBeatmaps.Count) / (double) elasticBeatmaps.Count:P}\t{c + truncatedBeatmaps.Count} of {elasticBeatmaps.Count}");
                
                c += truncatedBeatmaps.Count;
            }
        }
        
        public async Task<IEnumerable<BeatmapSet>> Search(string query,
            int amount = 100,
            int offset = 0,
            BeatmapSetOnlineStatus? rankedStatus = null,
            PlayMode mode = PlayMode.All)
        {
            if (amount > 100 || amount <= -1)
                amount = 100;
            
            var result = await _elasticClient.SearchAsync<ElasticBeatmap>(s => // Super complex query to search things. also super annoying
            {
                var ret = s
                    .From(offset)
                    .Size(amount);

                if (query == string.Empty)
                {
                    ret = ret
                        .MinScore(0)
                        .Aggregations(a => a.Max("ApprovedDate", f => f.Field(v => v.ApprovedDate)));
                }
                else
                {
                    ret = ret.MinScore(1);
                }
                ret = ret.Query(q =>
                            q.Bool(b => b
                                    .Filter(filter =>
                                    {
                                        QueryContainer res = filter;
                                    
                                        if (rankedStatus != null)
                                        {
                                            if (rankedStatus == BeatmapSetOnlineStatus.Ranked ||
                                                rankedStatus == BeatmapSetOnlineStatus.Approved)
                                            {
                                                res = filter.Terms(terms => terms.Field(t => t.RankedStatus).Terms(
                                                    BeatmapSetOnlineStatus.Approved,
                                                    BeatmapSetOnlineStatus.Ranked));
                                            }
                                            else
                                            {
                                                res = filter.Term(term => term.Field(p => p.RankedStatus)
                                                    .Value(rankedStatus));
                                            }
                                        }

                                        if (mode != PlayMode.All)
                                            res &= filter.Term(term => term.Field(p => p.Mode)
                                                .Value(mode));

                                        return res;
                                    })
                                    .Should(should =>
                                    {
                                        var res =
                                            should.Match(match => match.Field(p => p.Creator).Query(query).Boost(2)) ||
                                            should.Match(match => match.Field(p => p.Artist).Query(query).Boost(3)) ||
                                            should.Match(match => match.Field(p => p.DiffName).Query(query).Boost(1)) ||
                                            should.Match(match => match.Field(p => p.Tags).Query(query).Boost(1)) ||
                                            should.Match(match => match.Field(p => p.Title).Query(query).Boost(3));

                                        if (query == "")
                                            res = should;

                                        return res;
                                    }
                                )
                            )
                        );
                
                Logger.LogPrint(_elasticClient.RequestResponseSerializer.SerializeToString(ret, SerializationFormatting.Indented), LoggingTarget.Network, LogLevel.Debug);
                
                return ret;
            });

            if (!result.IsValid)
            {
                Logger.LogPrint(result.DebugInformation, LoggingTarget.Network, LogLevel.Important);
                return null;
            }
            
            Logger.LogPrint("Query done!");
            
            var r = new List<BeatmapSet>();
            if (result.Hits.Count > 0)
                lock (_dbContextMutex)
                {
                    var hits = 
                        result.Hits
                                .Where(h => h != null)
                                .Select(h => h.Source.Id)
                                .ToList();

                    var dbResult = _dbContext.BeatmapSet.Where(s => hits.Any(h => h == s.SetId))
                        .Include(o => o.ChildrenBeatmaps);
                    
                    r.AddRange(dbResult);
                }
            
            Logger.LogPrint("Database done!");
            
            var sets = new List<BeatmapSet>();
            
            foreach (var s in r)
            {
                // Fixes an Issue where osu!direct may not load!
                s.Artist = s.Artist.Replace("|", "");
                s.Title = s.Title.Replace("|", "");
                s.Creator = s.Creator.Replace("|", "");

                foreach (var bm in s.ChildrenBeatmaps)
                    bm.DiffName = bm.DiffName.Replace("|", "");
                
                sets.Add(s);
            }
            
            Logger.LogPrint("Direct Fix done!");

            return sets;
        }
    }
}