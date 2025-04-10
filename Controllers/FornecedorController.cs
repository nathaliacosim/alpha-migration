using Alpha.Data;
using Alpha.Models.Alpha;
using Alpha.Models.BethaCloud;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class FornecedorController
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private readonly string _rota = "api/fornecedores";

    public FornecedorController(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "Authorization", $"Bearer {_token}" } }
        };
    }

    public async Task<List<Fornecedor>> SelecionarTodosFornecedoresSemIdCloud()
    {
        try
        {
            var query = "SELECT * FROM fornecedor WHERE id_cloud is null;";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<Fornecedor>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} fornecedores sem ID Cloud foram encontrados!");
            return dados;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao buscar os fornecedores: {e.Message}");
            return new List<Fornecedor>();
        }
    }

    public async Task EnviarFornecedoresParaCloud()
    {
        var fornecedores = await SelecionarTodosFornecedoresSemIdCloud();
        if (!fornecedores.Any())
        {
            Console.WriteLine("❌ Nenhum fornecedor sem ID Cloud encontrado!");
            return;
        }

        foreach (var fornecedor in fornecedores)
        {
            Console.WriteLine($"📡 Enviando fornecedor {fornecedor.codigo} para o Cloud Patrimônio...");
            var tipoFornecedor = fornecedor.tipo == "J" ? "JURIDICA" : "FISICA";
            var fornecedorPost = new FornecedorPOST
            {
                nome = fornecedor.nome,
                cpfCnpj = Regex.Replace(fornecedor.cpf ?? "", @"[^\d]", ""),
                tipo = new TipoFornecedorPOST
                {
                    valor = tipoFornecedor,
                    descricao = tipoFornecedor
                },
                dataInclusao = "2025-01-01",
                situacao = new SituacaoFornecedorPOST
                {
                    valor = "ATIVO",
                    descricao = "ATIVO"
                }
            };

            var json = JsonConvert.SerializeObject(fornecedorPost);
            Console.WriteLine($"📤 JSON: {json}");
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            try
            {
                var enviaFornecedor = await _httpClient.PostAsync(_urlBase, content);
                var response = await enviaFornecedor.Content.ReadAsStringAsync();

                var id_cloud = response.ToString();
                Console.WriteLine($"📄 Resposta da API: {response}");
                if (response.Contains("message"))
                {
                    Console.WriteLine($"❌ Erro ao enviar o fornecedor {fornecedor.codigo}: {response}");
                    continue;
                }
                var query = $"UPDATE fornecedor SET id_cloud = '{id_cloud}' WHERE codigo = {fornecedor.codigo};";
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Fornecedor {fornecedor.codigo} enviado com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao enviar o fornecedor {fornecedor.codigo}: {e.Message}");
            }
        }
    }

    public async Task<List<Fornecedor>> SelecionarFornecedoresSemCnpjCpf()
    {
        try
        {
            var query = "SELECT * FROM fornecedor WHERE cpf = '' OR cpf IS NULL";
            using var connection = _pgConnect.GetConnection();
            var dados = (await connection.QueryAsync<Fornecedor>(query)).AsList();
            Console.WriteLine($"✅ {dados.Count} fornecedores sem CPF/CNPJ foram encontrados!");
            return dados;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao buscar os fornecedores: {e.Message}");
            return new List<Fornecedor>();
        }
    }

    public async Task AtualizarFornecedoresSemCnpjCpf()
    {
        var fornecedores = await SelecionarFornecedoresSemCnpjCpf();
        if (fornecedores.Count() == 0)
        {
            Console.WriteLine("❌ Nenhum fornecedor sem CPF/CNPJ encontrado!");
            return;
        }

        foreach (var fornecedor in fornecedores)
        {
            var tipo = fornecedor.tipo;
            var novoDocumento = string.Empty;
            if (tipo == "F")
            {
                novoDocumento = Utils.GeradorDocumentos.GerarCPF();
            }
            else
            {
                novoDocumento = Utils.GeradorDocumentos.GerarCNPJ();
            }

            var query = $"UPDATE fornecedor SET cpf = '{novoDocumento}', nome = UPPER(TRIM(nome)) WHERE codigo = {fornecedor.codigo};";
            try
            {
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query);
                Console.WriteLine($"✅ Fornecedor {fornecedor.codigo} atualizado com sucesso!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao atualizar o fornecedor {fornecedor.codigo}: {e.Message}");
            }
        }
    }

    public async Task BuscarFornecedoresCloud()
    {
        int offset = 0;
        int limit = 500;
        Console.WriteLine("🔎 Iniciando busca de fornecedores...");
        var controle = true;

        while (controle)
        {
            string urlBusca = $"{_urlBase}?limit={limit}&offset={offset}";
            Console.WriteLine($"📡 Buscando fornecedores... Offset: {offset}, Limite: {limit}");

            try
            {
                Console.WriteLine($"🔍 Fazendo requisição para: {urlBusca}");
                var response = await _httpClient.GetStringAsync(urlBusca);
                Console.WriteLine($"📜 Resposta recebida: {response.Substring(0, Math.Min(response.Length, 100))}...");
                var retorno = JsonConvert.DeserializeObject<FornecedorGET>(response);

                if (retorno?.content == null || retorno?.content?.Count == 0)
                {
                    Console.WriteLine("✅ Busca concluída! Nenhum fornecedor encontrado na última requisição.");
                    controle = false;
                    break;
                }

                Console.WriteLine($"📥 {retorno.content.Count} fornecedores recebidos. Inserindo no banco...");
                await Task.WhenAll(retorno.content.Select(InserirFornecedorCloud));

                if (!retorno.hasNext)
                {
                    Console.WriteLine("🚀 Todos os fornecedores foram processados!");
                    break;
                }
                offset += limit;
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Erro ao buscar fornecedores: {e.Message}");
                Console.WriteLine($"🛠 StackTrace: {e.StackTrace}");
                break;
            }
        }
    }

    public async Task InserirFornecedorCloud(ContentFornecedorGET dados)
    {
        if (dados == null)
        {
            Console.WriteLine("❌ Os dados do fornecedor estão nulos! Pulando...");
            return;
        }

        var verificaSeFornecedorExiste = $"SELECT id_cloud FROM fornecedores_cloud WHERE id_cloud = '{dados.id}'";
        var inserirFornecedor = $"INSERT INTO fornecedores_cloud (id_cloud, nome, cpf_cnpj, situacao) VALUES ('{dados.id.ToString()}', '{dados.nome.Trim()}', '{dados.cpfCnpj}', '{dados.situacao.valor}')";

        try
        {
            Console.WriteLine($"🔍 Verificando se o fornecedor {dados.id} já existe...");
            using var connection = _pgConnect.GetConnection();
            var id = await connection.ExecuteScalarAsync<string>(verificaSeFornecedorExiste);

            if (id == null)
            {
                await connection.ExecuteAsync(inserirFornecedor);
                Console.WriteLine($"✅ Fornecedor {dados.nome} inserido com sucesso!");
            }
            else
            {
                Console.WriteLine($"🔄 Fornecedor {dados.nome} já existe!");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Erro ao inserir fornecedor {dados.nome}: {e.Message}");
            return;
        }
    }
}