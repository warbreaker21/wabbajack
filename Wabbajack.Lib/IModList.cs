using System.Collections.Generic;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public interface IModList
    {
        List<Directive> Directives { get; set; }
        List<Archive> Archives { get; set; }
        Game GameType { get; set; }
        string Name { get; set; }
        IModList Clone();
    }
}
