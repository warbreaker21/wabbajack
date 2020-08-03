using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class ModListArchive
    {
        public string MachineUrl { get; set; }
        public string Name { get; set; }
        public long Hash { get; set; }
        public string PrimaryKeyString { get; set; }
        public long Size { get; set; }
        public string State { get; set; }
    }
}
