using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.NexusApi
{
    public class HTMLInterface
    {
        public static async Task<PermissionValue> GetUploadPermissions(Game game, long modId)
        {
            var client = new Common.Http.Client();
            var response = await client.GetHtmlAsync($"https://nexusmods.com/{game.MetaData().NexusName}/mods/{modId}");
            var perm = response.DocumentNode
                .Descendants()
                .Where(d => d.HasClass("permissions-title") && d.InnerHtml == "Upload permission")
                .SelectMany(d => d.ParentNode.ParentNode.GetClasses())
                .FirstOrDefault(perm => perm.StartsWith("permission-"));

            return perm switch
            {
                "permission-no" => PermissionValue.No,
                "permission-maybe" => PermissionValue.Maybe,
                "permission-yes" => PermissionValue.Yes,
                _ => PermissionValue.No
            };
        }

        public enum PermissionValue
        {
            Yes,
            Maybe,
            No
        }
    }
}
