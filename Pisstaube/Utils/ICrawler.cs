using Pisstaube.Database;

namespace Pisstaube.Utils
{
    public interface ICrawler
    {
        int LatestId { get;}
        bool IsCrawling { get; }
        
        void BeginCrawling();
        void Stop();
        void Wait();
        bool Crawl(int id, PisstaubeDbContext _context);
    }
}