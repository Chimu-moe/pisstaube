using System.Threading.Tasks;

namespace Pisstaube.Crawler
{
    public interface ICrawler
    {
        int LatestId { get; }
        bool IsCrawling { get; }

        void Start();
        void Stop();
        void Wait();
        Task<bool> Crawl(int id);
    }
}