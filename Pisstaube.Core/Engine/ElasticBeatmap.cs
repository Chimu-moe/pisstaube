using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using Nest;
using osu.Game.Beatmaps;
using Pisstaube.Core.Database.Models;
using Language = Pisstaube.Core.Database.Models.Language;

namespace Pisstaube.Core.Engine
{
    [ElasticsearchType(IdProperty = nameof(Id))]
    public class ElasticBeatmap
    {
        public int Id { get; set; }
        public BeatmapSetOnlineStatus RankedStatus{ get; set; }

        public string Artist { get; set; }
        public string Title { get; set; }
        public string Creator { get; set; }
        public List<string> Tags { get; set; }
        public List<PlayMode> Mode { get; set; }
        public List<string> DiffName { get; set; }
        public List<float> Cs { get; set; }
        public List<float> Ar { get; set; }
        public List<float> Od { get; set; }
        public List<float> Hp { get; set; }
        public List<double> DifficultyRating { get; set; }
        public List<double> Bpm { get; set; }
        public List<int> TotalLength { get; set; }
        public Genre Genre { get; set; }
        public Language Language { get; set; }

        public double ApprovedDate { get; set; }

        public override string ToString() =>
            $"\nSetId: {Id}\n" +
            $"RankedStatus: {RankedStatus}\n" +
            $"Artist: {Artist}\n" +
            $"Title: {Title}\n" +
            $"Creator: {Creator}\n" +
            $"Tags: [{Tags.Join(", ")}]\n" +
            $"Modes: [{Mode.Select(s => s.ToString()).Join(", ")}]\n" +
            $"DiffNames: [{DiffName.Join(", ")}]\n" +
            $"Cs: [{Cs.Select(s => s.ToString()).Join(", ")}]\n" +
            $"Ar: [{Ar.Select(s => s.ToString()).Join(", ")}]\n" +
            $"Od: [{Od.Select(s => s.ToString()).Join(", ")}]\n" +
            $"Hp: [{Hp.Select(s => s.ToString()).Join(", ")}]\n" +
            $"DifficultyRating: [{DifficultyRating.Select(s => s.ToString(CultureInfo.InvariantCulture)).Join(", ")}]\n" +
            $"Bpm: [{Bpm.Select(s => s.ToString()).Join(", ")}]\n" +
            $"TotalLength: [{TotalLength.Select(s => s.ToString()).Join(", ")}]\n" +
            $"Genre: {Genre}\n" +
            $"Language: {Language}\n" +
            $"DiffName: [{Mode.Select(s => s.ToString()).Join(", ")}]\n" +
            $"ApprovedDate: {ApprovedDate}";

        public static ElasticBeatmap GetElasticBeatmap(BeatmapSet bmSet)
        {
            var bm = new ElasticBeatmap
            {
                Id = bmSet.SetId,
                Artist = bmSet.Artist,
                Creator = bmSet.Creator,
                RankedStatus = bmSet.RankedStatus,
                Mode = bmSet.ChildrenBeatmaps.Select(cb => cb.Mode).ToList(),
                Tags = bmSet.Tags.Split(" ").Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                Title = bmSet.Title,
                DiffName = bmSet.ChildrenBeatmaps.Select(cb => cb.DiffName).ToList(),
                ApprovedDate =
                    bmSet.ApprovedDate?.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds ?? 0,
                Cs = bmSet.ChildrenBeatmaps.Select(cb => cb.Cs).ToList(),
                Ar = bmSet.ChildrenBeatmaps.Select(cb => cb.Ar).ToList(),
                Od = bmSet.ChildrenBeatmaps.Select(cb => cb.Od).ToList(),
                Hp = bmSet.ChildrenBeatmaps.Select(cb => cb.Hp).ToList(),
                DifficultyRating = bmSet.ChildrenBeatmaps.Select(cb => cb.DifficultyRating).ToList(),
                Bpm = bmSet.ChildrenBeatmaps.Select(cb => cb.Bpm).ToList(),
                Genre = bmSet.Genre,
                Language = bmSet.Language,
                TotalLength = bmSet.ChildrenBeatmaps.Select(cb => cb.TotalLength).ToList(),
            };
            return bm;
        }
    }
}