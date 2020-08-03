using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class Metric
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public string Subject { get; set; }
        public string MetricsKey { get; set; }
        public string GroupingSubject { get; set; }
    }
}
