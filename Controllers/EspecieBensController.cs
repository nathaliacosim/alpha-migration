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

public class EspecieBensController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/especies-bem";

    public EspecieBensController(PgConnect pgConnect, string token, string urlBase)
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

    public async Task BuscarEspeciesBensCloud()
    {
        Console.WriteLine("🔄 Buscando espécies de bens na nuvem...");
        int offset = 0;
        int limit = 100;
        bool controle = true;

        while (controle)
        {
            string urlBusca = $"{_urlBase}?limit={limit}&offset={offset}";
            Console.WriteLine($"📡 Buscando espécies de bens... Offset: {offset}, Limite: {limit}");
            Console.WriteLine($"🔗 URL: {urlBusca}");
            try
            {
                var response = await _httpClient.GetStringAsync(urlBusca);
                Console.WriteLine($"📜 Resposta recebida: {response.Substring(0, Math.Min(response.Length, 500))}...");

                var retorno = JsonConvert.DeserializeObject<EspecieBemGET>(response);
                if (retorno.content == null || retorno?.content?.Count == 0)
                {
                    Console.WriteLine("❌ Nenhuma espécie de bens encontrada!");
                    controle = false;
                    break;
                }

                Console.WriteLine($"✅ {retorno.content.Count} espécies de bens encontradas!");
                await Task.WhenAll(retorno.content.Select(InserirEspecieBens));

                if (!retorno.hasNext)
                {
                    Console.WriteLine("🚀 Todas as espécies de bens foram processadas!");
                    break;
                }
                offset += limit;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao buscar espécies de bens: {ex.Message}");
                controle = false;
                break;
            }
        }
    }

    private async Task InserirEspecieBens(ContentEspecieBemGET dados)
    {
        if (dados == null)
        {
            Console.WriteLine("⚠️ Dados da espécie de bens estão nulos.");
            return;
        }

        const string queryVerifica = "SELECT COUNT(*) FROM especie_bem_cloud WHERE id_cloud = @id_cloud;";
        const string queryInsert = @"INSERT INTO especie_bem_cloud (id_cloud, id_grupo_bem, i_conta, i_chave, tipo_chave, descricao)
                                     VALUES (@id_cloud, @id_grupo_bem, @i_conta, @i_chave, @tipo_chave, @descricao);";

        Console.WriteLine("🔄 Inserindo espécies de bens...");

        var parametros = new
        {
            id_cloud = dados.id.ToString(),
            id_grupo_bem = dados.grupoBem.id,
            i_conta = 0,
            i_chave = 0,
            tipo_chave = "C",
            descricao = dados.descricao.Trim().ToUpper()
        };

        try
        {
            using var connection = _pgConnect.GetConnection();
            int count = await connection.ExecuteScalarAsync<int>(queryVerifica, new { id_cloud = dados.id.ToString() });
            if (count == 0)
            {
                await connection.ExecuteAsync(queryInsert, parametros);
                Console.WriteLine($"✅ Espécie de bens {dados.descricao} inserida com sucesso!");
            }
            else
            {
                Console.WriteLine($"⚠️ Espécie de bens {dados.descricao} já existe no banco de dados!");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao inserir a espécie de bens {dados.descricao}: {e.Message}");
        }
    }
}