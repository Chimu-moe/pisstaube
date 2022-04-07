using System;
using System.ComponentModel.DataAnnotations;

namespace Pisstaube.CacheDb.Models
{
    public class CacheBeatmapSet
    {
        [Key] [Required] public int SetId { get; set; }

        public string Hash { get; set; }

        public long DownloadCount { get; set; }

        public DateTime LastDownload { get; set; }
    }
}