namespace Pisstaube.Core.Events
{
    public class DownloadMapRequest
    {
        public string _ID { get; set; }
        public string SetId { get; set; }
        public string NoVideo { get; set; }
    }
    
    public class DownloadMapResponse
    {
        public string _ID { get; set; }
        public string File { get; set; }
        public string IpfsHash { get; set; }
    }
}