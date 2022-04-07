using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Pisstaube.Core.Database;
using Pisstaube.Core.Database.Models;
using Pisstaube.Core.Utils;
using Pisstaube.Utils;
using StatsdClient;

namespace Pisstaube.Controllers.cheesegull
{
    [Route("api/cheesegull")]
    [ApiController]
    public class BeatmapController : ControllerBase
    {
        private readonly PisstaubeDbContext _dbContext;
        private readonly IDistributedCache _cache;
        private object _dbContextLock = new();

        public BeatmapController(PisstaubeDbContext dbContext, IDistributedCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get() => null;

        // GET /api/cheesegull/b/:BeatmapId
        [HttpGet("b/{beatmapId:int}")]
        public async Task<ActionResult> GetBeatmap(int beatmapId)
        {
            DogStatsd.Increment("beatmap.request");

            var raw = Request.Query.ContainsKey("raw");
            var cachedResult = await _cache.GetStringAsync("pisstaube:cache:" + raw + beatmapId);
            if (cachedResult != null)
                return Ok(cachedResult);

            lock (_dbContextLock) {
                var beatmap = _dbContext.Beatmaps.FirstOrDefault(cb => cb.BeatmapId == beatmapId);
                
                if (!raw)
                    return Ok(JsonUtil.Serialize(beatmap));

                var set = _dbContext.BeatmapSet.FirstOrDefault(s => s.SetId == beatmap.ParentSetId);
                if (set == null)
                {
                    _cache.SetStringAsync("pisstaube:cache:" + raw + beatmapId, "0", new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                    });
                    
                    return Ok("0");
                }

                var result = $"{set.SetId}.osz|" +
                             $"{set.Artist}|" +
                             $"{set.Title}|" +
                             $"{set.Creator}|" +
                             $"{(int) set.RankedStatus}|" +
                             "10.00|" +
                             $"{set.LastUpdate}|" +
                             $"{set.SetId}|" +
                             $"{set.SetId}|" +
                             $"{Convert.ToInt32(set.HasVideo)}|" +
                             "0|" +
                             "1234|" +
                             $"{Convert.ToInt32(set.HasVideo) * 4321}\r\n";
                
                _cache.SetStringAsync("pisstaube:cache:" + raw + beatmapId, result, new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                });
                
                return Ok(result);
            }
        }

        // GET /api/cheesegull/b/:BeatmapIds
        [HttpGet("b/{beatmapIds}")]
        public async Task<ActionResult> GetBeatmap(string beatmapIds)
        {
            DogStatsd.Increment("beatmap.request");
            
            var raw = Request.Query.ContainsKey("raw");
            if (raw)
                return Ok("raw is not supported!");
            
            var cachedResult = await _cache.GetStringAsync("pisstaube:cache:" + beatmapIds);
            if (cachedResult != null)
                return Ok(cachedResult);
            
            try
            {
                var bms = beatmapIds.Split(";");
                var bmIds = Array.ConvertAll(bms, int.Parse);

                lock (_dbContextLock)
                {
                    cachedResult = JsonUtil.Serialize(
                        _dbContext.Beatmaps
                            .Where(cb => bmIds.Any(x => cb.BeatmapId == x)));
                }

                
                _cache.SetStringAsync("pisstaube:cache:" + beatmapIds, cachedResult, new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                });

                    return Ok(cachedResult);
            }
            catch (FormatException)
            {
                return Ok("parameter MUST be an int array! E.G 983680;983692;983896");
            }
        }

        // GET /api/cheesegull/s/:BeatmapSetId
        [HttpGet("s/{beatmapSetId:int}")]
        public async Task<ActionResult> GetSet(int beatmapSetId)
        {
            DogStatsd.Increment("beatmap.set.request");
            
            var raw = Request.Query.ContainsKey("raw");

            var cachedResult = await _cache.GetStringAsync("pisstaube:cache:" + raw + beatmapSetId);
            if (cachedResult != null)
                return Ok(cachedResult);

            BeatmapSet beatmapSet;
            lock (_dbContextLock)
            {
                beatmapSet =
                    _dbContext.BeatmapSet
                        .Where(s => s.SetId == beatmapSetId)
                        .Include(x => x.ChildrenBeatmaps)
                        .FirstOrDefault();
            }


            string result;
            if (!raw)
            {
                result = JsonUtil.Serialize(beatmapSet);

                _cache.SetStringAsync("pisstaube:cache:" + raw + beatmapSetId, result, new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                });

                return Ok(result);
            }

            if (beatmapSet == null)
            {
                _cache.SetStringAsync("pisstaube:cache:" + raw + beatmapSetId, "0", new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                });
                    
                return Ok("0");
            }

            result = $"{beatmapSet.SetId}.osz|" +
                     $"{beatmapSet.Artist}|" +
                     $"{beatmapSet.Title}|" +
                     $"{beatmapSet.Creator}|" +
                     $"{(int) beatmapSet.RankedStatus}|" +
                     "10.00|" +
                     $"{beatmapSet.LastUpdate}|" +
                     $"{beatmapSet.SetId}|" +
                     $"{beatmapSet.SetId}|" +
                     $"{Convert.ToInt32(beatmapSet.HasVideo)}|" +
                     "0|" +
                     "1234|" +
                     $"{Convert.ToInt32(beatmapSet.HasVideo) * 4321}\r\n";

            _cache.SetStringAsync("pisstaube:cache:" + raw + beatmapSetId, result, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
            });

            return Ok(result);
        }

        // GET /api/cheesegull/s/:BeatmapSetIds
        [HttpGet("s/{beatmapSetIds}")]
        public async Task<ActionResult> GetSet(string beatmapSetIds)
        {
            DogStatsd.Increment("beatmap.set.request");
            
            var raw = Request.Query.ContainsKey("raw");
            if (raw)
                return Ok("raw is not supported!");
            
            var cachedResult = await _cache.GetStringAsync("pisstaube:cache:" + beatmapSetIds);
            if (cachedResult != null)
                return Ok(cachedResult);

            try
            {
                var bms = beatmapSetIds.Split(";");
                var bmsIds = Array.ConvertAll(bms, int.Parse);

                lock (_dbContextLock)
                {
                    var result = JsonUtil.Serialize(
                        _dbContext.BeatmapSet.Where(set => bmsIds.Any(s => set.SetId == s))
                            .Include(x => x.ChildrenBeatmaps)
                    );
                    
                    _cache.SetStringAsync("pisstaube:cache:" + beatmapSetIds, result, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                    });

                    return Ok(result);
                }
            }
            catch (FormatException)
            {
                return Ok("parameter MUST be an int array! E.G 1;16");
            }
        }
    }
}