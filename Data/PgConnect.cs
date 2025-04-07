using Npgsql;
using System;
using System.Data;
using System.Threading.Tasks;

namespace Alpha.Data;

public class PgConnect
{
    private readonly string _connectionString;

    public PgConnect(string host, int port, string database, string username, string password)
    {
        _connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};";
    }

    public IDbConnection GetConnection()
    {
        try
        {
            return new NpgsqlConnection(_connectionString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PG] Erro ao criar a conexão: {ex.Message}");
            throw;
        }
    }

    public void Connect()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        try
        {
            connection.Open();
            Console.WriteLine("[PG] Conexão estabelecida com sucesso!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PG] Erro ao conectar ao banco de dados: {ex.Message}");
        }
    }

    public async Task<T> ExecuteScalarAsync<T>(string query, object parameters = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(query, connection);
        AddParameters(command, parameters);

        try
        {
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? default : (T)Convert.ChangeType(result, typeof(T));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PG] Erro ao executar consulta escalar assíncrona: {ex.Message}");
            return default;
        }
    }

    public async Task<int> ExecuteInsertAsync(string query, object parameters)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(query, connection);
        AddParameters(command, parameters);

        try
        {
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? -1 : Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PG] Erro ao executar inserção assíncrona: {ex.Message}");
            return -1;
        }
    }

    public async Task ExecuteAsync(string query, object parameters = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(query, connection);
        AddParameters(command, parameters);

        try
        {
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("[PG] Comando executado com sucesso!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PG] Erro ao executar comando assíncrono: {ex.Message}");
        }
    }

    private void AddParameters(NpgsqlCommand command, object parameters)
    {
        if (parameters == null) return;

        foreach (var prop in parameters.GetType().GetProperties())
        {
            var value = prop.GetValue(parameters) ?? DBNull.Value;
            command.Parameters.AddWithValue($"@{prop.Name}", value);
        }
    }

    public void ExecuteCommand(string query)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            try
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PG] Erro ao executar comando no banco de dados: {ex.Message}");
            }
        }
    }

    public int ExecuteInsert(string query, object parameters)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            try
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    foreach (var prop in parameters.GetType().GetProperties())
                    {
                        command.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(parameters) ?? DBNull.Value);
                    }

                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PG] Erro ao executar inserção no banco de dados: {ex.Message}");
                return -1;
            }
        }
    }

    public T ExecuteScalar<T>(string query, object parameters)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            try
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    foreach (var prop in parameters.GetType().GetProperties())
                    {
                        command.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(parameters) ?? DBNull.Value);
                    }

                    var result = command.ExecuteScalar();
                    return result == DBNull.Value ? default : (T)Convert.ChangeType(result, typeof(T));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PG] Erro ao executar consulta escalar no banco de dados: {ex.Message}");
                return default;
            }
        }
    }

    public void Execute(string query, object parameters = null)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            try
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        foreach (var prop in parameters.GetType().GetProperties())
                        {
                            command.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(parameters) ?? DBNull.Value);
                        }
                    }

                    command.ExecuteNonQuery();
                    Console.WriteLine("[PG] Comando executado com sucesso!\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PG] Erro ao executar o comando no banco de dados: {ex.Message}");
            }
        }
    }
}