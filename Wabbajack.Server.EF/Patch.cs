using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class Patch
    {
        public Guid SrcId { get; set; }
        public Guid DestId { get; set; }
        public long? PatchSize { get; set; }
        public DateTime? Finished { get; set; }
        public byte? IsFailed { get; set; }
        public string FailMessage { get; set; }
        public DateTime? LastUsed { get; set; }
        public long Downloads { get; set; }

        public virtual ArchiveDownload Dest { get; set; }
        public virtual ArchiveDownload Src { get; set; }
    }
}
