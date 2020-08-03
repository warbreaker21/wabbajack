using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class NexusKey
    {
        public string ApiKey { get; set; }
        public int DailyRemain { get; set; }
        public int HourlyRemain { get; set; }
    }
}
