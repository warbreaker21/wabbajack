using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.VirtualFileSystem
{
    public class VirtualFile
    {
        private string _fullPath;
        public string FullPath
        {
            get
            {
                if (_fullPath == null)
                    _fullPath = String.Join("|", PathParts);
                return _fullPath;
            }
        }

        public string Path { get; set; }
        public bool IsConcrete => Parent == null;
        public bool IsArchive => Children != null;


        private IEnumerable<string> _pathParts;
        public IEnumerable<string> PathParts
        {
            get
            {
                if (_pathParts == null)
                {
                    var parts = new List<string>();
                    var c = this;
                    while (c != null)
                    {
                        parts.Prepend(c.Path);
                        c = c.Parent;
                    }
                    _pathParts = parts;
                }
                return _pathParts;
            }
        }

        public string Hash { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
        public IDictionary<string, VirtualFile> Children { get; set; }
        public VirtualFile Parent { get; set; }
        public Context Context { get; set; }

    }
}
