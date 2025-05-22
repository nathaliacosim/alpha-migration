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

public class TipoBaixaController
{
    private readonly PgConnect _pgConnect;
    private readonly OdbcConnect _odbcConnect;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private const string _rota = "api/tipos-baixa";

    public TipoBaixaController(PgConnect pgConnect, string token, string urlBase, OdbcConnect odbcConnect)
    {
        _pgConnect = pgConnect;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _odbcConnect = odbcConnect;
    }

    private async Task<List<TipoBaixaBethaDba>> SelecionarTiposBaixasBetha()
    {
        const string query = @"SELECT * FROM bethadba.motivos;";
        try
        {
            using var connection = _odbcConnect.GetConnection();
            var baixas = (await connection.QueryAsync<TipoBaixaBethaDba>(query)).ToList();
            Console.WriteLine($"✅ {baixas.Count} tipos de baixas encontradas!");
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os tipos de baixas: {ex.Message}");
            return new List<TipoBaixaBethaDba>();
        }
    }

    public async Task InserirTiposBaixasBetha()
    {
        var dados = await SelecionarTiposBaixasBetha();
        if (dados.Count == 0)
        {
            Console.WriteLine("⚠️ Nenhum tipo de baixa encontrado.");
            return;
        }

        foreach (var item in dados)
        {
            const string checkExistsQuery = @"SELECT COUNT(1) FROM tipo_baixa_cloud WHERE i_motivo = @i_motivo;";
            const string insertQuery = @"INSERT INTO tipo_baixa_cloud (id_cloud, i_motivo, descricao, classificacao, i_entidades)
                                         VALUES (@id_cloud, @i_motivo, @descricao, @classificacao, @i_entidades);";

            var parametros = new
            {
                id_cloud = "",
                item.i_motivo,
                descricao = item.descricao?.Trim() ?? null,
                classificacao = "OUTROS",
                item.i_entidades
            };

            try
            {
                int count = _pgConnect.ExecuteScalar<int>(checkExistsQuery, new { item.i_motivo });

                if (count == 0)
                {
                    _pgConnect.Execute(insertQuery, parametros);
                    Console.WriteLine($"✅ Registro {item.i_motivo} inserido com sucesso! 🎉");
                }
                else
                {
                    Console.WriteLine($"⚠️ Registro {item.i_motivo} já existe no banco.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao inserir tipo_baixa_cloud (ID {item.i_motivo}): {ex.Message}");
            }
        }
    }

    private async Task<List<TipoBaixaBetha>> SelecionarTiposBaixasBethaSemIdCloud()
    {
        const string query = "SELECT * FROM tipo_baixa_cloud WHERE id_cloud IS NULL OR id_cloud = '';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<TipoBaixaBetha>(query)).ToList();
            Console.WriteLine($"✅ {baixas.Count} os tipos de baixas sem ID Cloud foram encontradas!");
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os tipos de baixas: {ex.Message}");
            return new List<TipoBaixaBetha>();
        }
    }

    public async Task EnviarTiposBaixasBethaParaCloud()
    {
        var dados = await SelecionarTiposBaixasBethaSemIdCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhum tipo de baixa sem ID Cloud encontrada!");
            return;
        }

        foreach (var item in dados)
        {
            try
            {
                await EnviarTiposBaixasBetha(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar o tipo de baixa {item.id}: {ex.Message}");
            }
        }
    }

    private async Task EnviarTiposBaixasBetha(TipoBaixaBetha item)
    {
        var jsonBaixa = new TipoBaixaRootPOST
        {
            descricao = item.descricao.Trim().ToUpper(),
            classificacao = new ClassificacaoTipoBaixaPOST
            {
                valor = item.classificacao,
                descricao = item.classificacao
            }
        };

        var json = JsonConvert.SerializeObject(jsonBaixa);
        Console.WriteLine($"📤 Enviando tipo de baixa {item.id} para a nuvem...");

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_urlBase, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"📄 Resposta da API: {responseBody}");

        if (responseBody.Contains("message"))
        {
            Console.WriteLine($"❌ Erro ao enviar o tipo de baixa {item.id}: {responseBody}");
        }

        var query = $"UPDATE tipo_baixa_cloud SET id_cloud = '{responseBody}' WHERE id = {item.id};";
        await _pgConnect.ExecuteNonQueryAsync(query);

        Console.WriteLine($"✅ Tipo de baixa {item.id} enviada com sucesso!");
    }
}
