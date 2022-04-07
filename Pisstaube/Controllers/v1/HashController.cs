using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pisstaube.Core.Database;
using Pisstaube.Core.Database.Models;
using Pisstaube.Core.Utils;
using Pisstaube.Utils;
using StatsdClient;

namespace Pisstaube.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class HashController : ControllerBase
    {
        private readonly PisstaubeDbContext _dbContext;
        private object _dbContextLock = new();

        public HashController(PisstaubeDbContext dbContext) => _dbContext = dbContext;

        // GET /api/v1/hash
        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get() => null;

        // GET /api/v1/hash/:FileMd5
        [HttpGet("{hash}")]
        public ActionResult<string> Get(string hash)
        {
            DogStatsd.Increment("v1.beatmap.hash");
            
            lock (_dbContextLock) {
                var bm = _dbContext.Beatmaps.FirstOrDefault(cb => cb.FileMd5 == hash);
                if (bm == null)
                    return null;

                var set = _dbContext.BeatmapSet
                    .Include(s => s.ChildrenBeatmaps)
                    .FirstOrDefault(s => s.SetId == bm.ParentSetId);

                return JsonUtil.Serialize(set);
            }
        }
    }
}