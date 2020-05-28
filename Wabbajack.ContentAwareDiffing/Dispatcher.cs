using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.ContentAwareDiffing.Patchers;
using OctoDiff = Wabbajack.ContentAwareDiffing.Patchers.OctoDiff;

namespace Wabbajack.ContentAwareDiffing
{
    public class Dispatcher
    {
        public static IReadOnlyList<IPatcher> Patchers = new List<IPatcher>
        {
            new DDSDiff(),
            new OctoDiff()
        };
        private static Dictionary<string, IPatcher> _byFourCC;

        static Dispatcher()
        {
            _byFourCC = Patchers.ToDictionary(p => Encoding.ASCII.GetString(p.FourCC));
        }

        public static async Task<bool> CreatePatch(AbsolutePath src, AbsolutePath dest, AbsolutePath patchFile)
        {
            foreach (var patcher in Patchers)
            {
                if (!await patcher.CanBuildPatch(src, dest)) continue;

                await patcher.BuildPatch(src, dest, patchFile);
                return true;
            }

            return false;
        }
    }
}
