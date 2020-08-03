using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Wabbajack.Common;
using Wabbajack.Server.EF;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task<string> LoginByApiKey(string key)
        {
            return await Context.ApiKeys.Where(a => a.Apikey1 == key).Select(a => a.Owner).FirstOrDefaultAsync();
        }
        
        public async Task<string> AddLogin(string name)
        {
            var key = NewAPIKey();

            Context.Add(new ApiKey {Apikey1 = key, Owner = name});
            await Context.SaveChangesAsync();

            return key;
        }

        public static string NewAPIKey()
        {
            var arr = new byte[128];
            new Random().NextBytes(arr);
            return arr.ToHex();
        }
        
        public async Task<IEnumerable<(string Owner, string Key)>> GetAllUserKeys()
        {
            return (await Context.ApiKeys.ToListAsync()).Select(a => (a.Owner, a.Apikey1));
        }

        public async Task<bool> IsTarKey(string metricsKey)
        {
            return await Context.TarKeys.AnyAsync(f => f.MetricsKey == metricsKey);
        }
    }
}
