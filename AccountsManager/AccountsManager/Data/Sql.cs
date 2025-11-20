using Microsoft.Data.SqlClient;

namespace AccountsManager.Data
{
    public class Sql
    {
        private readonly IConfiguration _config;
        public Sql(IConfiguration config)
        {
            _config = config;
        }

        public async Task<SqlConnection> GetOpenConnectionAsync()
        {
            var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();
            return conn;
        }
    }
}