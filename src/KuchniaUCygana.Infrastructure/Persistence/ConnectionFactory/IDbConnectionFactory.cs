using System.Data;

namespace KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
