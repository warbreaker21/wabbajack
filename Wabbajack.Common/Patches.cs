using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using LiteDB.Engine;

namespace Wabbajack.Common
{
    public static partial class Utils
    {

        private static LiteDatabase _patchDb;
        private static ILiteCollection<PatchCacheEntry> _patchCollection;
        private static SharedEngine _pathEngine;


        class PatchCacheEntry
        {
            public ulong From { get; set; }
            public ulong To { get; set; }
            public byte[] Data { get; set; } = { };
        }    
        private static byte[] PatchKey(Hash src, Hash dest)
        {
            var arr = new byte[16];
            Array.Copy(BitConverter.GetBytes((ulong)src), 0, arr, 0, 8);
            Array.Copy(BitConverter.GetBytes((ulong)dest), 0, arr, 8, 8);
            return arr;
        }

        private static bool TryGetPatchEntry(Hash a, Hash b, out PatchCacheEntry found)
        {
            var result = _patchCollection.FindOne(p => p.From == (ulong)a && p.To == (ulong)b);
            found = result;
            return result != null;

        }
        
        public static async Task CreatePatchCached(byte[] a, byte[] b, Stream output)
        {
            var dataA = a.xxHash();
            var dataB = b.xxHash();
            var key = PatchKey(dataA, dataB);
            

            if (TryGetPatchEntry(dataA, dataB, out var found))
            {
                await output.WriteAsync(found.Data);
                return;
            }

            await using var patch = new MemoryStream();

            Status("Creating Patch");
            OctoDiff.Create(a, b, patch);


            try
            {
                _patchCollection.Upsert(new PatchCacheEntry
                {
                    From = (ulong)dataA, To = (ulong)dataB, Data = patch.ToArray()
                });
            }
            catch (LiteException _)
            {
            }
            
            await patch.CopyToAsync(output);
        }

        public static async Task<long> CreatePatchCached(Stream srcStream, Hash srcHash, FileStream destStream, Hash destHash,
            Stream? patchOutStream = null)
        {
            var key = PatchKey(srcHash, destHash);
            if (TryGetPatchEntry(srcHash, destHash, out var patch))
            {
                if (patchOutStream == null) return patch.Data.Length;
                
                await patchOutStream.WriteAsync(patch.Data);
                return patch.Data.Length;
            }
            
            Status("Creating Patch");
            await using var sigStream = new MemoryStream();
            await using var patchStream = new MemoryStream();
            OctoDiff.Create(srcStream, destStream, sigStream, patchStream);
            _patchCollection.Upsert(new PatchCacheEntry {From = (ulong)srcHash, To = (ulong)destHash, Data = patchStream.ToArray()});
            

            if (patchOutStream == null) return patchStream.Position;
            
            patchStream.Position = 0;
            await patchStream.CopyToAsync(patchOutStream);

            return patchStream.Position;
        }

        public static bool TryGetPatch(Hash foundHash, Hash fileHash, [MaybeNullWhen(false)] out byte[] ePatch)
        {
            var key = PatchKey(foundHash, fileHash);

            if (TryGetPatchEntry(foundHash, fileHash, out var result))
            {
                ePatch = result.Data;
                return true;
            }

            ePatch = null;
            return false;

        }

        public static bool HavePatch(Hash foundHash, Hash fileHash)
        {
            return _patchCollection.Exists(f => f.From == (ulong)foundHash && f.To == (ulong)fileHash);
        }

        public static void ApplyPatch(Stream input, Func<Stream> openPatchStream, Stream output)
        {
            using var ps = openPatchStream();
            using var br = new BinaryReader(ps);
            var bytes = br.ReadBytes(8);
            var str = Encoding.ASCII.GetString(bytes);
            switch (str)
            {
                case "BSDIFF40":
                    BSDiff.Apply(input, openPatchStream, output);
                    return;
                case "OCTODELT":
                    OctoDiff.Apply(input, openPatchStream, output);
                    return;
                default:
                    throw new Exception($"No diff dispatch for: {str}");
            }
        }
    }
}
