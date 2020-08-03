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
        /// <summary>
        /// Deletes all Nexus Mod Info entries that have a last checked date < `date` for a give name/mod pair
        /// </summary>
        /// <param name="game"></param>
        /// <param name="modId"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<long> DeleteNexusModInfosUpdatedBeforeDate(Game game, long modId, DateTime date)
        {
            var del = await Context.NexusModInfos.Where(mi => mi.Game == game && mi.ModId == modId && mi.LastChecked < date).ToListAsync();
            Context.RemoveRange(del);
            await Context.SaveChangesAsync();
            return del.Count;
        }
        
        /// <summary>
        /// Deletes all nexus mod files entries with a checked date < `date` for a given game/mod pair
        /// </summary>
        /// <param name="game"></param>
        /// <param name="modId"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<long> DeleteNexusModFilesUpdatedBeforeDate(Game game, long modId, DateTime date)
        {
            var del = await Context.NexusModFiles.Where(mi => mi.Game == game && mi.ModId == modId && mi.LastChecked < date).ToListAsync();
            Context.RemoveRange(del);
            await Context.SaveChangesAsync();
            return del.Count;
        }
        
        /// <summary>
        /// Get Nexus mod info by game/modid
        /// </summary>
        /// <param name="game"></param>
        /// <param name="modId"></param>
        /// <returns></returns>
        public async Task<ModInfo> GetNexusModInfo(Game game, long modId)
        {
            var result = await Context.NexusModInfos
                .FirstOrDefaultAsync(i => i.Game == game && i.ModId == modId);
            return result?.Data;
        }
        
        /// <summary>
        /// Get Neux mod files by game/modid
        /// </summary>
        /// <param name="game"></param>
        /// <param name="modId"></param>
        /// <returns></returns>
        
        public async Task<NexusApiClient.GetModFilesResponse> GetModFiles(Game game, long modId)
        {
            var result = await Context.NexusModFiles
                .FirstOrDefaultAsync(i => i.Game == game && i.ModId == modId);
            return result?.Data;
        }

        /// <summary>
        /// Add new Nexus mod info, any existing data will be replaced
        /// </summary>
        /// <param name="game"></param>
        /// <param name="modId"></param>
        /// <param name="lastCheckedUtc"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        
        public async Task AddNexusModInfo(Game game, long modId, DateTime lastCheckedUtc, ModInfo data)
        {
            await using var trans = await Context.Database.BeginTransactionAsync();

            Context.RemoveRange(await Context.NexusModInfos.Where(m => m.Game == game && m.ModId == modId).ToListAsync());
            Context.Add(new NexusModInfo {Game = game, ModId = modId, LastChecked = lastCheckedUtc, Data = data});
            await Context.SaveChangesAsync();
            await trans.CommitAsync();
        }
        
        /// <summary>
        /// Adds Nexus mod files info, any existing data will be replaced
        /// </summary>
        /// <param name="game"></param>
        /// <param name="modId"></param>
        /// <param name="lastCheckedUtc"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task AddNexusModFiles(Game game, long modId, DateTime lastCheckedUtc, NexusApiClient.GetModFilesResponse data)
        {
            await using var trans = await Context.Database.BeginTransactionAsync();

            Context.RemoveRange(await Context.NexusModFiles.Where(m => m.Game == game && m.ModId == modId).ToListAsync());
            Context.Add(new NexusModFile {Game = game, ModId = modId, LastChecked = lastCheckedUtc, Data = data});
            await Context.SaveChangesAsync();
            await trans.CommitAsync();
        }
        

        /// <summary>
        /// Deletes all data for a given modid (regardless of game/fileid)`
        /// </summary>
        /// <param name="modId"></param>
        /// <returns></returns>
        public async Task PurgeNexusCache(long modId)
        {
            await using var conn = await Open();
            Context.RemoveRange(await Context.NexusModInfos.Where(mi => mi.ModId == modId).ToListAsync());
            Context.RemoveRange(await Context.NexusModFiles.Where(mi => mi.ModId == modId).ToListAsync());
            await Context.SaveChangesAsync();
        }

        /// <summary>
        /// Get all nexus mod permissions
        /// </summary>
        /// <returns></returns>
        public async Task<Dictionary<(Game, long), HTMLInterface.PermissionValue>> GetNexusPermissions()
        {
            return await Context.NexusModPermissions.ToDictionaryAsync(e => (e.NexusGameId, e.ModId), e => e.Permissions);
        }

        /// <summary>
        /// Replace all nexus mod permissions
        /// </summary>
        /// <param name="permissions"></param>
        /// <returns></returns>
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
