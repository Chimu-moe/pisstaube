using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Pisstaube.Database;
using Pisstaube.Database.Models;
using StatsdClient;

namespace Pisstaube.Controllers
{
    [Route("api")] 
    [ApiController]
    public class BeatmapController : ControllerBase
    {
        private readonly PisstaubeDbContextFactory _contextFactory;

        public BeatmapController(PisstaubeDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }
        
        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get()
        {
            return null;
        }
        
        // GET /api/b/:BeatmapId
        [HttpGet("b/{beatmapId:int}")]
        public ActionResult<ChildrenBeatmap> GetBeatmap(int beatmapId)
        {
            DogStatsd.Increment("beatmap.request");
            return _contextFactory.Get().Beatmaps.FirstOrDefault(cb => cb.BeatmapId == beatmapId);
        }

        // GET /api/b/:BeatmapSetId
        [HttpGet("s/{beatmapSetId:int}")]
        public ActionResult<BeatmapSet> GetSet(int beatmapSetId)
        {
            var set = _contextFactory.Get().BeatmapSet.FirstOrDefault(s => s.SetId == beatmapSetId);
            
            DogStatsd.Increment("beatmap.set.request");
            
            if (set == null)
                return null;
            
            set.ChildrenBeatmaps = _contextFactory.Get().Beatmaps.Where(cb => cb.ParentSetId == set.SetId).ToList();
            return set;
        }
    }
}