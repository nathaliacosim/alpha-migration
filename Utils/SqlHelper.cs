using Alpha.Data;
using Dapper;
using System;
using System.Threading.Tasks;

namespace Alpha.Utils;

public class SqlHelper
{
    private readonly PgConnect _pgConnect;

    public SqlHelper(PgConnect pgConnect)
    {
        _pgConnect = pgConnect;
    }

    public async Task<T> ExecuteScalarAsync<T>(string query, object parameters = null)
    {
        try
        {
            using var connection = _pgConnect.GetConnection();
            return await connection.ExecuteScalarAsync<T>(query, parameters);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao executar scalar: {ex.Message}");
            return default;
        }
    }

    public async Task<T> QuerySingleOrDefaultAsync<T>(string query, object parameters = null)
    {
        try
        {
            using var connection = _pgConnect.GetConnection();
            return await connection.QuerySingleOrDefaultAsync<T>(query, parameters);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao executar query: {ex.Message}");
            return default;
        }
    }
}