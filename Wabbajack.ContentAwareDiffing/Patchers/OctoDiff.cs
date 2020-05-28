using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Octodiff.Core;
using Octodiff.Diagnostics;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;

namespace Wabbajack.ContentAwareDiffing.Patchers
{
    public class OctoDiff : IPatcher
    {
        private static ProgressReporter reporter = new ProgressReporter();
        public byte[] FourCC => Definitions.ByType[Definitions.FileType.OCTODELTA].First().Signature;

        public async Task BuildPatch(AbsolutePath source, AbsolutePath destination, AbsolutePath patchOutput)
        {
            Utils.Status("Building patch signature");
            await using var sourceStream = await source.OpenRead();
            await using var patchStream = await patchOutput.OpenWrite();
            await using var destStream = await destination.OpenShared(); 
                
            await GeneratePatch(sourceStream, destStream, patchStream); 
        }

        public static async Task GeneratePatch(Stream sourceStream,
            Stream destStream, Stream patchStream)
        {
            await using var signatureStream = new MemoryStream();
            var signatureBuilder = new SignatureBuilder();
            signatureBuilder.Build(sourceStream, new SignatureWriter(signatureStream));
            signatureStream.Position = 0;

            Utils.Status("Building Patch");
            var db = new DeltaBuilder {ProgressReporter = reporter};
            db.BuildDelta(destStream, new SignatureReader(signatureStream, reporter),
                new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(patchStream)));
        }

        public async Task<bool> CanBuildPatch(AbsolutePath source, AbsolutePath destination)
        {
            // Can always build a octodiff
            return true;
        }
        
        private class ProgressReporter : IProgressReporter
        {
            private DateTime _lastUpdate = DateTime.UnixEpoch;
            private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(100);
            public void ReportProgress(string operation, long currentPosition, long total)
            {
                if (DateTime.Now - _lastUpdate < _updateInterval) return;
                _lastUpdate = DateTime.Now;
                if (currentPosition >= total || total < 1 || currentPosition < 0)
                    return;
                Utils.Status(operation, new Percent(total, currentPosition));
            }
        }
    }
}
