using Alpha.Data;
using Alpha.Models.BethaCloud;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class OrganogramaController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/organogramas";

    public OrganogramaController(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task BuscarOrganogramasCloud()
    {
        int offset = 0;
        int limit = 500;
        Console.WriteLine("🔎 Iniciando busca dos organogramas...");
        var controle = true;

        while (controle)
        {
            string urlBusca = $"{_urlBase}?limit={limit}&offset={offset}";
            Console.WriteLine($"📡 Buscando organogramas... Offset: {offset}, Limite: {limit}");

            try
            {
                Console.WriteLine($"🔍 Fazendo requisição para: {urlBusca}");
                var response = await _httpClient.GetStringAsync(urlBusca);
                Console.WriteLine($"📜 Resposta recebida: {response.Substring(0, Math.Min(response.Length, 100))}...");
                var retorno = JsonConvert.DeserializeObject<OrganogramaGET>(response);

                if (retorno?.content == null || retorno?.content?.Count == 0)
                {
                    Console.WriteLine("✅ Busca concluída! Nenhum organograma encontrado na última requisição.");
                    controle = false;
                    break;
                }

                Console.WriteLine($"📥 {retorno.content.Count} organogramas recebidos. Inserindo no banco...");
                await Task.WhenAll(retorno.content.Select(InserirOrganogramaCloud));

                if (!retorno.hasNext)
                {
                    Console.WriteLine("🚀 Todos os organogramas foram processados!");
                    break;
                }
                offset += limit;
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao buscar organogramas: {e.Message}");
                Console.WriteLine($"🛠 StackTrace: {e.StackTrace}");
                break;
            }
        }
    }

    public async Task InserirOrganogramaCloud(ContentOrganogramaGET dados)
    {
        if (dados == null)
        {
            Console.WriteLine("❌ Os dados dos organogramas estão nulos! Pulando...");
            return;
        }

        if (dados.configuracaoOrganograma.id != 15985) return;

        const string checkExistsQuery = "SELECT COUNT(1) FROM organogramas_cloud_base WHERE id_cloud = @id_cloud";
        const string insertQuery = "INSERT INTO organogramas_cloud_base (id_cloud, descricao, numero, nivel, config_id, orgao, unidade, centro_custo) VALUES (@id_cloud, @descricao, @numero, @nivel, @config_id, @orgao, @unidade, @centro_custo);";

        var numeroOrganograma = dados.numeroOrganograma.Trim();

        if (numeroOrganograma.Length != 10)
        {
            Console.WriteLine($"⚠️ Formato inesperado de numeroOrganograma: {numeroOrganograma}. Ignorando...");
            return;
        }

        var parameters = new
        {
            id_cloud = dados.id.ToString(),
            descricao = dados.descricao.Trim(),
            numero = numeroOrganograma,
            nivel = dados.nivel.ToString(),
            config_id = dados.configuracaoOrganograma.id,
            orgao = numeroOrganograma.Substring(0, 2),       // 2 primeiros dígitos → Órgão
            unidade = numeroOrganograma.Substring(2, 3),     // 3 próximos dígitos → Unidade
            centro_custo = numeroOrganograma.Substring(5, 5) // 5 últimos dígitos → Centro de Custo
        };

        Console.WriteLine($"📡 Processando organograma: {dados.descricao} (ID: {dados.id}, Numero: {dados.numeroOrganograma})");
        try
        {
            Console.WriteLine($"🔍 Verificando se o organograma {dados.id} já existe...");
            using var connection = _pgConnect.GetConnection();
            int count = await _pgConnect.ExecuteScalarAsync<int>(checkExistsQuery, parameters);

            if (count == 0)
            {
                await _pgConnect.ExecuteInsertAsync(insertQuery, parameters);
                Console.WriteLine($"✅ Organograma {dados.descricao} inserido com sucesso!");
            }
            else
            {
                Console.WriteLine($"🔄 Organograma {dados.descricao} já existe!");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao inserir organograma {dados.descricao}: {e.Message}");
            return;
        }
    }
}