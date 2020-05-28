using System.IO;
using System.Runtime.InteropServices;
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
        
        public async Task BuildPatch(AbsolutePath source, AbsolutePath destination, AbsolutePath patchOutput)
        {
            unsafe
            {
                var srcImage = TexHelper.Instance.LoadFromDDSFile(source.ToString(), DDS_FLAGS.NONE);
                var destImage = TexHelper.Instance.LoadFromDDSFile(destination.ToString(), DDS_FLAGS.NONE);
                var destMetadata = destImage.GetMetadata();
                var srcMetaData = srcImage.GetMetadata();

                var srcBytes = ToMultiPlane(new UnmanagedMemoryStream((byte*)srcImage.GetImage(0).Pixels, srcMetaData.Width * srcMetaData.Height * 4));
                var destBytes = ToMultiPlane(new UnmanagedMemoryStream((byte*)destImage.GetImage(0).Pixels, srcMetaData.Width * srcMetaData.Height * 4));
                
                var patchStream = new MemoryStream();
                OctoDiff.GeneratePatch(srcBytes, destBytes, patchStream).Wait();



            }
            /*
            var compressed = srcImage.Compress(0, destMetadata.Format, TEX_COMPRESS_FLAGS.BC7_USE_3SUBSETS, 1);
            using var tempFile = new TempFile();
            compressed.SaveToDDSFile(DDS_FLAGS.NONE, tempFile.Path.ToString());

            var destHash = await destination.FileHashCachedAsync();
            var srcHash = await tempFile.Path.FileHashAsync();

            using var po = new TempFile();
            await Dispatcher.CreatePatch(tempFile.Path, destination, po.Path);
        
            if (destHash != srcHash)
                throw new InvalidDataException($"Hashes don't match {destHash} vs {srcHash}");*/
        }

        private Stream ToMultiPlane(UnmanagedMemoryStream ums)
        {
            var stride = 256 * 256;
            var ms = new MemoryStream();
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    for (int component = 0; component < 4; component++)
                    {
                        ms.Position = component * x * y;
                        ums.Position = (x * y) + component;
                        var b = ums.ReadByte();
                        ms.WriteByte((byte)b);
                    }

                }
            }

            ms.Position = 0;
            return ms;
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

            // Formats match, so we'll throw it to octodiff
            if (srcMetadata.Format == destMetadata.Format) return false;

            return true;
        }
    }
}
