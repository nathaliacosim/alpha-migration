using System;
using System.Data;
using System.Data.Odbc;
using System.Threading.Tasks;

namespace Alpha.Data
{
    public class OdbcConnect
    {
        private readonly string _connectionString;

        public OdbcConnect(string dsn)
        {
            if (string.IsNullOrEmpty(dsn))
            {
                throw new ArgumentException("⚠️ [ODBC] Os parâmetros de conexão não podem ser nulos ou vazios.");
            }

            _connectionString = $"DSN={dsn}";
        }

        public IDbConnection GetConnection()
        {
            try
            {
                return new OdbcConnection(_connectionString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ODBC] Erro ao criar a conexão: {ex.Message}");
                throw;
            }
        }

        public async Task ConnectAsync()
        {
            using (var connection = new OdbcConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    Console.WriteLine("✅ [ODBC] Conexão estabelecida com sucesso!\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [ODBC] Erro ao conectar ao banco de dados: {ex.Message}");
                }
            }
        }

        public async Task<bool> ExecuteCommandAsync(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                Console.WriteLine("⚠️ [ODBC] A consulta não pode ser vazia.");
                return false;
            }

            try
            {
                using (var connection = new OdbcConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OdbcCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                        Console.WriteLine("✅ [ODBC] Comando executado com sucesso!\n");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ODBC] Erro ao executar comando no banco de dados: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExecuteQueryAsync(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                Console.WriteLine("⚠️ [ODBC] A consulta não pode ser vazia.\n");
                return false;
            }

            try
            {
                using (var connection = new OdbcConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OdbcCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        Console.WriteLine("🔍 [ODBC] Resultados da consulta:");

                        if (!reader.HasRows)
                        {
                            Console.WriteLine("⚠️ [ODBC] Nenhum resultado encontrado.");
                            return false;
                        }

                        while (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Console.WriteLine($"{reader.GetName(i)}: {reader.GetValue(i)}");
                            }
                            Console.WriteLine();
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ODBC] Erro ao executar consulta no banco de dados: {ex.Message}");
                return false;
            }
        }
    }
}