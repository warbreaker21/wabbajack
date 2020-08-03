using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class IndexedFile
    {
        public long Hash { get; set; }
        public byte[] Sha256 { get; set; }
        public byte[] Sha1 { get; set; }
        public byte[] Md5 { get; set; }
        public int Crc32 { get; set; }
        public long Size { get; set; }
    }
}
