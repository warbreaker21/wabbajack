using System;
using System.Collections.Generic;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class NexusModPermission
    {
        public Game NexusGameId { get; set; }
        public long ModId { get; set; }
        public HTMLInterface.PermissionValue Permissions { get; set; }
    }
}
