using System;
using System.Net.Http;
using System.Threading.Tasks;
using osu.Framework.IO.Network;

namespace Pisstaube.API.Utils
{
    public static class HCaptcha
    {
        public class HCaptchaResponse
        {
            public bool Success { get; set; }
        }
        public static async Task<bool> VerifyAccessToken(string accessToken)
        {
            var secret = Environment.GetEnvironmentVariable("HCAPTCHA_SECRET");
            if (string.IsNullOrEmpty(secret)) // Ignore if invalid or not
                return true;

            var webRequest = new JsonWebRequest<HCaptchaResponse>("https://hcaptcha.com/siteverify")
            {
                Method = HttpMethod.Post
            };
            webRequest.AddParameter("response", accessToken);
            webRequest.AddParameter("secret", secret);
            
            await webRequest.PerformAsync();
            
            return webRequest.ResponseObject.Success;
        }
    }
}