using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Server.EF;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        private AppSettings _settings;
        private AsyncLocal<ServerDBContext> _context = new AsyncLocal<ServerDBContext>();
        public ServerDBContext Context
        {
            get
            {
                return _context.Value ?? (_context.Value = new ServerDBContext(_settings.SqlConnection));
            }
        }
        private ILogger<SqlService> _logger;

        public SqlService(ILogger<SqlService> logger, AppSettings settings)
        {
            _settings = settings;
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
