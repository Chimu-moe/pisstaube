using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using osu.Game.Beatmaps;
using Pisstaube.Allocation;
using Pisstaube.Core.Database.Models;
using Pisstaube.Core.Engine;
using Pisstaube.Core.Utils;
using Pisstaube.Utils;
using StatsdClient;

namespace Pisstaube.Controllers.cheesegull
{
    [Route("api/cheesegull/[controller]"),
     Route("api/bloodcat/[controller]"),]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly IBeatmapSearchEngineProvider _searchEngine;
        private readonly IDistributedCache _cache;

        public SearchController(IBeatmapSearchEngineProvider searchEngine, IDistributedCache cache)
        {
            _searchEngine = searchEngine;
            _cache = cache;
        }

        private bool TryGetFromQuery<T>(IEnumerable<string> keys, T def, out T val)
        {
            var rString = string.Empty;

            foreach (var k in keys)
            {
                if (!Request.Query.ContainsKey(k)) continue;

                Request.Query.TryGetValue(k, out var x);
                rString += x.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(rString))
            {
                val = def;
                return false;
            }

            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (!converter.IsValid(rString))
            {
                val = def;
                return false;
            }

            val = (T) converter.ConvertFromString(rString);
            return true;
        }

        // GET /api/cheesegull/search
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            DogStatsd.Increment("beatmap.searches");
            if (!GlobalConfig.EnableSearch)
                return Unauthorized("Searches are currently Disabled!");

            var raw = Request.Query.ContainsKey("raw");
            var ruri = Request.Query.ContainsKey("ruri");

            TryGetFromQuery(new[] {"query", "q"}, string.Empty, out var query);
            TryGetFromQuery(new[] {"amount", "a"}, 100, out var amount);
            TryGetFromQuery(new[] {"offset", "o"}, 0, out var offset);
            TryGetFromQuery(new[] {"page", "p"}, 0, out var page);
            TryGetFromQuery(new[] {"mode", "m"}, (int) PlayMode.All, out var mode);
            TryGetFromQuery(new[] {"status", "r"}, null, out int? r);
            
            if (ruri && r.HasValue) {
                r = r switch {
                    4 => (int) BeatmapSetOnlineStatus.None,
                    0 => (int) BeatmapSetOnlineStatus.Ranked,
                    7 => (int) BeatmapSetOnlineStatus.Ranked,
                    8 => (int) BeatmapSetOnlineStatus.Loved,
                    3 => (int) BeatmapSetOnlineStatus.Qualified,
                    2 => (int) BeatmapSetOnlineStatus.Pending,
                    5 => (int) BeatmapSetOnlineStatus.Graveyard,
                    
                    _ => (int) BeatmapSetOnlineStatus.Graveyard,
                };
            }

            BeatmapSetOnlineStatus? status = null;
            if (r != null)
                status = (BeatmapSetOnlineStatus) r.Value;

            offset += 100 * page;

            if (query.ToLower().Equals("newest") ||
                query.ToLower().Equals("top rated") || // TODO: Implementing this
                query.ToLower().Equals("most played")) // and this
                query = "";

            var ha = "pisstaube:cache:search" + query + amount + offset + status + mode + page + raw;

            var ca = await _cache.GetStringAsync(ha);
            if (ca != null)
                return Ok(ca);

            var result = await _searchEngine.Search(query, amount, offset, status, (PlayMode) mode);
            
            var beatmapSets = result as BeatmapSet[] ?? result.ToArray();
            if (beatmapSets.Length == 0) result = null; // Cheesegull logic ^^,

            if (!raw)
            {
                ca = JsonUtil.Serialize(beatmapSets);
            }
            else
            {
                if (result == null)
                {
                    ca = "-1\nNo Beatmaps were found!";

                    goto Return;
                }

                ca = beatmapSets.Length >= 100
                    ? "101"
                    : beatmapSets.Length + "\n";

                foreach (var set in beatmapSets) ca += set.ToDirect();
            }

            Return:
            await _cache.SetStringAsync(ha, ca, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
            });
            
            return Ok(ca);
        }
    }
}
