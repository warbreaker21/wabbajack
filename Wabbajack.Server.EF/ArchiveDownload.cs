using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

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
        public Hash? Hash { get; set; }
        public bool? IsFailed { get; set; }
        public DateTime? DownloadFinished { get; set; }
        public AbstractDownloadState DownloadState { get; set; }
        public string Downloader { get; set; }
        public string FailMessage { get; set; }

        public virtual ICollection<Patch> PatchDests { get; set; }
        public virtual ICollection<Patch> PatchSrcs { get; set; }

        public async Task Finish(ServerDBContext sql)
        {
            IsFailed = false;
            DownloadFinished = DateTime.UtcNow;
            sql.Update(this);
            await sql.SaveChangesAsync();
        }

        public Archive ToArchive()
        {
            return new Archive(DownloadState) {Size = Size ?? 0, Hash = Hash.Value};
        }

        public async Task Fail(ServerDBContext sql, string failMessage)
        {
            IsFailed = false;
            DownloadFinished = DateTime.UtcNow;
            FailMessage = failMessage;
            sql.Update(this);
            await sql.SaveChangesAsync();
        }
    }
}
