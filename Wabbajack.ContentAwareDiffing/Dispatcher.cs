using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wabbajack.ContentAwareDiffing.Patchers;

namespace Wabbajack.ContentAwareDiffing
{
    public class Dispatcher
    {
        public static IReadOnlyList<IPatcher> Patchers = new List<IPatcher>
        {
            new OctoDiff()
        };
        private static Dictionary<string, IPatcher> _byFourCC;

        static Dispatcher()
        {
            _byFourCC = Patchers.ToDictionary(p => Encoding.ASCII.GetString(p.FourCC));
        }

    }
}
