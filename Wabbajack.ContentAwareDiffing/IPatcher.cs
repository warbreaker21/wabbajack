using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.ContentAwareDiffing
{
    public interface IPatcher
    {
        /// <summary>
        /// Four bytes that mark the type of patch
        /// </summary>
        public byte[] FourCC { get; }
        
        public Task BuildPatch(AbsolutePath source, AbsolutePath destination, AbsolutePath patchOutput);

        public Task<bool> CanBuildPatch(AbsolutePath source, AbsolutePath destination);
    }
}
