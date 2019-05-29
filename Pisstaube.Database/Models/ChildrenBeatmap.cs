using System.ComponentModel.DataAnnotations;
using MessagePack;
using Newtonsoft.Json;
using opi.v1;
using osu.Game.Beatmaps;

namespace Pisstaube.Database.Models
{
    [MessagePackObject]
    public class ChildrenBeatmap
    {
        [System.ComponentModel.DataAnnotations.Key]
        [Required]
        [JsonProperty("BeatmapID")]
        [MessagePack.Key(0)]
        public int BeatmapId { get; set; }

        [JsonProperty("ParentSetID")]
        [MessagePack.Key(1)]
        public int ParentSetId { get; set; }
        
        [IgnoreMember]
        [JsonIgnore]
        public BeatmapSet Parent { get; set; } 

        [JsonProperty("DiffName")]
        [MessagePack.Key(2)]
        public string DiffName { get; set; }

        [JsonProperty("FileMD5")]
        [MessagePack.Key(3)]
        public string FileMd5 { get; set; }

        [JsonProperty("Mode")]
        [MessagePack.Key(4)]
        public PlayMode Mode { get; set; }

        [JsonProperty("BPM")]
        [MessagePack.Key(5)]
        public float Bpm { get; set; }

        [JsonProperty("AR")]
        [MessagePack.Key(6)]
        public float Ar { get; set; }

        [JsonProperty("OD")]
        [MessagePack.Key(7)]
        public float Od { get; set; }

        [JsonProperty("CS")]
        [MessagePack.Key(8)]
        public float Cs { get; set; }

        [JsonProperty("HP")]
        [MessagePack.Key(9)]
        public float Hp { get; set; }

        [JsonProperty("TotalLength")]
        [MessagePack.Key(10)]
        public int TotalLength { get; set; }

        [JsonProperty("HitLength")]
        [MessagePack.Key(11)]
        public long HitLength { get; set; }

        [JsonProperty("Playcount")]
        [MessagePack.Key(12)]
        public int Playcount { get; set; }

        [JsonProperty("Passcount")]
        [MessagePack.Key(13)]
        public int Passcount { get; set; }

        [JsonProperty("MaxCombo")]
        [MessagePack.Key(14)]
        public long MaxCombo { get; set; }

        [JsonProperty("DifficultyRating")]
        [MessagePack.Key(15)]
        public double DifficultyRating { get; set; }

        public static ChildrenBeatmap FromBeatmapInfo(BeatmapInfo info, BeatmapSet parent = null)
        {
            if (info == null)
                return null;

            var cb = new ChildrenBeatmap();

            cb.BeatmapId = info.OnlineBeatmapID ?? -1;
            cb.ParentSetId = info.BeatmapSetInfoID;
            cb.Parent = parent;
            cb.DiffName = info.Version;
            cb.FileMd5 = info.MD5Hash;
            cb.Mode = (PlayMode) info.RulesetID;
            cb.Ar = info.BaseDifficulty.ApproachRate;
            cb.Od = info.BaseDifficulty.OverallDifficulty;
            cb.Cs = info.BaseDifficulty.CircleSize;
            cb.Hp = info.BaseDifficulty.DrainRate;
            cb.TotalLength = (int) info.OnlineInfo.Length;
            cb.HitLength = (int) info.StackLeniency; // TODO: check
            cb.Playcount = info.OnlineInfo.PassCount;
            cb.Playcount = info.OnlineInfo.PlayCount;
            cb.MaxCombo = 0; // TODO: Fix
            cb.DifficultyRating = info.StarDifficulty;

            return cb;
        }
    }
}