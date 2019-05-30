using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Pisstaube.CacheDb;
using StatsdClient;

namespace Pisstaube.Utils
{
    public class Cleaner
    {
        private readonly PisstaubeCacheDbContextFactory _cache;
        private readonly ulong _maxSize;
        private readonly Storage _cacheStorage;
        
        public ulong MaxSize => _maxSize;
        public long DataDirectorySize { get; private set; }
        
        public Cleaner(Storage storage, PisstaubeCacheDbContextFactory cache)
        {
            _cache = cache;
            var maxSize = Environment.GetEnvironmentVariable("CLEANER_MAX_SIZE");
            Debug.Assert(maxSize != null, nameof(maxSize) + " != null");
            
            switch (maxSize[maxSize.Length - 1])
            {
                case 'b':
                case 'B':
                    ulong.TryParse(maxSize.Remove(maxSize.Length - 1), out _maxSize);
                    if (_maxSize == 0)
                        _maxSize = 536870912000; // 500 gb
                    break;
                    
                case 'k':
                case 'K':
                    ulong.TryParse(maxSize.Remove(maxSize.Length - 1), out _maxSize);
                    if (_maxSize == 0)
                        _maxSize = 536870912000; // 500 gb
                    else
                        _maxSize *= 1024;
                    break;
                    
                case 'm':
                case 'M':
                    ulong.TryParse(maxSize.Remove(maxSize.Length - 1), out _maxSize);
                    if (_maxSize == 0)
                        _maxSize = 536870912000; // 500 gb
                    else
                        _maxSize *= 1048576;
                    break;
                    
                case 'g':
                case 'G':
                    ulong.TryParse(maxSize.Remove(maxSize.Length - 1), out _maxSize);
                    if (_maxSize == 0)
                        _maxSize = 536870912000; // 500 gb
                    else
                        _maxSize *= 1073741824;
                    break;
                
                case 't':
                case 'T':
                    ulong.TryParse(maxSize.Remove(maxSize.Length - 1), out _maxSize);
                    if (_maxSize == 0)
                        _maxSize = 536870912000; // 500 gb
                    else
                        _maxSize *= 1099511627776;
                    break;
                
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    long.TryParse(maxSize, out var x);
                    if (x == 0)
                        _maxSize = 536870912000; // 500 gb
                    break;
                
                default:
                    ulong.TryParse(maxSize.Remove(maxSize.Length - 1), out _maxSize);
                    if (_maxSize == 0)
                        _maxSize = 536870912000; // 500 gb
                    break;
            }

            _cacheStorage = storage.GetStorageForDirectory("cache");
            
            var info = new DirectoryInfo(_cacheStorage.GetFullPath("./")); 
            DataDirectorySize = info.EnumerateFiles().Sum(file => file.Length);
            
            DogStatsd.Set("cleaner.storage_usage", (ulong) DataDirectorySize / _maxSize);
        }

        private bool IsFitting(long size) => (ulong) (size + DataDirectorySize) <= _maxSize;

        public void IncreaseSize(long size) => DataDirectorySize += size;

        public bool FreeStorage()
        {
            for (var i = 0; i < 1000; i++)
            {
                Logger.LogPrint($"FreeStorage (DirectorySize: {DataDirectorySize} MaxSize: {_maxSize})");
                DogStatsd.Set("cleaner.storage_usage", (ulong) DataDirectorySize / _maxSize);
                if (IsFitting(0)) return true;
                
                Logger.LogPrint("Freeing Storage");

                using (var db = _cache.GetForWrite())
                {
                    var map = db.Context.CacheBeatmapSet.FirstOrDefault(cbs => (cbs.LastDownload - DateTime.Now).TotalDays < 7);
                    if (map != null)
                    {
                        db.Context.CacheBeatmapSet.Remove(map);
                        db.Context.SaveChanges();
                        if (!_cacheStorage.Exists(map.SetId.ToString("x8")))
                            continue;
                    
                        DataDirectorySize -= new FileInfo(_cacheStorage.GetFullPath(map.SetId.ToString("x8"))).Length;
                        if (DataDirectorySize < 0)
                            DataDirectorySize = 0;
                    
                        _cacheStorage.Delete(map.SetId.ToString("x8"));
                        db.Context.SaveChanges();
                    }
                    else
                    {
                        map = db.Context.CacheBeatmapSet.OrderByDescending(cbs => cbs.LastDownload)
                            .ThenByDescending(cbs => cbs.DownloadCount).FirstOrDefault();
                    
                        if (map == null) continue;
                        db.Context.CacheBeatmapSet.Remove(map);
                        db.Context.SaveChanges();
                        if (!_cacheStorage.Exists(map.SetId.ToString("x8")))
                            continue;
                    
                        DataDirectorySize -= new FileInfo(_cacheStorage.GetFullPath(map.SetId.ToString("x8"))).Length;
                        if (DataDirectorySize < 0)
                            DataDirectorySize = 0;
                    
                        _cacheStorage.Delete(map.SetId.ToString("x8"));
                        db.Context.SaveChanges();
                    }
                }
            }

            return false; // Failed to FreeStorage!
        }
    }
}