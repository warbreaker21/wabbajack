using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class AccessLog
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public string Ip { get; set; }
        public string MetricsKey { get; set; }
    }
}
