using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class ArchiveDownload
    {
        public ArchiveDownload()
        {
            PatchDests = new HashSet<Patch>();
            PatchSrcs = new HashSet<Patch>();
        }

        public Guid Id { get; set; }
        public string PrimaryKeyString { get; set; }
        public long? Size { get; set; }
        public long? Hash { get; set; }
        public byte? IsFailed { get; set; }
        public DateTime? DownloadFinished { get; set; }
        public string DownloadState { get; set; }
        public string Downloader { get; set; }
        public string FailMessage { get; set; }

        public virtual ICollection<Patch> PatchDests { get; set; }
        public virtual ICollection<Patch> PatchSrcs { get; set; }
    }
}
