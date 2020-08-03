using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.EF;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task<Guid> AddKnownDownload(Archive a, DateTime downloadFinished)
        {
            await using var conn = await Open();
            var Id = Guid.NewGuid();
            await conn.ExecuteAsync(
                "INSERT INTO ArchiveDownloads (Id, PrimaryKeyString, Size, Hash, DownloadState, Downloader, DownloadFinished, IsFailed) VALUES (@Id, @PrimaryKeyString, @Size, @Hash, @DownloadState, @Downloader, @DownloadFinished, @IsFailed)",
                new
                {
                    Id = Id,
                    PrimaryKeyString = a.State.PrimaryKeyString,
                    Size = a.Size == 0 ? null : (long?)a.Size,
                    Hash = a.Hash == default ? null : (Hash?)a.Hash,
                    DownloadState = a.State,
                    Downloader = AbstractDownloadState.TypeToName[a.State.GetType()],
                    DownloadFinished = downloadFinished,
                    IsFailed = false
                });
            return Id;
        }
        
        public async Task<Guid> EnqueueDownload(Archive a)
        {
            await using var conn = await Open();
            var id = Guid.NewGuid();

            Context.Add(new ArchiveDownload
            {
                Id = id,
                PrimaryKeyString = a.State.PrimaryKeyString,
                Size = a.Size == 0 ? null : (long?)a.Size,
                Hash = a.Hash,
                DownloadState = a.State,
                Downloader = AbstractDownloadState.TypeToName[a.State.GetType()],
            });
            await Context.SaveChangesAsync();
            return id;
        }

        public async Task<HashSet<(Hash Hash, string PrimaryKeyString)>> GetAllArchiveDownloads()
        {
            return (await Context.ArchiveDownloads.Select(e => new {Hash = e.Hash.Value, e.PrimaryKeyString}).ToListAsync())
                .Select(e => (e.Hash, e.PrimaryKeyString))
                .ToHashSet();
        }

        
        public async Task<ArchiveDownload> GetArchiveDownload(Guid id)
        {
            return await Context.ArchiveDownloads.FirstOrDefaultAsync(a => a.Id == id);
        }
        
        public async Task<ArchiveDownload> GetArchiveDownload(string primaryKeyString, Hash hash, long size)
        {
            return await Context.ArchiveDownloads.FirstOrDefaultAsync(a =>
                a.PrimaryKeyString == primaryKeyString && a.Hash == hash && a.Size == size);
        }

        
        public async Task<ArchiveDownload> GetOrEnqueueArchive(Archive a)
        {
            await using var trans = await Context.Database.BeginTransactionAsync();

            var existing = await GetArchiveDownload(a.State.PrimaryKeyString, a.Hash, a.Size);
            if (existing != default)
            {
                return existing;
            }

            var enqueued = await EnqueueDownload(a);
            await trans.CommitAsync();
            return await GetArchiveDownload(enqueued);
        }

        public async Task<ArchiveDownload> GetNextPendingDownload(bool ignoreNexus = false)
        {
            await using var conn = await Open();
           
            if (ignoreNexus)
            {
                return await Context.ArchiveDownloads.FirstOrDefaultAsync(a =>
                    a.DownloadFinished == null && a.Downloader != "NexusDownloader+State");
            }

            return await Context.ArchiveDownloads.FirstOrDefaultAsync(a => a.DownloadFinished == null);
        }
        
        public async Task UpdatePendingDownload(ArchiveDownload ad)
        {
            var ar = await Context.ArchiveDownloads.FirstAsync(a => a.Id == ad.Id);
            ar.IsFailed = ad.IsFailed;
            ar.DownloadFinished = ad.DownloadFinished;
            ar.Size = ad.Size;
            ar.Hash = ad.Hash;
            ar.FailMessage = ad.FailMessage;
            await Context.SaveChangesAsync();
        }

        public async Task<int> EnqueueModListFilesForIndexing()
        {
            await using var conn = await Open();
            return await conn.ExecuteAsync(@"
            INSERT INTO dbo.ArchiveDownloads (Id, PrimaryKeyString, Hash, DownloadState, Size, Downloader)
            SELECT DISTINCT NEWID(), mla.PrimaryKeyString, mla.Hash, mla.State, mla.Size, SUBSTRING(mla.PrimaryKeyString, 0, CHARINDEX('|', mla.PrimaryKeyString))
            FROM [dbo].[ModListArchives] mla
                LEFT JOIN dbo.ArchiveDownloads ad on mla.PrimaryKeyString = ad.PrimaryKeyString AND mla.Hash = ad.Hash
            WHERE ad.PrimaryKeyString is null");
        }

        public async Task<List<Archive>> GetGameFiles(Game game, string version)
        {
            return (await Context.ArchiveDownloads.Where(a =>
                        a.PrimaryKeyString.StartsWith($"GameFileSourceDownloader+State|{game}|{version}|"))
                    .ToListAsync())
                .Select(f => new Archive(f.DownloadState) {Hash = f.Hash.Value, Size = f.Size ?? 0})
                .ToList();
        }

        public async Task<List<Archive>> ResolveDownloadStatesByHash(Hash hash)
        {
            return (await Context.ArchiveDownloads
                    .Where(a => a.Hash == hash && a.IsFailed == false && a.DownloadFinished != null)
                    .OrderByDescending(a => a.DownloadFinished)
                    .ToListAsync())
                .Select(f => new Archive(f.DownloadState) {Hash = f.Hash.Value, Size = f.Size ?? 0})
                .ToList();
        }
    }
}
