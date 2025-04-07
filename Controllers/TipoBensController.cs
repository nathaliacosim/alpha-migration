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

public class TipoBensController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase = "https://patrimonio.betha.cloud/patrimonio-services/api/tipos-bem";

    public TipoBensController(PgConnect pgConnect, string token)
    {
        _pgConnect = pgConnect;
        _token = token;
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task<List<TipoBem>> SelecionarTiposBensSemIdCloud()
    {
        try
        {
            var query = "SELECT * FROM pat_tp_bens WHERE id_cloud is null;";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<TipoBem>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} tipos de bens sem ID Cloud foram encontrados!");
            return dados;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao buscar os tipos de bens: {e.Message}");
            return new List<TipoBem>();
        }
    }

    public async Task EnviarTiposBensParaCloud()
    {
        var tiposBens = await SelecionarTiposBensSemIdCloud();
        if (!tiposBens.Any())
        {
            Console.WriteLine("❌ Nenhum tipo de bem sem ID Cloud encontrado!");
            return;
        }
        foreach (var tipoBem in tiposBens)
        {
            Console.WriteLine($"📡 Enviando o tipo de bem {tipoBem.codigo} para o Cloud Patrimônio...");
            var classificacao = StringHelper.RemoverAcentos(tipoBem.descricao);
            var tipoBemPost = new TipoBemPOST
            {
                descricao = tipoBem.descricao.Trim().ToUpper(),
                classificacao = new ClassificacaoTipoBemPOST
                {
                    valor = classificacao.ToUpper()
                }
            };

            var json = JsonConvert.SerializeObject(tipoBemPost);
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
                    Console.WriteLine($"❌ Erro ao enviar o tipo de bem {tipoBem.codigo}: {response}");
                    continue;
                }
                var query = $"UPDATE pat_tp_bens SET id_cloud = '{id_cloud}' WHERE codigo = {tipoBem.codigo};";
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Tipo de bem {tipoBem.codigo} enviado com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao enviar o tipo de bem {tipoBem.codigo}: {e.Message}");
            }
        }
    }
}