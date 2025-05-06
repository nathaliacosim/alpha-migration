using Alpha.Data;
using Alpha.UseCase;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Alpha;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var config = LoadConfiguration();
        var tokenConversao = config["TokenConversao"];
        var odbcConnection = ConfigureOdbc(config);
        var pgConnection = ConfigurePostgres(config);
        var urlBase = "https://services.patrimonio.betha.cloud/patrimonio-services/";

        Console.WriteLine("🔧 Tratamento de dados... 🔄");
        await new ProcesssaDados(pgConnection, odbcConnection, tokenConversao, urlBase).Executar();

        Console.WriteLine("✅ Processo finalizado com sucesso!");
    }

    private static IConfiguration LoadConfiguration()
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                                               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                               .Build();

        Console.WriteLine($"🌍 Host: {config["Postgres:Host"]}");
        Console.WriteLine($"📍 Porta: {config["Postgres:Port"]}");
        Console.WriteLine($"📚 Banco de Dados: {config["Postgres:Database"]}");
        Console.WriteLine($"🔑 Usuário: {config["Postgres:Username"]}");

        return config;
    }

    private static OdbcConnect ConfigureOdbc(IConfiguration config)
    {
        string dsn = config["ODBC:DSN"];
        Console.WriteLine($"🔌 Iniciando conexão ODBC ao DNS: {dsn}... ⏳");

        var connection = new OdbcConnect(dsn);
        connection.GetConnection();
        return connection;
    }

    private static PgConnect ConfigurePostgres(IConfiguration config)
    {
        string host = config["Postgres:Host"];
        int port = int.Parse(config["Postgres:Port"]);
        string database = config["Postgres:Database"];
        string username = config["Postgres:Username"];
        string password = config["Postgres:Password"];

        Console.WriteLine($"🔑 Iniciando conexão Postgres ao DB: {database}... 🔄");

        var connection = new PgConnect(host, port, database, username, password);
        connection.Connect();
        return connection;
    }
}