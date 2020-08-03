using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class ModList
    {
        public string MachineUrl { get; set; }
        public long Hash { get; set; }
        public string Metadata { get; set; }
        public string Modlist1 { get; set; }
        public byte BrokenDownload { get; set; }
    }
}
