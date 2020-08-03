using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class AllArchiveContent
    {
        public long TopParent { get; set; }
        public long? Parent { get; set; }
        public long Child { get; set; }
        public string Path { get; set; }
        public long? Size { get; set; }
    }
}
