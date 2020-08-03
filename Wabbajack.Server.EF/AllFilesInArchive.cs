using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class AllFilesInArchive
    {
        public long TopParent { get; set; }
        public long Child { get; set; }
    }
}
