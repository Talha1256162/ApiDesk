using System.Data;

namespace ApiForge.Persistence.Connection;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
