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

public class LocalizacaoFisicaController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase = "https://patrimonio.betha.cloud/patrimonio-services/api/localizacoes-fisicas";

    public LocalizacaoFisicaController(PgConnect pgConnect, string token)
    {
        _pgConnect = pgConnect;
        _token = token;
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task<List<Local>> SelecionarLocalizacoesSemIdCloud()
    {
        try
        {
            var query = "SELECT * FROM local WHERE id_cloud is null;";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<Local>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} localizações físicas sem ID Cloud foram encontradas!");
            return dados;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar a localização física {ex.Message}");
            return new List<Local>();
        }
    }

    public async Task EnviarLocalizacoesParaCloud()
    {
        var localizacoes = await SelecionarLocalizacoesSemIdCloud();
        if (!localizacoes.Any())
        {
            Console.WriteLine("❌ Nenhuma localização física sem ID Cloud encontrada!");
            return;
        }

        foreach (var localizacao in localizacoes)
        {
            Console.WriteLine($"📡 Enviando a localização física {localizacao.codigo} para o Cloud Patrimônio...");
            var localizacaoPost = new LocalizacaoFisicaPOST
            {
                descricao = localizacao.descricao.Trim().ToUpper(),
                nivel = 1,
                classificacao = new ClassificacaoLocalizacaoFisicaPOST
                {
                    valor = "ANALITICO"
                },
                endereco = null,
                observacao = null
            };

            var json = JsonConvert.SerializeObject(localizacaoPost);
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
                    Console.WriteLine($"❌ Erro ao enviar a localização física {localizacao.codigo}: {response}");
                    continue;
                }
                var query = $"UPDATE local SET id_cloud = '{id_cloud}' WHERE codigo = {localizacao.codigo};";
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Localização física {localizacao.codigo} enviada com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao enviar a localização física {localizacao.codigo}: {e.Message}");
            }
        }
    }

    public async Task BuscarLocalizacaoFisicaCloud()
    {
        int offset = 0;
        int limit = 500;
        Console.WriteLine("🔎 Iniciando busca de localizações físicas...");
        var controle = true;

        while (controle)
        {
            string urlBusca = $"{_urlBase}?limit={limit}&offset={offset}";
            Console.WriteLine($"📡 Buscando localizações físicas... Offset: {offset}, Limite: {limit}");

            try
            {
                Console.WriteLine($"🔍 Fazendo requisição para: {urlBusca}");
                var response = await _httpClient.GetStringAsync(urlBusca);
                Console.WriteLine($"📜 Resposta recebida: {response.Substring(0, Math.Min(response.Length, 100))}...");
                var retorno = JsonConvert.DeserializeObject<LocalizacaoFisicaGET>(response);

                if (retorno?.content == null || retorno?.content?.Count == 0)
                {
                    Console.WriteLine("✅ Busca concluída! Nenhuma localização física encontrada na última requisição.");
                    controle = false;
                    break;
                }

                Console.WriteLine($"📥 {retorno.content.Count} localizações físicas recebidas. Inserindo no banco...");
                await Task.WhenAll(retorno.content.Select(InserirLocalizacaoFisicaCloud));

                if (!retorno.hasNext)
                {
                    Console.WriteLine("🚀 Todas as localizações físicas foram processadas!");
                    break;
                }
                offset += limit;
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao buscar localizações físicas: {e.Message}");
                Console.WriteLine($"🛠 StackTrace: {e.StackTrace}");
                break;
            }
        }
    }

    public async Task InserirLocalizacaoFisicaCloud(ContentLocalizacaoFisicaGET dados)
    {
        if (dados == null)
        {
            Console.WriteLine("❌ Os dados da localização física estão nulos! Pulando...");
            return;
        }

        var verificaSeExiste = $"SELECT id_cloud FROM local_cloud WHERE id_cloud = '{dados.id}'";
        var inserirRegistro = $"INSERT INTO local_cloud (id_cloud, descricao, classificacao, nivel) VALUES ('{dados.id.ToString()}', '{dados.descricao.Trim()}', '{dados.classificacao.valor}', {dados.nivel})";

        try
        {
            Console.WriteLine($"🔍 Verificando se a localização física {dados.id} já existe...");
            using var connection = _pgConnect.GetConnection();
            var id = await connection.ExecuteScalarAsync<string>(verificaSeExiste);

            if (id == null)
            {
                await connection.ExecuteAsync(inserirRegistro);
                Console.WriteLine($"✅ Localização física {dados.descricao} inserida com sucesso!");
            }
            else
            {
                Console.WriteLine($"🔄 Localização física {dados.descricao} já existe!");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao inserir localização física {dados.descricao}: {e.Message}");
            return;
        }
    }
}