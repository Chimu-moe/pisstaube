using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Ipfs.Http;
using osu.Framework.Logging;
using osu.Framework.Platform; 

namespace Pisstaube.Allocation
{
    public class IpfsCache
    {
        private async Task<string> PinFile(string path)
        {
            var host = Environment.GetEnvironmentVariable("IPFS_CLUSTER_HOST");
            var secret = Environment.GetEnvironmentVariable("IPFS_CLUSTER_SECRET");

            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException(nameof(host), "IPFS_CLUSTER_HOST must be set!");
            
            if (string.IsNullOrEmpty(secret))
                throw new ArgumentNullException(nameof(secret), "IPFS_CLUSTER_SECRET must be set!");

            var p = new Process
            {
                StartInfo =
                {
                    RedirectStandardOutput = true,
                    FileName = "ipfs-cluster-ctl",
                    Arguments = $"--host {host} --secret {secret} add {path} -Q --name {path}"
                }
            };
            p.Start();
            
            await p.WaitForExitAsync();
            
            var hash = await p.StandardOutput.ReadToEndAsync();

            return hash.Trim();
        }
        
        public async Task<string> CacheFile(string path)
        {
            try
            {
                var hash = await PinFile(path);

                File.Delete(path); // We won't need the file anymore

                return hash;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to upload file to interplanetary FileSystem (IPFS)");
                return "";
            }
        } 
    } 
}
