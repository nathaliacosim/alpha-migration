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

public class BensController
{
    private readonly PgConnect _pgConnect;
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private const string _rota = "api/bens";

    public BensController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper)
    {
        _pgConnect = pgConnect;
        _sqlHelper = sqlHelper;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public async Task EnviarBensParaCloud()
    {
        var bens = await SelecionarBensSemIdCloud();
        if (!bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem sem ID Cloud encontrado!");
            return;
        }

        foreach (var item in bens)
        {
            try
            {
                var payload = await MontarPayloadBem(item);
                await EnviarBem(payload, item.codigo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar o bem {item.codigo}: {ex.Message}");
            }
        }
    }

    private async Task<List<Bens>> SelecionarBensSemIdCloud()
    {
        const string query = "SELECT * FROM pat_bens WHERE id_cloud IS NULL;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var bens = (await connection.QueryAsync<Bens>(query)).ToList();
            Console.WriteLine($"✅ {bens.Count} bens sem ID Cloud foram encontrados!");
            return bens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os bens: {ex.Message}");
            return new List<Bens>();
        }
    }

    #region Métodos de Seleção Auxiliares

    public Task<string> SelecionarTipoBemByTipo(string tipo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM pat_tp_bens WHERE classificacao = @classificacao;", new { classificacao = tipo });

    public Task<string> SelecionarTipoUtilizacaoByCodigo(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM tp_classificacao WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarGrupoBensBySub(int? codigoSubgrupo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>(@"
            SELECT g.id_cloud
            FROM pat_subgrupo_bens s
            JOIN pat_grupo_bens g ON g.codigo = s.grupo_bens_cod
            WHERE s.codigo = @codigoSubgrupo;", new { codigoSubgrupo });

    public Task<string> SelecionarTipoAquisicaoByCodigo(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM pat_tp_origem WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarEstadoConservacaoByStatus(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM pat_tp_status WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarFornecedorByCodigo(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM fornecedor WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarLocalizacaoByCodigo(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM local WHERE codigo = @codigo;", new { codigo });

    public Task<GrupoBens> SelecionarDadosGrupoBensBySub(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<GrupoBens>(@"
            SELECT g.*
            FROM pat_subgrupo_bens s
            JOIN pat_grupo_bens g ON g.codigo = s.grupo_bens_cod
            WHERE s.codigo = @codigo;", new { codigo });

    public Task<SubgrupoBens> SelecionarDadosEspecieBensBySub(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<SubgrupoBens>("SELECT * FROM pat_subgrupo_bens WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarMetodoDepreciacao(string tipo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM metodo_depreciacao_cloud WHERE tipo = @tipo;", new { tipo });

    #endregion Métodos de Seleção Auxiliares

    private async Task<BensPOST> MontarPayloadBem(Bens item)
    {
        var dadosEspecie = await SelecionarDadosEspecieBensBySub(item.subgrupo_bens_cod);
        var dadosGrupo = await SelecionarDadosGrupoBensBySub(item.subgrupo_bens_cod);

        var idCloudEspecieBem = dadosEspecie?.id_cloud;
        var idCloudGrupoBem = dadosGrupo.id_cloud;
        var depreciacaoAnual = dadosGrupo?.deprecia;
        var vidaUtilAnos = dadosEspecie?.vida_util_meses / 12;

        var idCloudTipoUtilizacao = item.tp_classificacao_cod != null ? await SelecionarTipoUtilizacaoByCodigo(item.tp_classificacao_cod) : null;
        var idCloudTipoBem = await SelecionarTipoBemByTipo(item.tipo);
        var idCloudTipoAquisicao = await SelecionarTipoAquisicaoByCodigo(item.tp_origem_cod);
        var idCloudEstadoConservacao = item.aquisicao_status_cod != null
            ? await SelecionarEstadoConservacaoByStatus(item.aquisicao_status_cod)
            : await SelecionarEstadoConservacaoByStatus(item.atual_status_cod);

        var idCloudFornecedor = item.fornecedor_cod != null ? await SelecionarFornecedorByCodigo(item.fornecedor_cod) : null;
        var idCloudLocalizacaoFisica = item.local_cod != null ? await SelecionarLocalizacaoByCodigo(item.local_cod) : null;
        var idCloudMetodoDepreciacao = await SelecionarMetodoDepreciacao("DEPRECIACAO");

        var nroPlaca = item.plaqueta == null || item.plaqueta == 0 ? $"BTH{item.codigo}" : item.plaqueta.ToString();

        Console.WriteLine($"📦 Montando payload do bem {item.codigo}...");

        return new BensPOST
        {
            numeroRegistro = item.codigo.ToString(),
            numeroPlaca = nroPlaca,
            descricao = item.descricao?.Trim().ToUpper(),
            dataInclusao = "2025-01-01",
            dataAquisicao = item.nfdata?.ToString("yyyy-MM-dd"),
            consomeCombustivel = false,

            tipoUtilizacaoBem = item.tipo == "I" && idCloudTipoUtilizacao != null
                ? new TipoUtilizacaoBemBensPOST { id = int.Parse(idCloudTipoUtilizacao) }
                : null,

            numeroAnoProcesso = (!string.IsNullOrWhiteSpace(item.processo_num) && item.processo_num != "0" && item.processo_ano > 0)
                ? new NumeroAnoProcessoBensPOST { descricao = $"{item.processo_num}/{item.processo_ano}" }
                : null,

            numeroAnoEmpenho = (item.empenho_num > 0 && item.empenho_ano > 0)
                ? new List<NumeroAnoEmpenhoBensPOST> {
                    new NumeroAnoEmpenhoBensPOST { descricao = $"{item.empenho_num}/{item.empenho_ano}" }
                } : null,

            tipoBem = new TipoBemBensPOST { id = int.Parse(idCloudTipoBem) },
            grupoBem = new GrupoBemBensPOST { id = int.Parse(idCloudGrupoBem) },
            especieBem = new EspecieBemBensPOST { id = int.Parse(idCloudEspecieBem) },
            tipoAquisicao = new TipoAquisicaoBensPOST { id = int.Parse(idCloudTipoAquisicao) },
            situacaoBem = new SituacaoBemBensPOST { descricao = "Em Edição", valor = "EM_EDICAO" },
            responsavel = new ResponsavelBensPOST { id = 42085454 },
            organograma = new OrganogramaBensPOST { id = 2394672 },

            estadoConservacao = new EstadoConservacaoBensPOST { id = int.Parse(idCloudEstadoConservacao) },

            tipoComprovante = item.notafiscal != null ? new TipoComprovanteBensPOST { id = 3755 } : null,

            fornecedor = item.fornecedor_cod != null
                ? new FornecedorBensPOST { id = int.Parse(idCloudFornecedor) }
                : null,

            numeroComprovante = item.notafiscal != null
                ? item.nfiscalserie != null
                    ? $"{item.notafiscal}/{item.nfiscalserie}"
                    : $"{item.notafiscal}"
                : null,

            localizacaoFisica = item.local_cod != null
                ? new LocalizacaoFisicaBensPOST { id = int.Parse(idCloudLocalizacaoFisica) }
                : null,

            bemValor = new BemValorBensPOST
            {
                moeda = new MoedaBensPOST
                {
                    id = 8,
                    nome = "R$ - Real (1994)",
                    sigla = "R$",
                    dtCotacao = "1994-07-01",
                    fatorConversao = 2750,
                    formaCalculo = "DIVIDIR"
                },
                metodoDepreciacao = depreciacaoAnual > 0 && depreciacaoAnual != null
                    ? new MetodoDepreciacaoBensPOST { id = int.Parse(idCloudMetodoDepreciacao) }
                    : null,

                vlAquisicao = item.nfvalor,
                vlAquisicaoConvertido = item.nfvalor,
                vlResidual = 0,
                saldoDepreciar = item.nfvalor,
                vlDepreciado = 0,
                vlDepreciavel = item.nfvalor,
                vlLiquidoContabil = item.nfvalor,
                taxaDepreciacaoAnual = depreciacaoAnual > 0 ? depreciacaoAnual : null,
                dtInicioDepreciacao = depreciacaoAnual > 0 ? item.nfdata?.ToString("yyyy-MM-dd") : null,
                anosVidaUtil = depreciacaoAnual > 0 ? vidaUtilAnos : null
            }
        };
    }

    private async Task EnviarBem(BensPOST bensPost, int codigoBem)
    {
        var json = JsonConvert.SerializeObject(bensPost);
        Console.WriteLine($"📤 Enviando bem {codigoBem} para a nuvem...");

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_urlBase, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"📄 Resposta da API: {responseBody}");

        if (responseBody.Contains("message"))
        {
            Console.WriteLine($"❌ Erro ao enviar o bem {codigoBem}: {responseBody}");
        }

        var query = $"UPDATE pat_bens SET id_cloud = '{responseBody}' WHERE codigo = {codigoBem};";
        await _sqlHelper.ExecuteScalarAsync<int>(query);

        Console.WriteLine($"✅ Bem {codigoBem} enviado com sucesso!");
    }

    public async Task<List<Bens>> SelecionarBensParaExclusao()
    {
        const string query = "SELECT * FROM pat_bens WHERE id_cloud IS NOT NULL AND id_cloud != '' AND id_cloud != '0';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var bens = (await connection.QueryAsync<Bens>(query)).ToList();
            Console.WriteLine($"✅ {bens.Count} bens encontrados para exclusão!");
            return bens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os bens: {ex.Message}");
            return null;
        }
    }

    public async Task ExcluirBensCloud()
    {
        var bens = await SelecionarBensParaExclusao();
        Console.WriteLine("🔧 Excluindo bens da nuvem...");
        if (bens == null)
        {
            Console.WriteLine("❌ Nenhum bem encontrado para exclusão!");
            return;
        }

        foreach (var item in bens)
        {
            var url_base = $"{_urlBase}/{item.id_cloud}";
            Console.WriteLine($" 🔹  Excluindo bem com ID {item.id_cloud}...");

            var response = await _httpClient.DeleteAsync(url_base);
            Console.WriteLine($" 🗑️ Requisição DELETE enviada para: {url_base}");
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📄 Resposta da API: {responseContent}");
        }
    }

    public async Task<List<Bens>> SelecionarBensComDataTombamento()
    {
        const string query = "SELECT * FROM pat_bens WHERE tombamento_data is not null ORDER BY tombamento_data, codigo;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var bens = (await connection.QueryAsync<Bens>(query)).ToList();
            Console.WriteLine($"✅ {bens.Count} bens com data de tombamento encontrados!");
            return bens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os bens: {ex.Message}");
            return null;
        }
    }

    public async Task AguardarTombamento()
    {
        var bens = await SelecionarBensComDataTombamento();
        if (bens == null || !bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem encontrado com data de tombamento!");
            return;
        }

        foreach (var item in bens)
        {
            var url = $"{_urlBase}/{item.id_cloud}/aguardarTombamento";
            Console.WriteLine($"🔄 Iniciando solicitação para aguardar tombamento do bem 🏷️ {item.codigo}...");

            try
            {
                var response = await _httpClient.PostAsync(url, null);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"⚠️ Falha na requisição (HTTP {response.StatusCode}) para o bem {item.codigo}.");
                    Console.WriteLine($"📄 Detalhes: {responseContent}");
                    continue;
                }

                Console.WriteLine($"✅ Tombamento aguardado com sucesso para o bem {item.codigo}.");
                Console.WriteLine($"📄 Resposta da API: {responseContent}");

                if (responseContent.Contains("message"))
                {
                    Console.WriteLine($"❌ A API retornou uma mensagem de erro para o bem {item.codigo}: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro inesperado ao processar o bem {item.codigo}: {ex.Message}");
            }

            Console.WriteLine(new string('-', 60));
        }
    }

    public async Task DesfazerTombamento()
    {
        var bens = await SelecionarBensComDataTombamento();
        if (bens == null || !bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem encontrado com data de tombamento!");
            return;
        }
        foreach (var item in bens)
        {
            var url = $"{_urlBase}/{item.id_cloud}/desfazerTombamento";
            Console.WriteLine($"🔄 Iniciando solicitação para desfazer tombamento do bem 🏷️ {item.codigo}...");
            try
            {
                var response = await _httpClient.PostAsync(url, null);
                var responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"⚠️ Falha na requisição (HTTP {response.StatusCode}) para o bem {item.codigo}.");
                    Console.WriteLine($"📄 Detalhes: {responseContent}");
                    continue;
                }

                Console.WriteLine($"✅ Tombamento desfeito com sucesso para o bem {item.codigo}.");
                Console.WriteLine($"📄 Resposta da API: {responseContent}");
                if (responseContent.Contains("message"))
                {
                    Console.WriteLine($"❌ A API retornou uma mensagem de erro para o bem {item.codigo}: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro inesperado ao processar o bem {item.codigo}: {ex.Message}");
            }
            Console.WriteLine(new string('-', 60));
        }
    }

    public async Task TombarBens()
    {
        var bens = await SelecionarBensComDataTombamento();
        if (bens == null || !bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem encontrado com data de tombamento!");
            return;
        }

        foreach (var item in bens)
        {
            Console.WriteLine($"🔄 Iniciando tombamento do bem 🏷️ {item.codigo}...");

            var url = $"{_urlBase}/{item.id_cloud}/tombar";
            var numeroPlaca = (item.plaqueta == null || item.plaqueta == 0)
                ? $"BTH{item.codigo}"
                : item.plaqueta.ToString();

            var dadosTombamento = new TombarBemPOST
            {
                nroPlaca = numeroPlaca,
                organograma = new OrganogramaTombarBemPOST { id = 2394672 },
                responsavel = new ResponsavelTombarBemPOST { id = 42085454 },
                dhTombamento = item.nfdata >= item.tombamento_data ? item.tombamento_data?.ToString("yyyy-MM-dd") + " 00:00:00" : item.nfdata?.ToString("yyyy-MM-dd") + " 00:00:00",
            };

            var jsonPayload = JsonConvert.SerializeObject(dadosTombamento, Formatting.Indented);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            Console.WriteLine("📦 Dados enviados:");
            Console.WriteLine(jsonPayload);

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"⚠️ Erro ao tombar o bem {item.codigo} (HTTP {response.StatusCode})");
                    Console.WriteLine($"📄 Resposta da API: {responseContent}");
                    continue;
                }

                Console.WriteLine($"✅ Tombamento realizado com sucesso para o bem {item.codigo}!");
                Console.WriteLine($"📄 Resposta da API: {responseContent}");

                if (responseContent.Contains("message"))
                {
                    Console.WriteLine($"⚠️ Aviso: A API retornou uma mensagem para o bem {item.codigo}: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro inesperado ao tombar o bem {item.codigo}: {ex.Message}");
            }

            Console.WriteLine(new string('-', 70));
        }
    }
}