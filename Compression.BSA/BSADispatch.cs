using System.Collections.Generic;
using System.IO;
using System.Text;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;

namespace Compression.BSA
{
    public static class BSADispatch
    {
        private static readonly SignatureChecker Signature = new SignatureChecker(Definitions.FileType.BSA, Definitions.FileType.BA2, Definitions.FileType.TES3);
        public static IBSAReader OpenRead(AbsolutePath filename)
        {
            switch (Signature.Matches(filename))
            {
                case Definitions.FileType.TES3:
                    return new TES3Reader(filename);
                case Definitions.FileType.BSA:
                    return new BSAReader(filename);
                case Definitions.FileType.BA2:
                    return new BA2Reader(filename);
                default:
                    throw new InvalidDataException("Filename is not a .bsa or .ba2, magic " + filename);
            }
        }
        public static bool MightBeBSA(AbsolutePath filename)
        {
            return Signature.Matches(filename).HasValue;
        }
    }
}
