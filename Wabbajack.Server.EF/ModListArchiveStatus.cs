using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class ModListArchiveStatus
    {
        public byte[] PrimaryKeyStringHash { get; set; }
        public long Hash { get; set; }
        public string PrimaryKeyString { get; set; }
        public byte IsValid { get; set; }
    }
}
