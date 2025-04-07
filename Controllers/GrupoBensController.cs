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

public class GrupoBensController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase = "https://patrimonio.betha.cloud/patrimonio-services/api/grupos-bem";

    public GrupoBensController(PgConnect pgConnect, string token)
    {
        _pgConnect = pgConnect;
        _token = token;
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task<List<GrupoBens>> SelecionarGruposBensSemIdCloud()
    {
        try
        {
            var query = "SELECT * FROM pat_grupo_bens WHERE id_cloud is null;";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<GrupoBens>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} grupos de bens sem ID Cloud foram encontrados!");
            return dados;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os grupos de bens {ex.Message}");
            return new List<GrupoBens>();
        }
    }

    public async Task<string> SelecionarIdCloudMetodoDepreciacao()
    {
        try
        {
            const string query = "SELECT id_cloud FROM metodo_depreciacao_cloud WHERE id = 1;";
            using var connection = _pgConnect.GetConnection();
            var idCloud = await connection.ExecuteScalarAsync<string>(query);

            if (string.IsNullOrWhiteSpace(idCloud))
            {
                Console.WriteLine("⚠️ Nenhum ID Cloud encontrado para o método de depreciação com ID = 1.");
                return null;
            }

            Console.WriteLine($"✅ ID Cloud do método de depreciação encontrado: {idCloud}");
            return idCloud;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar o ID Cloud do método de depreciação: {ex.Message}");
            return null;
        }
    }


    public async Task<string> SelecionarIdCloudTipoBem(int codigo)
    {
        try
        {
            const string query = @"SELECT id_cloud FROM pat_tp_bens WHERE codigo = @codigo;";
            using var connection = _pgConnect.GetConnection();
            var idCloud = await connection.ExecuteScalarAsync<string>(query, new { codigo });

            if (string.IsNullOrWhiteSpace(idCloud))
            {
                Console.WriteLine($"⚠️ Nenhum ID Cloud encontrado para o tipo de bem com código = {codigo}.");
                return null;
            }

            Console.WriteLine($"✅ ID Cloud do tipo de bem encontrado: {idCloud}");
            return idCloud;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar o ID Cloud do tipo de bem (código: {codigo}): {ex.Message}");
            return null;
        }
    }



    public async Task EnviarGruposBensParaCloud()
    {
        var idMetodoDepreciacao = await SelecionarIdCloudMetodoDepreciacao();
        var gruposBens = await SelecionarGruposBensSemIdCloud();
        if (!gruposBens.Any())
        {
            Console.WriteLine("❌ Nenhum grupo de bens sem ID Cloud encontrado!");
            return;
        }

        foreach (var grupo in gruposBens)
        {
            Console.WriteLine($"📡 Enviando o grupo de bens {grupo.codigo} para o Cloud Patrimônio...");
            var idTipoBem = await SelecionarIdCloudTipoBem(grupo.tp_bens_cod ?? 0);

            var grupoPost = new GrupoBemPOST
            {
                descricao = grupo.descricao.Trim().ToUpper(),
                tipoBem = new TipoBemGrupoBemPOST
                {
                    id = int.Parse(idTipoBem)
                },
                metodoDepreciacao = new MetodoDepreciacaoGrupoBemPOST
                {
                    id = int.Parse(idMetodoDepreciacao)
                },
                percentualDepreciacaoAnual = grupo.deprecia ?? null,
                percentualValorResidual = null
            };

            var json = JsonConvert.SerializeObject(grupoPost);
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
                    Console.WriteLine($"❌ Erro ao enviar o grupo de bem {grupo.codigo}: {response}");
                    continue;
                }
                var query = $"UPDATE pat_grupo_bens SET id_cloud = '{id_cloud}' WHERE codigo = {grupo.codigo};";
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Grupo de bem {grupo.codigo} enviado com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao enviar o grupo de bem {grupo.codigo}: {e.Message}");
            }
        }
    }
}
