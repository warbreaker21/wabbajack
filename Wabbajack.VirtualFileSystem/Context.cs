using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Ceras.Resolvers;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public class Context : IExternalRootObject
    {
        private const string MAGIC = "Wabbajack VFS File";
        private const string STAGED_ROOT = "vfs_staged_files";
        private const int DISK_INDEXER_TASKS = 4;
        private const int INGEST_BUFFER_SIZE = 1024;
        private const int INDEXER_BUFFER_SIZE = 1024;
        private const int DISK_SYNC_BUFFER_SIZE = 1024;

        private string _filename;
        private Mode _mode;
        private Channel<IndexJob> _ingestChannel;
        private Channel<IndexJob> _indexerChannel;
        private Channel<IndexJob> _diskSyncChannel;

        private ImmutableDictionary<string, VirtualFile> _topLevelIndex;
        private ImmutableDictionary<string, VirtualFile> _byFullPathIndex;
        private ImmutableDictionary<ulong, VirtualFile> _byHashIndex;

        public Context() : this(Mode.Transient)
        {
        }

        public Context(string disk_name) : this(Mode.Persistent, disk_name)
        {
        }

        public Context(Mode contextMode) : this(Mode.Persistent, contextMode == Mode.Persistent ? "vfs_log.bin" : null)
        {
        }

        private Context(Mode contextMode, string filename)
        {
            _mode = contextMode;
            _filename = filename;
        }

        private class IndexJob
        {
            public string Source { get; set; }
            public VirtualFile File { get; set; }
            public TaskCompletionSource<VirtualFile> WhenFinished { get; set; }

            public IndexMode Mode { get; set; }
        }

        enum IndexMode
        {
            Delete,
            Update,
            Add
        }

        public void Startup()
        {
            _indexerChannel = Channel.CreateBounded<>()
        }

        private async Task DiskIndexerJob(int idx)
        {
            while (true)
            {
                var value = await _ingestChannel.Reader.ReadAsync();
                if (value == null) break;

                var result = await UpdateFile(value);
                if (result == null)
                {
                    value.WhenFinished.SetResult(null);
                }

                await _indexerChannel.Writer.WriteAsync(result);
            }

        }

        private async Task<IndexJob> UpdateFile(IndexJob job)
        {
            if (_topLevelIndex.TryGetValue(job.Source, out var vf))
            {
                if (!File.Exists(job.Source))
                {
                    job.File = vf;
                    job.Mode = IndexMode.Delete;
                    return job;
                }


                var fi = new FileInfo(job.Source);
                if (fi.Length == vf.Size && fi.LastWriteTime == vf.LastModified) return null;

                job.Mode = IndexMode.Update;
                job.File = await IndexFile(job.Source);

                return null;
            }

            job.Mode = IndexMode.Add;
            job.File = await IndexFile(job.Source);
            return job;
        }

        private Task<VirtualFile> IndexFile(string jobSource)
        {
            return IndexFile(jobSource, jobSource);
        }

        private async Task SetChildren(string jobSource, VirtualFile vf)
        {
            if (FileExtractor.CanExtract(Path.GetExtension(jobSource)))
            {
                var tmp_dir = Path.Combine(STAGED_ROOT, Guid.NewGuid().ToString());
                Utils.Status($"Extracting Archive {Path.GetFileName(vf.Path)}");

                FileExtractor.ExtractAll(vf.Path, tmp_dir);

                var files = Directory.EnumerateFiles(tmp_dir, DirectoryEnumerationOptions.Recursive)
                    .Select(abs_path => Task.Run(() =>
                    {
                        var rel_path = abs_path.RelativeTo(tmp_dir);
                        return IndexFile(rel_path, abs_path);
                    }))
                    .ToList();
                
                DeleteDirectory(tmp_dir);

                var d = new Dictionary<string, VirtualFile>();
                foreach (var file in files)
                {
                    var f = await file;
                    d[f.Path] = f;
                }

                vf.Children = d;
            }
        }

        private async Task<VirtualFile> IndexFile(string rel_path, string abs_path)
        {
            var fi = new FileInfo(abs_path);

            var vf = new VirtualFile
            {
                Context = this,
                Path = rel_path,
                Size = fi.Length,
                LastModified = fi.LastWriteTime
            };

            // Just start the hasher for now, we'll continue to do other work while we wait.
            var hasher = Utils.FileHashAsync(abs_path);

            await SetChildren(abs_path, vf);

            vf.Hash = await hasher;

            return vf;
        }

        /*
        private void UpdateFile(string f)
        {
            TOP:
            var lv = Lookup(f);
            if (lv == null)
            {
                Utils.Status($"Analyzing {f}");

                lv = new VirtualFile
                {
                    Paths = new[] { f }
                };

                lv.Analyze();
                Add(lv);
                if (lv.IsArchive)
                {
                    UpdateArchive(lv);
                    // Upsert after extraction incase extraction fails
                    lv.FinishedIndexing = true;
                }
            }

            if (lv.IsOutdated)
            {
                Purge(lv);
                goto TOP;
            }
        }

        private void UpdateArchive(VirtualFile f)
        {
            if (!f.IsStaged)
                throw new InvalidDataException("Can't analyze an unstaged file");

            var tmp_dir = Path.Combine(_stagedRoot, Guid.NewGuid().ToString());
            Utils.Status($"Extracting Archive {Path.GetFileName(f.StagedPath)}");

            FileExtractor.ExtractAll(f.StagedPath, tmp_dir);


            Utils.Status($"Updating Archive {Path.GetFileName(f.StagedPath)}");

            var entries = Directory.EnumerateFiles(tmp_dir, "*", SearchOption.AllDirectories)
                .Select(path => path.RelativeTo(tmp_dir));

            var new_files = entries.Select(e =>
            {
                var new_path = new string[f.Paths.Length + 1];
                f.Paths.CopyTo(new_path, 0);
                new_path[f.Paths.Length] = e;
                var nf = new VirtualFile
                {
                    Paths = new_path
                };
                nf._stagedPath = Path.Combine(tmp_dir, e);
                Add(nf);
                return nf;
            }).ToList();

            // Analyze them
            new_files.PMap(file =>
            {
                Utils.Status($"Analyzing {Path.GetFileName(file.StagedPath)}");
                file.Analyze();
            });
            // Recurse into any archives in this archive
            new_files.Where(file => file.IsArchive).Do(file => UpdateArchive(file));

            f.FinishedIndexing = true;

            if (!_isSyncing)
                SyncToDisk();

            Utils.Status("Cleaning Directory");
            DeleteDirectory(tmp_dir);
        }

        public Action Stage(IEnumerable<VirtualFile> files)
        {
            var grouped = files.SelectMany(f => f.FilesInPath)
                .Distinct()
                .Where(f => f.ParentArchive != null)
                .GroupBy(f => f.ParentArchive)
                .OrderBy(f => f.Key == null ? 0 : f.Key.Paths.Length)
                .ToList();

            var Paths = new List<string>();

            foreach (var group in grouped)
            {
                var tmp_path = Path.Combine(_stagedRoot, Guid.NewGuid().ToString());
                FileExtractor.ExtractAll(group.Key.StagedPath, tmp_path);
                Paths.Add(tmp_path);
                foreach (var file in group)
                    file._stagedPath = Path.Combine(tmp_path, file.Paths[group.Key.Paths.Length]);
            }

            return () =>
            {
                Paths.Do(p =>
                {
                    if (Directory.Exists(p)) DeleteDirectory(p);
                });
            };
        }*/

        public async Task AddRoot(string root)
        {
            var jobs = Directory.EnumerateFiles(root, "*", DirectoryEnumerationOptions.Recursive)
                .Select(f => new IndexJob()
                {
                    Source = f,
                    WhenFinished = new TaskCompletionSource<VirtualFile>()
                }).ToList();

            foreach (var job in jobs)
            {
                await _ingestChannel.Writer.WriteAsync(job);
            }

            await Task.WhenAll(jobs.Select(j => j.WhenFinished.Task));
        }

        public enum Mode
        {
            Persistent,
            Transient
        }

        public int GetReferenceId()
        {
            return 0;
        }
    }
}
