using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class NexusModFilesSlow
    {
        public long GameId { get; set; }
        public long FileId { get; set; }
        public long ModId { get; set; }
        public DateTime LastChecked { get; set; }
    }
}
