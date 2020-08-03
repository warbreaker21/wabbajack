using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class NexusFileInfo
    {
        public int Game { get; set; }
        public long ModId { get; set; }
        public long FileId { get; set; }
        public DateTime LastChecked { get; set; }
        public string Data { get; set; }
    }
}
