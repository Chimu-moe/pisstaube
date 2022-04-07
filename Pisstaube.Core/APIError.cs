namespace Pisstaube.Core
{
    public enum ErrorCode
    {
        Ok,
        InvalidKey,
        InvalidState,
        DownloadFailed,
    }
    
    public struct APIError
    {
        public ErrorCode Code { get; set; }
        public string Message { get; set; }
    }
}