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

public class ReavaliacaoController
{
    private readonly PgConnect _pgConnect;
    private readonly OdbcConnect _odbcConnect;
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _urlBase;
    private readonly string _rota = "api/reavaliacoes";

    public ReavaliacaoController(PgConnect pgConnect, string token, string urlBase, OdbcConnect odbcConnect)
    {
        _pgConnect = pgConnect;
        _odbcConnect = odbcConnect;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public async Task<List<ReavaliacaoBethaDba>> SelecionarReavaliacoesBetha()
    {
        const string query = "SELECT * FROM bethadba.reavaliacoes;";
        try
        {
            using var connection = _odbcConnect.GetConnection();
            var reavaliacoes = (await connection.QueryAsync<ReavaliacaoBethaDba>(query)).AsList();
            Console.WriteLine($"✅ {reavaliacoes.Count} reavaliações encontradas!");
            return reavaliacoes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as reavaliações: {ex.Message}");
            return [];
        }
    }

    public async Task InserirReavaliacoes()
    {
        var reavaliacoes = await SelecionarReavaliacoesBetha();
        if (reavaliacoes.Count == 0)
        {
            Console.WriteLine("⚠️ Nenhuma reavaliação encontrada para inserir.");
            return;
        }

        foreach (var reavaliacao in reavaliacoes)
        {
            const string checkExistsQuery = "SELECT COUNT(*) FROM reavaliacao_cabecalho_cloud WHERE i_reavaliacao = @i_reavaliacao;";
            const string insertQuery = @"INSERT INTO reavaliacao_cabecalho_cloud
                                         (id_cloud, i_reavaliacao, i_bem, data_reav, saldo_ant, percentual, nro_portaria, dt_portaria,
                                         matricula_pessoal, valor_calc, motivo_valorizacao, i_comissoes, i_reav_bem, i_entidades)
                                        VALUES (@id_cloud, @i_reavaliacao, @i_bem, @data_reav, @saldo_ant, @percentual, @nro_portaria, @dt_portaria,
                                         @matricula_pessoal, @valor_calc, @motivo_valorizacao, @i_comissoes, @i_reav_bem, @i_entidades);";

            var parametros = new
            {
                id_cloud = "",
                reavaliacao.i_reavaliacao,
                reavaliacao.i_bem,
                reavaliacao.data_reav,
                reavaliacao.saldo_ant,
                reavaliacao.percentual,
                reavaliacao.nro_portaria,
                reavaliacao.dt_portaria,
                reavaliacao.matricula_pessoal,
                reavaliacao.valor_calc,
                reavaliacao.motivo_valorizacao,
                reavaliacao.i_comissoes,
                reavaliacao.i_reav_bem,
                reavaliacao.i_entidades
            };

            try
            {
                var count = await _pgConnect.ExecuteScalarAsync<int>(checkExistsQuery, new { reavaliacao.i_reavaliacao });
                if (count == 0)
                {
                    Console.WriteLine($"🟢 Reavaliação {reavaliacao.i_reavaliacao} não encontrada, inserindo...");
                    await _pgConnect.ExecuteInsertAsync(insertQuery, parametros);
                }
                else
                {
                    Console.WriteLine($"⚠️ Reavaliação {reavaliacao.i_reavaliacao} já existe! Pulando...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao inserir reavaliação: {ex.Message}");
            }
        }
    }

    private async Task<List<ReavaliacaoBethaDba>> SelecionarReavaliacaoBethaSemIdCloud()
    {
        const string query = "SELECT * FROM reavaliacao_cabecalho_cloud WHERE id_cloud IS NULL OR id_cloud = '';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var reavaliacoes = (await connection.QueryAsync<ReavaliacaoBethaDba>(query)).AsList();
            Console.WriteLine($"✅ {reavaliacoes.Count} reavaliações sem ID Cloud foram encontradas!");
            return reavaliacoes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as reavaliações: {ex.Message}");
            return new List<ReavaliacaoBethaDba>();
        }
    }

    public async Task EnviarReavaliacoesBethaParaCloud()
    {
        var dados = await SelecionarReavaliacaoBethaSemIdCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma reavaliação sem ID Cloud encontrada!");
            return;
        }
        foreach (var item in dados)
        {
            try
            {
                await EnviarReavaliacaoBetha(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar a reavaliação {item.id}: {ex.Message}");
            }
        }
    }

    private async Task EnviarReavaliacaoBetha(ReavaliacaoBethaDba reavaliacao)
    {
        var jsonReavaliacao = new ReavaliacaoPost
        {
            dhReavaliacao = DateTime.ParseExact((reavaliacao.data_reav), "MM/dd/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd") + " 00:00:00",
            criterioFundamentacao = reavaliacao.motivo_valorizacao ?? "NÃO INFORMADO",
            responsavel = new Responsavel
            {
                id = 43537257
            }
        };

        var json = JsonConvert.SerializeObject(jsonReavaliacao);
        Console.WriteLine($"📤 Enviando reavaliação {reavaliacao.id} para a nuvem...");

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_urlBase, content);

        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"📄 Resposta da API: {responseBody}");

        if (!responseBody.Contains("message"))
        {
            Console.WriteLine("✅ Reavaliação enviada com sucesso!");

            var query = $"UPDATE reavaliacao_cabecalho_cloud SET id_cloud = '{responseBody}' WHERE id = {reavaliacao.id};";
            await _pgConnect.ExecuteNonQueryAsync(query);
        } else
        {
            Console.WriteLine($"❌ Erro ao enviar reavaliação para a nuvem: {responseBody}");
        }
    }
}