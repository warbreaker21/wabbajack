using System;
using System.Collections.Generic;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class NexusModInfo
    {
        public Game Game { get; set; }
        public long ModId { get; set; }
        public DateTime LastChecked { get; set; }
        public ModInfo Data { get; set; }
    }
}
