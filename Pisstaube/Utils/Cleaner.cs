using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Pisstaube.CacheDb;

namespace Pisstaube.Utils
{
    public class Cleaner
    {
        private readonly PisstaubeCacheDbContext _context;
        private readonly ulong _maxSize;
        private long _dataDirectorySize;
        private readonly Storage _cacheStorage;
        
        public Cleaner(Storage storage, PisstaubeCacheDbContext context)
        {
            _context = context;
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
            _dataDirectorySize = info.EnumerateFiles().Sum(file => file.Length);
        }

        private bool IsFitting(long size) => (ulong) (size + _dataDirectorySize) <= _maxSize;

        public void IncreaseSize(long size) => _dataDirectorySize += size;

        public bool FreeStorage()
        {
            for (var i = 0; i < 1000; i++)
            {
                Logger.LogPrint($"FreeStorage (DirectorySize: {_dataDirectorySize} MaxSize: {_maxSize})");
                if (IsFitting(0)) return true;
                
                Logger.LogPrint("Freeing Storage");
                
                var map = _context.CacheBeatmapSet.FirstOrDefault(cbs => (cbs.LastDownload - DateTime.Now).TotalDays < 7);
                if (map != null)
                {
                    _context.CacheBeatmapSet.Remove(map);
                    _context.SaveChanges();
                    if (!_cacheStorage.Exists(map.SetId.ToString("x8")))
                        continue;
                    
                    _dataDirectorySize -= new FileInfo(_cacheStorage.GetFullPath(map.SetId.ToString("x8"))).Length;
                    if (_dataDirectorySize < 0)
                        _dataDirectorySize = 0;
                    
                    _cacheStorage.Delete(map.SetId.ToString("x8"));
                    _context.SaveChanges();
                }
                else
                {
                    map = _context.CacheBeatmapSet.OrderByDescending(cbs => cbs.LastDownload)
                        .ThenByDescending(cbs => cbs.DownloadCount).FirstOrDefault();
                    
                    if (map == null) continue;
                    _context.CacheBeatmapSet.Remove(map);
                    _context.SaveChanges();
                    if (!_cacheStorage.Exists(map.SetId.ToString("x8")))
                        continue;
                    
                    _dataDirectorySize -= new FileInfo(_cacheStorage.GetFullPath(map.SetId.ToString("x8"))).Length;
                    if (_dataDirectorySize < 0)
                        _dataDirectorySize = 0;
                    
                    _cacheStorage.Delete(map.SetId.ToString("x8"));
                    _context.SaveChanges();
                }
            }

            return false; // Failed to FreeStorage!
        }
    }
}