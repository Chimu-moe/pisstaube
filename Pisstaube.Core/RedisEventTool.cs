using System;
using System.Threading;
using System.Threading.Tasks;
using Pisstaube.Core.Events;
using Pisstaube.Core.Utils;
using Pisstaube.Utils;
using StackExchange.Redis;

namespace Pisstaube.Core
{
    public class RedisEventTool
    {
        private readonly IDatabase _database;
        
        public RedisEventTool(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase(int.Parse(Environment.GetEnvironmentVariable("REDIS_DATABASE") ?? "-1"));
        }

        public async Task<TR> Request<TR, T>(string key, T obj)
            where TR : class, new()
        {
            var startTime = DateTime.UtcNow;
            var timeoutTime = DateTime.UtcNow.AddMinutes(1);
            var input = JsonUtil.Serialize(obj);

            var requestGuid = Guid.NewGuid().ToString();
            key = key + ":" + requestGuid;

            await _database.StringSetAsync(key, input);
            
            while (true)
            {
                var outputStr = (string)await _database.StringGetAsync(key);
                var keyHasChanged = outputStr != input || string.IsNullOrEmpty(outputStr);
                
                if (keyHasChanged)
                    return JsonUtil.Deserialize<TR>(outputStr);

                if (startTime > timeoutTime)
                    break;
            }

            _database.KeyDelete(key);

            return null;
        }

        public Task<DownloadMapResponse> DownloadBeatmap(DownloadMapRequest mapRequest)
            => Request<DownloadMapResponse, DownloadMapRequest>(
                $"pisstaube:events:downloads:{mapRequest.SetId}", mapRequest);
    }
}