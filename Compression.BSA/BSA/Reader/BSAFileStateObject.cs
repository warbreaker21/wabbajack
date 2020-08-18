using System;
using System.Collections.Generic;
using System.Text;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Compression.BSA
{
    [JsonName("BSAFileState")]
    public class BSAFileStateObject : FileStateObject
    {
        public bool FlipCompression { get; set; }
        public ulong FileHash { get; set; }
        public ulong FolderHash { get; set; }

        public BSAFileStateObject() { }

        public BSAFileStateObject(FileRecord fileRecord)
        {
            FlipCompression = fileRecord.FlipCompression;
            FileHash = fileRecord.Hash;
            FolderHash = fileRecord.Folder.Hash;
            if (fileRecord.BSA.HasFolderNames)
            {
                Path = fileRecord.Path;
            }
            else
            {
                Path = (RelativePath)$"{FolderHash}\\{fileRecord.Name}";
            }

            Index = fileRecord._index;
        }

    }
}
