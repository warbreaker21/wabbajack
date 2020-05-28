using System;
using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;

namespace Wabbajack.ContentAwareDiffing.Test
{
    public class DiffingTests
    {
        [Fact]
        public async Task CanDiffCompressedDDSFiles()
        {
            var src = @"TestingData\noiseTextureUncompressed.dds".RelativeTo(AbsolutePath.EntryPoint);
            var dest = @"TestingData\noiseTextureCAO5Compressed.dds".RelativeTo(AbsolutePath.EntryPoint);
            
            var tempFile = new TempFile();
            Assert.True(await Dispatcher.CreatePatch(src, dest, tempFile.Path));
        }
    }
}
