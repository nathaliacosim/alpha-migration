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
            Console.WriteLine($"❌ Erro ao criar a conexão: {ex.Message}");
            throw;
        }
    }

    public void Connect()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        try
        {
            connection.Open();
            Console.WriteLine("🟢 Conexão estabelecida com sucesso!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao conectar ao banco de dados: {ex.Message}");
        }
    }

    public async Task<int> ExecuteNonQueryAsync(string query, object parameters = null)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(query, connection);
            AddParameters(command, parameters);

            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro ao executar comando: {ex.Message}");
            return 0;
        }
    }

    public async Task<T> ExecuteScalarAsync<T>(string query, object parameters = null)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(query, connection);
            AddParameters(command, parameters);

            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? default : (T)Convert.ChangeType(result, typeof(T));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro ao executar consulta escalar: {ex.Message}");
            return default;
        }
    }

    public async Task<int> ExecuteInsertAsync(string query, object parameters)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(query, connection);
            AddParameters(command, parameters);

            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? -1 : Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro ao inserir dados (async): {ex.Message}");
            return -1;
        }
    }

    public async Task ExecuteAsync(string query, object parameters = null)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(query, connection);
            AddParameters(command, parameters);

            await command.ExecuteNonQueryAsync();
            Console.WriteLine("✅ Comando executado com sucesso (async)!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao executar comando (async): {ex.Message}");
        }
    }

    public void Execute(string query, object parameters = null)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(query, connection);
            AddParameters(command, parameters);

            command.ExecuteNonQuery();
            Console.WriteLine("✅ Comando executado com sucesso!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao executar comando: {ex.Message}");
        }
    }

    public int ExecuteInsert(string query, object parameters)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(query, connection);
            AddParameters(command, parameters);

            var result = command.ExecuteScalar();
            return result == DBNull.Value ? -1 : Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro ao inserir dados: {ex.Message}");
            return -1;
        }
    }

    public T ExecuteScalar<T>(string query, object parameters)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(query, connection);
            AddParameters(command, parameters);

            var result = command.ExecuteScalar();
            return result == DBNull.Value ? default : (T)Convert.ChangeType(result, typeof(T));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro ao executar consulta escalar: {ex.Message}");
            return default;
        }
    }

    public void ExecuteCommand(string query)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(query, connection);
            command.ExecuteNonQuery();
            Console.WriteLine("📥 Comando SQL executado com sucesso!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao executar comando direto: {ex.Message}");
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
}