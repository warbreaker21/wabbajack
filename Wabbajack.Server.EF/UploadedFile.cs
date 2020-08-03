using System;
using System.Collections.Generic;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class UploadedFile
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public string UploadedBy { get; set; }
        public long Hash { get; set; }
        public DateTime UploadDate { get; set; }
        public string Cdnname { get; set; }
    }
}
