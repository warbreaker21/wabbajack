using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class AuthoredFile
    {
        public Guid ServerAssignedUniqueId { get; set; }
        public DateTime LastTouched { get; set; }
        public string CdnfileDefinition { get; set; }
        public DateTime? Finalized { get; set; }
    }
}
