using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class AuthoredFilesSummary
    {
        public Guid ServerAssignedUniqueId { get; set; }
        public DateTime LastTouched { get; set; }
        public DateTime? Finalized { get; set; }
        public string OriginalFileName { get; set; }
        public string MungedName { get; set; }
        public string Author { get; set; }
        public string Size { get; set; }
    }
}
