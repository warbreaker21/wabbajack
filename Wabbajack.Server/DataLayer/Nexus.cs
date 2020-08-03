using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Wabbajack.Server.EF;

namespace Wabbajack.Server.DataLayer
{
    /// <summary>
    /// SQL routines that read/write cached information from the Nexus
    /// </summary>
    public partial class SqlService
    {
        public async Task<long> DeleteNexusModInfosUpdatedBeforeDate(Game game, long modId, DateTime date)
        {
            var del = await Context.NexusModInfos.Where(mi => mi.Game == game && mi.ModId == modId && mi.LastChecked < date).ToListAsync();
            Context.RemoveRange(del);
            await Context.SaveChangesAsync();
            return del.Count;
        }
        
        public async Task<long> DeleteNexusModFilesUpdatedBeforeDate(Game game, long modId, DateTime date)
        {
            var del = await Context.NexusModFiles.Where(mi => mi.Game == game && mi.ModId == modId && mi.LastChecked < date).ToListAsync();
            Context.RemoveRange(del);
            await Context.SaveChangesAsync();
            return del.Count;
        }
        
        public async Task<ModInfo> GetNexusModInfo(Game game, long modId)
        {
            var result = await Context.NexusModInfos
                .FirstOrDefaultAsync(i => i.Game == game && i.ModId == modId);
            return result?.Data;
        }
        
        public async Task<NexusApiClient.GetModFilesResponse> GetModFiles(Game game, long modId)
        {
            var result = await Context.NexusModFiles
                .FirstOrDefaultAsync(i => i.Game == game && i.ModId == modId);
            return result?.Data;
        }

        
        public async Task AddNexusModInfo(Game game, long modId, DateTime lastCheckedUtc, ModInfo data)
        {
            await using var trans = await Context.Database.BeginTransactionAsync();

            Context.RemoveRange(await Context.NexusModInfos.Where(m => m.Game == game && m.ModId == modId).ToListAsync());
            Context.Add(new NexusModInfo {Game = game, ModId = modId, LastChecked = lastCheckedUtc, Data = data});
            await Context.SaveChangesAsync();
            await trans.CommitAsync();
        }
        
        public async Task AddNexusModFiles(Game game, long modId, DateTime lastCheckedUtc, NexusApiClient.GetModFilesResponse data)
        {
            await using var trans = await Context.Database.BeginTransactionAsync();

            Context.RemoveRange(await Context.NexusModFiles.Where(m => m.Game == game && m.ModId == modId).ToListAsync());
            Context.Add(new NexusModFile {Game = game, ModId = modId, LastChecked = lastCheckedUtc, Data = data});
            await Context.SaveChangesAsync();
            await trans.CommitAsync();
        }
        

        public async Task PurgeNexusCache(long modId)
        {
            await using var conn = await Open();
            Context.RemoveRange(await Context.NexusModInfos.Where(mi => mi.ModId == modId).ToListAsync());
            Context.RemoveRange(await Context.NexusModFiles.Where(mi => mi.ModId == modId).ToListAsync());
            await Context.SaveChangesAsync();
        }

        public async Task<Dictionary<(Game, long), HTMLInterface.PermissionValue>> GetNexusPermissions()
        {
            return await Context.NexusModPermissions.ToDictionaryAsync(e => (e.NexusGameId, e.ModId), e => e.Permissions);
        }

        public async Task SetNexusPermissions(IEnumerable<(Game, long, HTMLInterface.PermissionValue)> permissions)
        {
            await using var trans = await Context.Database.BeginTransactionAsync();
            
            Context.RemoveRange(await Context.NexusModPermissions.ToListAsync());
            
            await Context.AddRangeAsync(permissions.Select(p => new NexusModPermission
            {
                NexusGameId = p.Item1,
                ModId = p.Item2,
                Permissions = p.Item3
            }));

            await Context.SaveChangesAsync();
            await trans.CommitAsync();
        }
    }
}
