using Alpha.Data;
using Alpha.Models.Alpha;
using Alpha.Models.BethaCloud;
using Alpha.Utils;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class BaixaBensController
{
    private readonly PgConnect _pgConnect;
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;

    public BaixaBensController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper)
    {
        _pgConnect = pgConnect ?? throw new ArgumentNullException(nameof(pgConnect));
        _sqlHelper = sqlHelper ?? throw new ArgumentNullException(nameof(sqlHelper));
        _urlBase = !string.IsNullOrWhiteSpace(urlBase) ? urlBase : throw new ArgumentException("URL base inválida.", nameof(urlBase));

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public async Task<List<Baixa>> SelecionarBaixas()
    {
        const string query = "SELECT * FROM pat_baixas WHERE id_cloud IS NOT NULL ORDER BY data_baixa;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            return (await connection.QueryAsync<Baixa>(query)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar baixas: {ex.Message}");
            return new();
        }
    }

    private async Task<List<BaixaBens>> ObterBensDaBaixaAsync(string idCabecalho)
    {
        const string query = "SELECT * FROM pat_atualiza_bens WHERE id_cloud_baixa = @id_cabecalho;";
        using var connection = _pgConnect.GetConnection();
        return (await connection.QueryAsync<BaixaBens>(query, new { id_cabecalho = idCabecalho })).ToList();
    }

    public async Task EnviarBaixaBensParaCloud()
    {
        var cabecalhos = await SelecionarBaixas();

        if (!cabecalhos.Any())
        {
            Console.WriteLine("⚠️ Nenhuma baixa encontrada no banco.");
            return;
        }

        foreach (var cabecalho in cabecalhos)
        {
            Console.WriteLine($"\n🛠️ Processando bens da baixa {cabecalho.id}...");

            var bens = await ObterBensDaBaixaAsync(cabecalho.id_cloud);

            if (bens is null || !bens.Any())
            {
                Console.WriteLine($"❌ Nenhum bem encontrado para {cabecalho.id}.");
                continue;
            }

            Console.WriteLine($"📦 {bens.Count} bens encontrados.");

            int enviados = 0;

            foreach (var bem in bens)
            {
                Console.WriteLine($"🚀 Enviando bem {bem.codigo}...");
                if (await EnviarBemBaixadoAsync(bem))
                {
                    enviados++;
                    Console.WriteLine($"📈 Progresso: {enviados}/{bens.Count} bens enviados.");
                }

                Console.WriteLine("\n");
            }
        }
    }

    private async Task<bool> EnviarBemBaixadoAsync(BaixaBens bem, int tentativas = 0)
    {
        const int maxTentativas = 3;

        var payload = new BaixaBensPOST
        {
            baixa = new BaixaIdPOST
            {
                id = int.Parse(bem.id_cloud_baixa)
            },
            bem = new BemBaixaBensPOST
            {
                id = int.Parse(bem.id_cloud_bem)
            },
            notaExplicativa = ""
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = $"{_urlBase}api/baixas/{bem.id_cloud_baixa}/bens";
        Console.WriteLine($"🔗 Enviando para: {url}");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"📄 Resposta da API para o bem {bem.codigo}: {responseBody}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Bem {bem.codigo} enviado com sucesso.");

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"⚠️ Resposta da API está vazia. Não é possível atualizar o banco.");
                    return false;
                }

                var queryUp = @"UPDATE pat_atualiza_bens SET id_cloud = @IdCloud WHERE codigo = @Codigo;";
                var parameters = new
                {
                    IdCloud = responseBody,
                    Codigo = bem.codigo
                };

                using var connection = _pgConnect.GetConnection();
                var rowsAffected = await connection.ExecuteAsync(queryUp, parameters);

                if (rowsAffected > 0)
                {
                    Console.WriteLine($"💾 Registro do bem {bem.codigo} atualizado com id_cloud = '{responseBody}'.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"⚠️ Nenhum registro foi atualizado para o bem {bem.codigo}.");
                    return false;
                }
            }

            Console.WriteLine($"❌ Falha ao enviar bem {bem.codigo}: {response.StatusCode}");

            if (tentativas < maxTentativas)
            {
                Console.WriteLine($"🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                await Task.Delay(3000);
                return await EnviarBemBaixadoAsync(bem, tentativas + 1);
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao enviar bem {bem.codigo}: {ex.Message}");
            return false;
        }
    }
}