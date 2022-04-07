using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using osu.Game.Beatmaps;

namespace Pisstaube.Core.Database.Models
{
    public enum Genre
    {
        Any,
        Unspecified,
        Game,
        Anime,
        Rock,
        Pop,
        Other,
        Novelty,
        HipHop = 9,
        Electronic,
    }

    public enum Language
    {
        Any,
        Other,
        English,
        Japanese,
        Chinese,
        Instrumental,
        Korean,
        French,
        German,
        Swedish,
        Spanish,
        Italian
    }

    [Serializable]
    public class BeatmapSet
    {
        [Required]
        [Key]
        [DataMember(Name = "SetID")]
        public int SetId { get; set; }

        [DataMember(Name = "ChildrenBeatmaps")] public List<ChildrenBeatmap> ChildrenBeatmaps { get; set; }

        [DataMember(Name = "RankedStatus")] public BeatmapSetOnlineStatus RankedStatus { get; set; }

        [DataMember(Name = "ApprovedDate")] public DateTime? ApprovedDate { get; set; }

        [DataMember(Name = "LastUpdate")] public DateTime? LastUpdate { get; set; }

        [DataMember(Name = "LastChecked")] public DateTime? LastChecked { get; set; }

        [DataMember(Name = "Artist")] public string Artist { get; set; }

        [DataMember(Name = "Title")] public string Title { get; set; }

        [DataMember(Name = "Creator")] public string Creator { get; set; }

        [DataMember(Name = "Source")] public string Source { get; set; }

        [DataMember(Name = "Tags")] public string Tags { get; set; }

        [DataMember(Name = "HasVideo")] public bool HasVideo { get; set; }

        [DataMember(Name = "Genre")] public Genre Genre { get; set; }

        [DataMember(Name = "Language")] public Language Language { get; set; }

        [DataMember(Name = "Favourites")] public long Favourites { get; set; }

        [IgnoreDataMember] public string IpfsHash { get; set; }
        [IgnoreDataMember] public bool Disabled { get; set; }

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
                Genre = (Genre) (info.OnlineInfo.Genre.Id ?? (int) Genre.Any),
                Language = (Language) info.OnlineInfo.Language.Id,
                Disabled = info.OnlineInfo.Availability.DownloadDisabled
            };

            foreach (var map in info.Beatmaps)
                beatmapSet.ChildrenBeatmaps.Add(ChildrenBeatmap.FromBeatmapInfo(map, info.OnlineInfo, beatmapSet));

            return beatmapSet;
        }

        public string ToDirect()
        {
            var maxDiff = ChildrenBeatmaps
                .Select(cbm => cbm.DifficultyRating)
                .Concat(new double[] {0})
                .Max();

            maxDiff *= 1.5;

            var retStr = $"{SetId}.osz|" +
                         $"{Artist}|" +
                         $"{Title}|" +
                         $"{Creator}|" +
                         $"{(int) RankedStatus}|" +
                         $"{maxDiff:0.00}|" +
                         $"{LastUpdate}Z|" +
                         $"{SetId}|" +
                         $"{SetId}|" +
                         "0|" +
                         "1234|" +
                         $"{Convert.ToInt32(HasVideo)}|" +
                         $"{Convert.ToInt32(HasVideo) * 4321}|";

            retStr = ChildrenBeatmaps.Aggregate(retStr, (current, cb) => current + cb.ToDirect());

            return retStr.TrimEnd(',') + "|\r\n";
        }
    }
}