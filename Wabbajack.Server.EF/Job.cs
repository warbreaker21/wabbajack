using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class Job
    {
        public long Id { get; set; }
        public int Priority { get; set; }
        public string PrimaryKeyString { get; set; }
        public DateTime? Started { get; set; }
        public DateTime? Ended { get; set; }
        public DateTime Created { get; set; }
        public byte? Success { get; set; }
        public string ResultContent { get; set; }
        public string Payload { get; set; }
        public string OnSuccess { get; set; }
        public Guid? RunBy { get; set; }
    }
}
