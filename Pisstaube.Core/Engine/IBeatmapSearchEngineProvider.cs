using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using Pisstaube.Core.Database.Models;

namespace Pisstaube.Core.Engine
{
    public interface IBeatmapSearchEngineProvider
    {
        bool IsConnected { get; }
        
        Task Index(IEnumerable<BeatmapSet> sets);

        Task<IEnumerable<BeatmapSet>> Search(string query,
            int amount = 100,
            int offset = 0,
            BeatmapSetOnlineStatus? rankedStatus = null,
            PlayMode mode = PlayMode.All);
    }
}