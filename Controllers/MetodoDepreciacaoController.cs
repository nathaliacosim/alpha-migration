using Alpha.Data;
using Alpha.Models.BethaCloud;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class MetodoDepreciacaoController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/metodos-depreciacao";

    public MetodoDepreciacaoController(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task EnviarMetodoDepreciacaoPadrao()
    {
        const string descricaoMetodo = "Linear ou Cotas Constantes";
        const string tipoDepreciacao = "DEPRECIACAO";

        var dados = new MetodoDepreciacaoPOST
        {
            ativo = true,
            descricao = descricaoMetodo,
            classificacao = new ClassificacaoMetodoDepreciacaoPOST
            {
                valor = "LINEAR_OU_COTAS_CONSTANTES"
            },
            tipoDepreciacao = new TipoDepreciacaoPOST
            {
                valor = tipoDepreciacao
            }
        };

        try
        {
            var json = JsonConvert.SerializeObject(dados);
            Console.WriteLine($"📤 JSON: {json}");

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_urlBase, content);

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📄 Resposta da API: {responseContent}");

            if (!response.IsSuccessStatusCode || responseContent.Contains("message"))
            {
                Console.WriteLine($"❌ Erro ao enviar o método de depreciação padrão: {responseContent}");
                return;
            }

            var query = @"INSERT INTO metodo_depreciacao_cloud (id_cloud, descricao, tipo)
                          VALUES (@IdCloud, @Descricao, @Tipo)";

            using var connection = _pgConnect.GetConnection();
            await connection.ExecuteAsync(query, new
            {
                IdCloud = responseContent,
                Descricao = descricaoMetodo,
                Tipo = tipoDepreciacao
            });

            Console.WriteLine("✅ Método de depreciação padrão enviado com sucesso!");
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"❌ Erro HTTP ao enviar método de depreciação: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro inesperado ao enviar método de depreciação: {ex.Message}");
        }
    }
}