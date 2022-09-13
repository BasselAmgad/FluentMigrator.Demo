using Microsoft.Data.SqlClient;
using System.Data;

namespace FluentMigrator.Demo.Context
{
    public class DapperContext
    {
        private readonly IConfiguration _configuration;
        public DapperContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public IDbConnection CreateConnection()
            => new SqlConnection(_configuration.GetConnectionString("SqlConnection"));
        public IDbConnection CreateMasterConnection()
            => new SqlConnection(_configuration.GetConnectionString("MasterConnection"));
    }
}
