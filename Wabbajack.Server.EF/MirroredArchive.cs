using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class MirroredArchive
    {
        public long Hash { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Uploaded { get; set; }
        public string Rationale { get; set; }
    }
}
