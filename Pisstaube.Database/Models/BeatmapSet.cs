using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MessagePack;
using Newtonsoft.Json;
using opi.v1;
using osu.Game.Beatmaps;

namespace Pisstaube.Database.Models
{
    [MessagePackObject]
    public class BeatmapSet
    {
        [System.ComponentModel.DataAnnotations.Key]
        [Required]
        [JsonProperty("SetID")]
        [MessagePack.Key(0)]
        public int SetId { get; set; }

        [JsonProperty("ChildrenBeatmaps")]
        [MessagePack.Key(1)]
        public List<ChildrenBeatmap> ChildrenBeatmaps { get; set; }

        [JsonProperty("RankedStatus")]
        [MessagePack.Key(2)]
        public BeatmapSetOnlineStatus RankedStatus { get; set; }

        [JsonProperty("ApprovedDate")]
        [MessagePack.Key(3)]
        public DateTime? ApprovedDate { get; set; }

        [JsonProperty("LastUpdate")]
        [MessagePack.Key(4)]
        public DateTime? LastUpdate { get; set; }

        [JsonProperty("LastChecked")]
        [MessagePack.Key(5)]
        public DateTime? LastChecked { get; set; }

        [JsonProperty("Artist")]
        [MessagePack.Key(6)]
        public string Artist { get; set; }

        [JsonProperty("Title")]
        [MessagePack.Key(7)]
        public string Title { get; set; }

        [JsonProperty("Creator")]
        [MessagePack.Key(8)]
        public string Creator { get; set; }

        [JsonProperty("Source")]
        [MessagePack.Key(9)]
        public string Source { get; set; }

        [JsonProperty("Tags")]
        [MessagePack.Key(10)]
        public string Tags { get; set; }

        [JsonProperty("HasVideo")]
        [MessagePack.Key(11)]
        public bool HasVideo { get; set; }

        [JsonProperty("Genre")]
        [MessagePack.Key(12)]
        public Genre Genre { get; set; }

        [JsonProperty("Language")]
        [MessagePack.Key(13)]
        public Language Language { get; set; }

        [JsonProperty("Favourites")]
        [MessagePack.Key(14)]
        public long Favourites { get; set; }

        public static BeatmapSet FromBeatmapSetInfo(BeatmapSetInfo info)
        {
            if (info?.Beatmaps == null)
                return null;

            var beatmapSet = new BeatmapSet
            {
                SetId = info.OnlineBeatmapSetID ?? -1,
                RankedStatus = info.Status,
                ApprovedDate = info.OnlineInfo.Ranked?.DateTime,
                LastUpdate = info.OnlineInfo.LastUpdated?.DateTime,
                LastChecked = DateTime.Now,
                Artist = info.Metadata.Artist,
                Title = info.Metadata.Title,
                Creator = info.Metadata.Author.Username,
                Source = info.Metadata.Source,
                Tags = info.Metadata.Tags,
                HasVideo = info.OnlineInfo.HasVideo,
                ChildrenBeatmaps = new List<ChildrenBeatmap>(),
                // Obsolete!
                Genre = Genre.Any,
                Language = Language.Any
            };

            foreach (var map in info.Beatmaps)
                beatmapSet.ChildrenBeatmaps.Add(ChildrenBeatmap.FromBeatmapInfo(map, beatmapSet));

            return beatmapSet;
        }
    }
}