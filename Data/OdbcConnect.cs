using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Data;

public class OdbcConnect
{
    private readonly string _connectionString;

    public OdbcConnect(string dsn)
    {
        if (string.IsNullOrEmpty(dsn))
        {
            throw new ArgumentException("[ODBC] Os parâmetros de conexão não podem ser nulos ou vazios.");
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
            Console.WriteLine($"[ODBC] Erro ao criar a conexão: {ex.Message}");
            throw;
        }
    }

    public void Connect()
    {
        using (var connection = new OdbcConnection(_connectionString))
        {
            try
            {
                connection.Open();
                Console.WriteLine("[ODBC] Conexão estabelecida com sucesso!\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ODBC] Erro ao conectar ao banco de dados: {ex.Message}");
            }
        }
    }

    public bool ExecuteCommand(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            Console.WriteLine("[ODBC] A consulta não pode ser vazia.");
            return false;
        }

        try
        {
            using (var connection = new OdbcConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OdbcCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                    Console.WriteLine("[ODBC] Comando executado com sucesso!\n");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ODBC] Erro ao executar comando no banco de dados: {ex.Message}");
            return false;
        }
    }

    public bool ExecuteQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            Console.WriteLine("[ODBC] A consulta não pode ser vazia.\n");
            return false;
        }

        try
        {
            using (var connection = new OdbcConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OdbcCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    Console.WriteLine("[ODBC] Resultados da consulta:");
                    if (!reader.HasRows)
                    {
                        Console.WriteLine("[ODBC] Nenhum resultado encontrado.");
                        return false;
                    }

                    while (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            Console.Write($"{reader.GetName(i)}: {reader.GetValue(i)}\n");
                        }
                        Console.WriteLine();
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ODBC] Erro ao executar consulta no banco de dados: {ex.Message}");
            return false;
        }
    }
}