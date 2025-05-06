using Alpha.Data;
using Alpha.Models.Alpha;
using Alpha.Models.BethaCloud;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class GrupoBensController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/grupos-bem";

    public GrupoBensController(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = $"{urlBase}{_rota}";
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

    public async Task BuscarGrupoBensCloud()
    {
        Console.WriteLine("🔄 Buscando grupos de bens do Cloud Patrimônio...");
        int offset = 0;
        int limit = 500;
        bool controle = true;

        while (controle)
        {
            string urlBusca = $"{_urlBase}?limit={limit}&offset={offset}";
            Console.WriteLine($"📡 Buscando grupos de bens... Offset: {offset}, Limite: {limit}");
            Console.WriteLine($"🔗 URL: {urlBusca}");

            try
            {
                var response = await _httpClient.GetStringAsync(urlBusca);
                Console.WriteLine($"📜 Resposta recebida: {response.Substring(0, Math.Min(response.Length, 100))}...");

                var retorno = JsonConvert.DeserializeObject<GrupoBemGET>(response);
                if (retorno.content == null || retorno?.content?.Count == 0)
                {
                    Console.WriteLine("❌ Nenhum grupo de bens encontrado!");
                    controle = false;
                    break;
                }

                Console.WriteLine($"✅ {retorno.content.Count} grupos de bens encontrados!");
                await Task.WhenAll(retorno.content.Select(InserirGrupoBens));

                if (!retorno.hasNext)
                {
                    Console.WriteLine("🚀 Todos os grupos de bens foram processados!");
                    break;
                }
                offset += limit;
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao buscar grupos de bens: {e.Message}");
                controle = false;
            }
        }
    }

    private async Task InserirGrupoBens(ContentGrupoBemGET grupo)
    {
        if (grupo == null)
        {
            Console.WriteLine("❌ Grupo de bens nulo!");
            return;
        }

        const string queryVerifica = "SELECT COUNT(*) FROM grupo_bem_cloud WHERE id_cloud = @id_cloud;";
        const string queryInsert = @"INSERT INTO grupo_bem_cloud (id_cloud, i_conta, descricao, id_tipo_bem, id_metodo_depreciacao, percentual_depreciacao, percentual_residual, vida_util, sigla_tipo_bem, sigla_tipo_conta, sigla_classif_conta) 
                      VALUES (@id_cloud, @i_conta, @descricao, @id_tipo_bem, @id_metodo_depreciacao, @percentual_depreciacao, @percentual_residual, @vida_util, @sigla_tipo_bem, @sigla_tipo_conta, @sigla_classif_conta);";

        var parametros = new
        {
            id_cloud = grupo.id,
            i_conta = 0,
            descricao = grupo.descricao.Trim(),
            id_tipo_bem = grupo.tipoBem?.id,
            id_metodo_depreciacao = grupo.metodoDepreciacao?.id,
            percentual_depreciacao = grupo.percentualDepreciacaoAnual,
            percentual_residual = grupo.percentualValorResidual,
            vida_util = grupo.vidaUtil,
            sigla_tipo_bem = "",
            sigla_tipo_conta = "",
            sigla_classif_conta = 0
        };

        try
        {
            int count = _pgConnect.ExecuteScalar<int>(queryVerifica, parametros);
            if (count == 0)
            {
                await _pgConnect.ExecuteAsync(queryInsert, parametros);
                Console.WriteLine($"✅ Grupo de bens {grupo.descricao} inserido com sucesso!");
            }
            else
            {
                Console.WriteLine($"⚠️ Grupo de bens {grupo.descricao} já existe no banco de dados!");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao inserir o grupo de bens {grupo.descricao}: {e.Message}");
        }
    }
}