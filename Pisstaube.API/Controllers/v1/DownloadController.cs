using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pisstaube.API.Utils;
using Pisstaube.Core;
using Pisstaube.Core.Events;

namespace Pisstaube.API.Controllers.v1
{
    public enum DownloadState
    {
        None,
        HCaptcha,
        APIKey,
    }

    [ApiController]
    [Route("api/v1/[controller]")]
    public class DownloadController : ControllerBase
    {
        // GET /api/v1/download/:set_id
        [HttpGet("{setId:int}")]
        public async Task<IActionResult> Get(
            [FromServices] RedisEventTool redisEventTool,
            int setId,
            [FromQuery(Name = "k")] string key,
            [FromQuery(Name = "nv")] bool noVideo,
            [FromQuery(Name = "s")] DownloadState state)
        {
            if (key == null)
            {
                return StatusCode(StatusCodes.Status401Unauthorized,
                    new APIError
                    {
                        Code = ErrorCode.InvalidKey,
                        Message = "The given key is invalid..."
                    });
            }
            
            if (state == DownloadState.None)
            {
                return StatusCode(StatusCodes.Status400BadRequest,
                    new APIError
                    {
                        Code = ErrorCode.InvalidState,
                        Message = "State cannot be none"
                    });
            }

            switch (state)
            {
                case DownloadState.HCaptcha:
                    var verified = await HCaptcha.VerifyAccessToken(key);
                    
                    Console.WriteLine(verified);

                    if (!verified)
                    {
                        return StatusCode(StatusCodes.Status401Unauthorized,
                            new APIError
                            {
                                Code = ErrorCode.InvalidKey,
                                Message = "The given key is invalid... (HCaptcha Failure)"
                            });
                    }

                    var response = await redisEventTool.DownloadBeatmap(new DownloadMapRequest
                    {
                        SetId = setId,
                        NoVideo = noVideo
                    });

                    if (response == null  || response.IpfsHash == "")
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new APIError
                            {
                                Code= ErrorCode.DownloadFailed,
                                Message = "For some unknown reason, we couldn't download the beatmap. This incidence has been reported."
                            });

                    return Redirect($"https://ipfs.chimu.moe/{response.IpfsHash}?filename={response.File}");
            }

            return Ok("Yes");
        }
    }
}