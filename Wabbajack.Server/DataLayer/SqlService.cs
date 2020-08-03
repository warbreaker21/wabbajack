using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Server.EF;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        private AppSettings _settings;
        public ServerDBContext Context;
        private ILogger<SqlService> _logger;

        public SqlService(ILogger<SqlService> logger, AppSettings settings)
        {
            _settings = settings;
            Context = new ServerDBContext(settings.SqlConnection);
            _logger = logger;
        }

        public async Task<SqlConnection> Open()
        {
            var conn = new SqlConnection(_settings.SqlConnection);
            await conn.OpenAsync();
            return conn;
        }
    }
}
