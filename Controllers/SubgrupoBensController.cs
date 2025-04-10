using Alpha.Data;
using Alpha.Models.Alpha;
using Alpha.Models.BethaCloud;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class SubgrupoBensController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/especies-bem";

    public SubgrupoBensController(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task<List<SubgrupoBens>> SelecionarSubgruposBensSemIdCloud()
    {
        try
        {
            var query = "SELECT * FROM pat_subgrupo_bens WHERE id_cloud is null;";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<SubgrupoBens>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} subgrupos de bens sem ID Cloud foram encontrados!");
            return dados;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os subgrupos de bens {ex.Message}");
            return new List<SubgrupoBens>();
        }
    }

    public async Task<string> SelecionarIdCloudGrupoBens(int codigo)
    {
        try
        {
            var query = "SELECT id_cloud FROM pat_grupo_bens WHERE codigo = @codigo;";
            using var connection = _pgConnect.GetConnection();
            var idCloud = await connection.ExecuteScalarAsync<string>(query, new { codigo });
            if (string.IsNullOrWhiteSpace(idCloud))
            {
                Console.WriteLine($"⚠️ Nenhum ID Cloud encontrado para o grupo de bens com código = {codigo}.");
                return null;
            }
            Console.WriteLine($"✅ ID Cloud do grupo de bens encontrado: {idCloud}");
            return idCloud;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar o ID Cloud do grupo de bens: {ex.Message}");
            return null;
        }
    }

    public async Task EnviarSubgruposBensParaCloud()
    {
        var subgruposBens = await SelecionarSubgruposBensSemIdCloud();
        if (subgruposBens.Count == 0)
        {
            Console.WriteLine("✅ Nenhum subgrupo de bens para enviar.");
            return;
        }

        foreach (var subgrupo in subgruposBens)
        {
            Console.WriteLine($"📡 Enviando o subgrupo de bens {subgrupo.codigo} para o Cloud Patrimônio...");

            var idCloudGrupoBens = await SelecionarIdCloudGrupoBens(subgrupo.grupo_bens_cod ?? 0);
            if (string.IsNullOrWhiteSpace(idCloudGrupoBens))
            {
                Console.WriteLine($"⚠️ ID Cloud do grupo de bens não encontrado para o subgrupo {subgrupo.descricao}. Pulando envio...");
                continue;
            }

            var especieBemPost = new EspecieBemPOST
            {
                grupoBem = new GrupoBemEspecieBemPOST { id = int.Parse(idCloudGrupoBens) },
                descricao = subgrupo.descricao.Trim().ToUpper()
            };

            var json = JsonConvert.SerializeObject(especieBemPost);
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
                    Console.WriteLine($"❌ Erro ao enviar o subgrupo de bem {subgrupo.codigo}: {response}");
                    continue;
                }
                var query = $"UPDATE pat_subgrupo_bens SET id_cloud = '{id_cloud}' WHERE codigo = {subgrupo.codigo};";
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Subgrupo de bem {subgrupo.codigo} enviado com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao enviar o subgrupo de bem {subgrupo.codigo}: {e.Message}");
            }
        }
    }
}