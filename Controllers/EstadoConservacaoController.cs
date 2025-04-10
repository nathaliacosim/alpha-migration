using Alpha.Data;
using Alpha.Models.Alpha;
using Alpha.Models.BethaCloud;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class EstadoConservacaoController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/estados-conservacao";

    public EstadoConservacaoController(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task<List<EstadoConservacao>> SelecionarEstadosConservacaoSemIdCloud()
    {
        try
        {
            var query = "SELECT * FROM pat_tp_status WHERE id_cloud is null;";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<EstadoConservacao>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} estados de conservação sem ID Cloud foram encontrados!");
            return dados;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os estados de conservação {ex.Message}");
            return new List<EstadoConservacao>();
        }
    }

    public async Task EnviarEstadosConservacaoParaCloud()
    {
        var estadosConservacao = await SelecionarEstadosConservacaoSemIdCloud();
        if (!estadosConservacao.Any())
        {
            Console.WriteLine("❌ Nenhum estado de conservação sem ID Cloud encontrado!");
            return;
        }

        foreach (var estado in estadosConservacao)
        {
            Console.WriteLine($"📡 Enviando estado de conservação {estado.descricao} para o Cloud Patrimônio...");
            var estadoPost = new EstadoConservacaoPOST
            {
                descricao = estado.descricao.Trim().ToUpper()
            };

            var json = JsonConvert.SerializeObject(estadoPost);
            Console.WriteLine($"📤 JSON: {json}");
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            try
            {
                var enviarDados = await _httpClient.PostAsync(_urlBase, content);
                var response = await enviarDados.Content.ReadAsStringAsync();

                var id_cloud = response.ToString();
                Console.WriteLine($"📄 Resposta da API: {response}");
                if (response.Contains("message"))
                {
                    Console.WriteLine($"❌ Erro ao enviar o estado de conservação do bem {estado.codigo}: {response}");
                    continue;
                }
                var query = $"UPDATE pat_tp_status SET id_cloud = '{id_cloud}' WHERE codigo = {estado.codigo};";
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Estado de conservação do bem {estado.codigo} enviado com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao enviar o estado de conservação do bem {estado.codigo}: {e.Message}");
            }
        }
    }
}