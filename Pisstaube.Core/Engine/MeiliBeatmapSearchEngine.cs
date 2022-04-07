using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Meilisearch;
using Microsoft.EntityFrameworkCore;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using Pisstaube.Core.Database;
using Pisstaube.Core.Database.Models;
using Index = Meilisearch.Index;

namespace Pisstaube.Core.Engine
{
    public class MeiliBeatmapSearchEngine : IBeatmapSearchEngineProvider
    {
        private readonly PisstaubeDbContext _dbContext;
        private readonly object _dbContextMutex = new();
        
        private MeilisearchClient _meili;
        private Index _index;
        public bool IsConnected => _meili.Health().Result && _index != null;

        public MeiliBeatmapSearchEngine(PisstaubeDbContext dbContext)
        {
            var key = Environment.GetEnvironmentVariable("MEILI_MASTERKEY");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(key))
                key = null;
            
            _meili = new MeilisearchClient(
                $"http://{Environment.GetEnvironmentVariable("MEILI_HOSTNAME")}:{Environment.GetEnvironmentVariable("MEILI_PORT")}",
                key);
            
            _index = _meili.GetOrCreateIndex("pisstaube", "id").Result;
            _dbContext = dbContext;
        }
        
        public async Task Index(IEnumerable<BeatmapSet> sets)
        {
            var elasticBeatmaps = sets.Select(ElasticBeatmap.GetElasticBeatmap).ToList();
            
            var c = 0;
            while (c < elasticBeatmaps.Count)
            {
                var truncatedBeatmaps = elasticBeatmaps.Skip(c).Take(10_000).ToList(); // Submit beatmaps in Chunks

                //await _index.DeleteDocuments(truncatedBeatmaps.Select(bm => bm.Id));
                await _index.AddDocuments(truncatedBeatmaps);
                
                Logger.LogPrint($"{(c + truncatedBeatmaps.Count) / (double) elasticBeatmaps.Count:P}\t{c + truncatedBeatmaps.Count} of {elasticBeatmaps.Count}");
                
                c += truncatedBeatmaps.Count;
            }
            
            Logger.LogPrint("Done!");
        }

        public async Task<IEnumerable<BeatmapSet>> Search(string query, int amount = 100, int offset = 0,
            BeatmapSetOnlineStatus? rankedStatus = null,
            PlayMode mode = PlayMode.All)
        {
            var filterQuery = mode != PlayMode.All
                ? $"mode = {(int) mode} "
                : $"";

            if (filterQuery != "")
                filterQuery += "AND ";

            if (rankedStatus != null)
            {
                if (rankedStatus == BeatmapSetOnlineStatus.Ranked ||
                    rankedStatus == BeatmapSetOnlineStatus.Approved)
                    filterQuery += "" +
                                   "(" +
                                       $"rankedStatus = {(int) BeatmapSetOnlineStatus.Ranked} " +
                                       $"OR rankedStatus = {(int) BeatmapSetOnlineStatus.Approved}" +
                                   ") ";
                else
                    filterQuery += $"rankedStatus = {(int) rankedStatus} ";
            }

            if (filterQuery == "")
                filterQuery = null;
            
            Console.WriteLine(filterQuery);
            
            var searchResult = await _index.Search<ElasticBeatmap>(query, new SearchQuery
            {
                AttributesToHighlight = new [] { "title", "artist", "tags", "creator", "diffName" },
                Limit = amount,
                Offset = offset,
                Matches = true,
                Filters = filterQuery
            });
            
            Logger.LogPrint("Query done!");
            
            ParallelQuery<BeatmapSet> r = null;
            if (searchResult.Hits.Any())
                lock (_dbContextMutex)
                {
                    var hits = 
                        searchResult.Hits
                            .Select(h => h.Id)
                            .ToList();

                    var dbResult = _dbContext.BeatmapSet
                        .Include(s => s.ChildrenBeatmaps)
                        .Where(s => hits.Contains(s.SetId))
                        .AsParallel();

                    r = dbResult;
                }
            
            Logger.LogPrint("Database done!");
            
            var sets = new List<BeatmapSet>();

            if (r != null)
            {
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
            }
            
            Logger.LogPrint("Direct Fix done!");

            return sets;
        }
    }
}