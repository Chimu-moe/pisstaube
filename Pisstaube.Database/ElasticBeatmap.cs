using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;
using opi.v1;
using osu.Game.Beatmaps;
using Pisstaube.Database.Models;

namespace Pisstaube.Database
{
    public class ElasticBeatmap
    {
        public int SetId;
        public BeatmapSetOnlineStatus RankedStatus;

        public string Artist;
        public string Title;
        public string Creator;
        public List<string> Tags;
        public List<PlayMode> Mode;
        public List<string> DiffName;

        public double ApprovedDate;

        public override string ToString()
        {
            return $"SetId: {SetId}\n" +
                   $"RankedStatus: {RankedStatus}\n" +
                   $"Artist: {Artist}\n" +
                   $"Title: {Title}\n" +
                   $"Creator: {Creator}\n" +
                   $"Tags: [{Tags.Join()}]\n" +
                   $"Mode: {Mode}\n" +
                   $"DiffName: {DiffName}\n" +
                   $"ApprovedDate: {ApprovedDate}";
        }

        public static ElasticBeatmap GetElasticBeatmap(BeatmapSet bmset)
        {
            var bm = new ElasticBeatmap
            {
                SetId = bmset.SetId,
                Artist = bmset.Artist,
                Creator = bmset.Creator,
                RankedStatus = bmset.RankedStatus,
                Mode = bmset.ChildrenBeatmaps.Select(cb => cb.Mode).ToList(),
                Tags = bmset.Tags.Split(" ").Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                Title = bmset.Title,
                DiffName = bmset.ChildrenBeatmaps.Select(cb => cb.DiffName).ToList(),
                ApprovedDate =
                    (bmset.ApprovedDate?.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds ?? 0)
            };
            return bm;
        }
    }
}