using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class ArchiveContent
    {
        public long Parent { get; set; }
        public long Child { get; set; }
        public string Path { get; set; }
        public byte[] PathHash { get; set; }
    }
}
