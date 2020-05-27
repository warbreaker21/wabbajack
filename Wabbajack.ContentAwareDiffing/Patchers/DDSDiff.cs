using System.IO;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using DirectXTexNet;

namespace Wabbajack.ContentAwareDiffing.Patchers
{
    public class DDSDiff : IPatcher
    {
        public byte[] FourCC => Encoding.ASCII.GetBytes("WJDDSDIFF");
        
        private SignatureChecker checker = new SignatureChecker(Definitions.FileType.DDS);
        
        public Task BuildPatch(AbsolutePath source, AbsolutePath destination, AbsolutePath patchOutput)
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> CanBuildPatch(AbsolutePath source, AbsolutePath destination)
        {
            if (!(await checker.MatchesAsync(source)).HasValue ||
                !(await checker.MatchesAsync(destination)).HasValue) return false;

            var srcImage = TexHelper.Instance.LoadFromDDSFile(source.ToString(), DDS_FLAGS.NONE);
            var destImage = TexHelper.Instance.LoadFromDDSFile(destination.ToString(), DDS_FLAGS.NONE);

            var srcMetadata = srcImage.GetMetadata();
            var destMetadata = destImage.GetMetadata();

            if (srcMetadata.Width != destMetadata.Width) return false;
            if (srcMetadata.Height != destMetadata.Height) return false;

            if (srcMetadata.Depth != destMetadata.Depth) return false;
            if (srcMetadata.MipLevels != destMetadata.MipLevels) return false;
            
            if (srcMetadata.Format)

            return true;
        }
    }
}
