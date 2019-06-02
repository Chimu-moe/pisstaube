using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using opi.v1;
using Pisstaube.Database;
using Pisstaube.Database.Models;

namespace Pisstaube.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HashController : ControllerBase
    {
        private readonly PisstaubeDbContextFactory _contextFactory;

        public HashController(PisstaubeDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }
        
        // GET /api/hash
        [HttpGet]
        public ActionResult<List<BeatmapSet>> Get()
        {
            return null;
        }
        
        // GET /api/hash/:FileMd5
        [HttpGet("{hash}")]
        public ActionResult<BeatmapSet> Get(string hash)
        {
            var bm = _contextFactory.Get().Beatmaps.FirstOrDefault(cb => cb.FileMd5 == hash);
            if (bm == null)
                return null;

            var set = _contextFactory.Get().BeatmapSet.FirstOrDefault(s => s.SetId == bm.ParentSetId);
            if (set == null)
                return null;
            
            set.ChildrenBeatmaps = _contextFactory.Get().Beatmaps.Where(cb => cb.ParentSetId == set.SetId).ToList();
            return set;
        }
    }
}