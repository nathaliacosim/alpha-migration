using Alpha.Data;
using Alpha.Models.Alpha;
using Alpha.Models.BethaCloud;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class TipoUtilizacaoController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/tipos-utilizacao-bem";

    public TipoUtilizacaoController(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task<List<TipoUtilizacao>> SelecionarTiposUtilizacaoSemIdCloud()
    {
        try
        {
            var query = "SELECT * FROM tp_classificacao WHERE id_cloud is null;";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<TipoUtilizacao>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} tipos de utilização sem ID Cloud foram encontrados!");
            return dados;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao buscar os tipos de utilização: {e.Message}");
            return new List<TipoUtilizacao>();
        }
    }

    public async Task EnviarTiposUtilizacaoParaCloud()
    {
        var tiposUtilizacao = await SelecionarTiposUtilizacaoSemIdCloud();
        if (!tiposUtilizacao.Any())
        {
            Console.WriteLine("❌ Nenhum tipo de utilização sem ID Cloud encontrado!");
            return;
        }

        foreach (var tipoUtilizacao in tiposUtilizacao)
        {
            Console.WriteLine($"📡 Enviando o tipo de utilização {tipoUtilizacao.codigo} para o Cloud Patrimônio...");
            var tipoUtilizacaoPost = new TipoUtilizacaoPOST
            {
                descricao = tipoUtilizacao.descricao.Trim().ToUpper(),
                classificacao = new ClassificacaoTipoUtilizacaoPOST
                {
                    valor = tipoUtilizacao.classificacao,
                    descricao = tipoUtilizacao.classificacao
                }
            };

            var json = JsonConvert.SerializeObject(tipoUtilizacaoPost);
            Console.WriteLine($"📤 JSON: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var enviarDados = await _httpClient.PostAsync(_urlBase, content);
                var response = await enviarDados.Content.ReadAsStringAsync();

                var id_cloud = response.ToString();
                Console.WriteLine($"📄 Resposta da API: {response}");
                if (response.Contains("message"))
                {
                    Console.WriteLine($"❌ Erro ao enviar o tipo de utilização {tipoUtilizacao.codigo}: {response}");
                    continue;
                }
                var query = $"UPDATE tp_classificacao SET id_cloud = '{id_cloud}' WHERE codigo = {tipoUtilizacao.codigo};";
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Tipo de utilização {tipoUtilizacao.codigo} enviado com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao enviar o tipo de utilização {tipoUtilizacao.codigo}: {e.Message}");
            }
        }
    }
}