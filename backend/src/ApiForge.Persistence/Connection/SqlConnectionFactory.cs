using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ApiForge.Persistence.Connection;

public sealed class SqlConnectionFactory(IConfiguration configuration) : ISqlConnectionFactory
{
    public IDbConnection CreateConnection()
    {
        var connectionString = configuration.GetConnectionString("ApiForge")
            ?? throw new InvalidOperationException("Connection string 'ApiForge' is missing.");

        return new SqlConnection(connectionString);
    }
}
