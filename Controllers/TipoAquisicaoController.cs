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

public class TipoAquisicaoController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/tipos-aquisicao";

    public TipoAquisicaoController(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task<List<TipoAquisicao>> SelecionarTiposAquisicaoSemIdCloud()
    {
        try
        {
            var query = "SELECT * FROM pat_tp_origem WHERE id_cloud is null;";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<TipoAquisicao>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} tipos de aquisição sem ID Cloud foram encontrados!");
            return dados;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao buscar os tipos de aquisição: {e.Message}");
            return new List<TipoAquisicao>();
        }
    }

    public async Task EnviarTiposAquisicaoParaCloud()
    {
        var tiposAquisicao = await SelecionarTiposAquisicaoSemIdCloud();
        if (!tiposAquisicao.Any())
        {
            Console.WriteLine("❌ Nenhum tipo de aquisição sem ID Cloud encontrado!");
            return;
        }

        foreach (var tipoAquisicao in tiposAquisicao)
        {
            Console.WriteLine($"📡 Enviando o tipo de aquisição {tipoAquisicao.codigo} para o Cloud Patrimônio...");
            var tipoAquisicaoPost = new TipoAquisicaoPOST
            {
                descricao = tipoAquisicao.descricao.Trim().ToUpper(),
                classificacao = new ClassificacaoTipoAquisicaoPOST
                {
                    valor = tipoAquisicao.classificacao,
                    descricao = tipoAquisicao.classificacao
                }
            };

            var json = JsonConvert.SerializeObject(tipoAquisicaoPost);
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
                    Console.WriteLine($"❌ Erro ao enviar o tipo de aquisição {tipoAquisicao.codigo}: {response}");
                    continue;
                }
                var query = $"UPDATE pat_tp_origem SET id_cloud = '{id_cloud}' WHERE codigo = {tipoAquisicao.codigo};";
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Tipo de aquisição {tipoAquisicao.codigo} enviado com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao enviar o tipo de aquisição {tipoAquisicao.codigo}: {e.Message}");
            }

        }
    }
}
