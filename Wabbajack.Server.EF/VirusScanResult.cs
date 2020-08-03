using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class VirusScanResult
    {
        public long Hash { get; set; }
        public byte IsMalware { get; set; }
    }
}
