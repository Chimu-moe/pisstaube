using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using opi.v1;
using osu.Game.Beatmaps;
using Shared.Helpers;
using Shared.Interfaces;

namespace Pisstaube.Database.Models
{
    [Serializable]
    public class BeatmapSet : ISerializer
    {
        [Required]
        [Key]
        [JsonProperty("SetID")]
        public int SetId { get; set; }

        [JsonProperty("ChildrenBeatmaps")]
        public List<ChildrenBeatmap> ChildrenBeatmaps { get; set; }

        [JsonProperty("RankedStatus")]
        public BeatmapSetOnlineStatus RankedStatus { get; set; }

        [JsonProperty("ApprovedDate")]
        public DateTime? ApprovedDate { get; set; }

        [JsonProperty("LastUpdate")]
        public DateTime? LastUpdate { get; set; }

        [JsonProperty("LastChecked")]
        public DateTime? LastChecked { get; set; }

        [JsonProperty("Artist")]
        public string Artist { get; set; }

        [JsonProperty("Title")]
        public string Title { get; set; }

        [JsonProperty("Creator")]
        public string Creator { get; set; }

        [JsonProperty("Source")]
        public string Source { get; set; }

        [JsonProperty("Tags")]
        public string Tags { get; set; }

        [JsonProperty("HasVideo")]
        public bool HasVideo { get; set; }

        [JsonProperty("Genre")]
        public Genre Genre { get; set; }

        [JsonProperty("Language")]
        public Language Language { get; set; }

        [JsonProperty("Favourites")]
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

        public void ReadFromStream(MStreamReader sr)
        {
            SetId = sr.ReadInt32();

            var count = sr.ReadInt32();
            ChildrenBeatmaps = new List<ChildrenBeatmap>();
            for (var i = 0; i < count; i++)
                ChildrenBeatmaps.Add(sr.ReadData<ChildrenBeatmap>());

            RankedStatus = (BeatmapSetOnlineStatus) sr.ReadByte();

            if (DateTime.TryParse(sr.ReadString(), out var res))
                ApprovedDate = res;
            
            if (DateTime.TryParse(sr.ReadString(), out res))
                LastUpdate = res;
            
            if (DateTime.TryParse(sr.ReadString(), out res))
                LastChecked = res;

            Artist = sr.ReadString();
            Title = sr.ReadString();
            Creator = sr.ReadString();
            Source = sr.ReadString();
            Tags = sr.ReadString();
            HasVideo = sr.ReadBoolean();
            Genre = (Genre) sr.ReadByte();
            Language = (Language) sr.ReadByte();
            Favourites = sr.ReadInt64();
        }

        public void WriteToStream(MStreamWriter sw)
        {
            sw.Write(SetId);
            sw.Write(ChildrenBeatmaps.Count);
            foreach (var bm in ChildrenBeatmaps)
                sw.Write(bm);
            sw.Write((byte) RankedStatus);
            sw.Write(ApprovedDate?.ToString(), true);
            sw.Write(LastUpdate?.ToString(), true);
            sw.Write(LastChecked?.ToString(), true);
            sw.Write(Artist, true);
            sw.Write(Title, true);
            sw.Write(Creator, true);
            sw.Write(Source, true);
            sw.Write(Tags, true);
            sw.Write(HasVideo);
            sw.Write((byte) Genre);
            sw.Write((byte) Language);
            sw.Write(Favourites);
        }
    }
}