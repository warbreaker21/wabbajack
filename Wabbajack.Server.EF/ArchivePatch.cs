using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class ArchivePatch
    {
        public byte[] SrcPrimaryKeyStringHash { get; set; }
        public string SrcPrimaryKeyString { get; set; }
        public long SrcHash { get; set; }
        public byte[] DestPrimaryKeyStringHash { get; set; }
        public string DestPrimaryKeyString { get; set; }
        public long DestHash { get; set; }
        public string SrcState { get; set; }
        public string DestState { get; set; }
        public string SrcDownload { get; set; }
        public string DestDownload { get; set; }
        public string Cdnpath { get; set; }
    }
}
