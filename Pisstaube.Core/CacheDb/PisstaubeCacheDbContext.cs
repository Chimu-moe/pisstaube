using Microsoft.EntityFrameworkCore;
using JetBrains.Annotations;
using Pisstaube.CacheDb.Models;
using osu.Framework.Platform;

namespace Pisstaube.CacheDb
{
    public class PisstaubeCacheDbContext : DbContext
    {
        private readonly string _conString;
        public DbSet<CacheBeatmapSet> CacheBeatmapSet { get; [UsedImplicitly] set; }
        public DbSet<Beatmap> CacheBeatmaps { get; [UsedImplicitly] set; }

        public PisstaubeCacheDbContext() {
            _conString = (new NativeStorage(".")).GetDatabaseConnectionString("cache");
        }

        public PisstaubeCacheDbContext(string conString = null) => _conString = conString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.UseSqlite(_conString);
        }

        public void Migrate() => Database.Migrate();
    }
}