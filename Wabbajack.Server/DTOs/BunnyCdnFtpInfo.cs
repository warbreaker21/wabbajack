using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Server.DTOs
{
    
    public enum BunnyStorageArea
    {
        AuthoredFiles,
        Patches,
        Mirrors
    }
    
    public class BunnyCdnFtpInfo
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Hostname { get; set; }

        public static async Task<BunnyCdnFtpInfo> GetFtpInfo(BunnyStorageArea area)
        {
            return (await Utils.FromEncryptedJson<Dictionary<string, BunnyCdnFtpInfo>>("bunnycdn"))[area.ToString()];
        }
    }
}
