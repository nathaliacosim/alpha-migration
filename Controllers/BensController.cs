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

    #region Conversão e Migração Sistema Betha

    public async Task EnviarBensBethaParaCloud()
    {
        var bens = await SelecionarBensBethaSemIdCloud();
        if (!bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem sem ID Cloud encontrado!");
            return;
        }

        foreach (var item in bens)
        {
            try
            {
                var payload = MontarPayloadBemBetha(item);
                await EnviarBemBetha(payload, item.i_bem);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar o bem {item.i_bem}: {ex.Message}");
            }
        }
    }

    private async Task<List<BensBetha>> SelecionarBensBethaSemIdCloud()
    {
        const string query = "SELECT * FROM bens_cloud WHERE id_cloud IS NULL;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var bens = (await connection.QueryAsync<BensBetha>(query)).ToList();
            Console.WriteLine($"✅ {bens.Count} bens sem ID Cloud foram encontrados!");
            return bens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os bens: {ex.Message}");
            return new List<BensBetha>();
        }
    }

    public BensPOST MontarPayloadBemBetha(BensBetha item)
    {
        Console.WriteLine($"📦 Montando payload do bem {item.i_bem}...");

        return new BensPOST
        {
            numeroRegistro = item.i_bem.ToString(),
            numeroPlaca = item.numero_placa,
            numeroComprovante = item.documento,
            descricao = item.descricao.Trim().ToUpper(),
            dataInclusao = "2025-01-01",
            dataAquisicao = DateTime.ParseExact(item.data_aquis, "MM/dd/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"),
            consomeCombustivel = false,
            numeroAnoEmpenho = new[] { "M", "N", "C" }.Contains(item.tipo_aquis) && item.nr_empenho != null && item.ano_empenho != null ? new List<NumeroAnoEmpenhoBensPOST>
                {
                    new NumeroAnoEmpenhoBensPOST
                    {
                        descricao = (item.nr_empenho+"/"+item.ano_empenho),
                    }
                } : null,
            numeroAnoProcesso = new[] { "M", "N", "C" }.Contains(item.tipo_aquis) && item.i_processo != null && item.i_ano_proc != null
            ? new NumeroAnoProcessoBensPOST
            {
                descricao = item.i_processo + "/" + item.i_ano_proc
            }
            : null,
            numeroAnoSolicitacao = null,
            tipoBem = new TipoBemBensPOST
            {
                id = item.id_tipo_bem,
            },
            grupoBem = new GrupoBemBensPOST
            {
                id = item.id_grupo_bem,
            },
            especieBem = new EspecieBemBensPOST
            {
                id = item.id_especie_bem,
            },
            tipoUtilizacaoBem = item.id_tipo_bem == 2929 ? new TipoUtilizacaoBemBensPOST
            {
                id = item.id_tipo_utilizacao,
            } : null,
            tipoAquisicao = new TipoAquisicaoBensPOST
            {
                id = item.id_tipo_aquisicao,
            },
            fornecedor = item.i_fornec != null ? new FornecedorBensPOST
            {
                id = item.id_fornecedor,
            } : null,
            responsavel = item.i_respons != null ? new ResponsavelBensPOST
            {
                id = item.id_responsavel,
            } : null,
            estadoConservacao = new EstadoConservacaoBensPOST
            {
                id = item.id_estado_conservacao,
            },
            tipoComprovante = item.id_tipo_comprovante != null ? new TipoComprovanteBensPOST
            {
                id = item.id_tipo_comprovante,
            } : null,
            organograma = new OrganogramaBensPOST
            {
                id = item.id_organograma,
            },
            situacaoBem = new SituacaoBemBensPOST
            {
                descricao = "Em Edição",
                valor = "EM_EDICAO"
            },
            localizacaoFisica = new LocalizacaoFisicaBensPOST
            {
                id = item.id_localizacao_fisica
            },
            bemValor = new BemValorBensPOST
            {
                metodoDepreciacao = item.tipo_aquis != "D" && item.perc_deprec_anual > 0 ? new MetodoDepreciacaoBensPOST
                {
                    id = item.id_metodo_depreciacao
                } : null,
                moeda = new MoedaBensPOST
                {
                    id = 8,
                    nome = "R$ - Real (1994)",
                    sigla = "R$",
                    dtCotacao = "1994-07-01",
                    fatorConversao = 2750,
                    formaCalculo = "DIVIDIR"
                },
                vlAquisicao = item.valor_aquis ?? 0.01m,
                vlAquisicaoConvertido = item.valor_aquis ?? 0.01m,
                vlResidual = 0,
                saldoDepreciar = item.valor_aquis ?? 0.01m,
                vlDepreciado = 0,
                vlDepreciavel = item.valor_aquis ?? 0.01m,
                vlLiquidoContabil = item.valor_aquis ?? 0.01m,
                taxaDepreciacaoAnual = item.tipo_aquis != "D" && item.perc_deprec_anual > 0 ? item.perc_deprec_anual : null,
                dtInicioDepreciacao = item.tipo_aquis != "D" && item.perc_deprec_anual > 0 ? DateTime.ParseExact(item.dt_inicio_deprec, "MM/dd/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd") : null,
                anosVidaUtil = item.tipo_aquis != "D" && item.perc_deprec_anual > 0 ? item.vida_util : null
            }
        };
    }

    private async Task<bool> EnviarBemBetha(BensPOST bensPost, int codigoBem, int tentativas = 0, int maxTentativas = 1)
    {
        try
        {
            var json = JsonConvert.SerializeObject(bensPost);
            Console.WriteLine($"📤 Enviando bem {codigoBem} para a nuvem...");

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_urlBase, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"📄 Resposta da API para o bem {codigoBem}: {responseBody}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Bem {codigoBem} enviado com sucesso.");

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine("⚠️ Resposta da API está vazia. Não é possível atualizar o banco.");
                    return false;
                }

                var queryUp = @"UPDATE bens_cloud SET id_cloud = @IdCloud WHERE i_bem = @Codigo;";
                var parameters = new
                {
                    IdCloud = responseBody,
                    Codigo = codigoBem
                };

                using var connection = _pgConnect.GetConnection();
                var rowsAffected = await connection.ExecuteAsync(queryUp, parameters);

                if (rowsAffected > 0)
                {
                    Console.WriteLine($"💾 Registro do bem {codigoBem} atualizado com id_cloud = '{responseBody}'.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"⚠️ Nenhum registro foi atualizado para o bem {codigoBem}.");
                    return false;
                }
            }
            else
            {
                if (responseBody.Contains("alterar o valor depreciado, o valor atualizado ficará abaixo do valor residual"))
                {
                    Console.WriteLine($"⚠️ Bem {codigoBem} não pode ser enviado (valor depreciado menor que o residual).");

                    var query = @"UPDATE bens_cloud SET id_cloud = @IdCloud WHERE i_bem = @Codigo;";
                    var parameters = new { IdCloud = "VALOR-NEGATIVO", Codigo = codigoBem };

                    using var connection = _pgConnect.GetConnection();
                    await connection.ExecuteAsync(query, parameters);

                    return true;
                }

                if (responseBody.Contains("o tipo de aquisição deve ser diferente de locação ou comodato"))
                {
                    Console.WriteLine($"⚠️ Bem {codigoBem} não pode ser enviado (locação/comodato), será considerado como enviado.");

                    var query = @"UPDATE bens_cloud SET id_cloud = @IdCloud WHERE i_bem = @Codigo;";
                    var parameters = new { IdCloud = "BEM-IGNORADO", Codigo = codigoBem };

                    using var connection = _pgConnect.GetConnection();
                    await connection.ExecuteAsync(query, parameters);

                    return true;
                }

                Console.WriteLine($"❌ Falha ao enviar bem {codigoBem}: {response.StatusCode}");

                if (tentativas < maxTentativas)
                {
                    Console.WriteLine($"🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                    await Task.Delay(3000);
                    return await EnviarBemBetha(bensPost, codigoBem, tentativas + 1, maxTentativas);
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao enviar bem {codigoBem}: {ex.Message}");
            return false;
        }
    }

    public async Task AguardarTombamentoBetha()
    {
        var bens = await SelecionarBensEnviadosBetha();
        if (bens == null || !bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem encontrado!");
            return;
        }

        foreach (var item in bens)
        {
            var url = $"{_urlBase}/{item.id_cloud}/aguardarTombamento";
            Console.WriteLine($"🔄 Iniciando solicitação para aguardar tombamento do bem 🏷️ {item.i_bem}...");

            try
            {
                var response = await _httpClient.PostAsync(url, null);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"⚠️ Falha na requisição (HTTP {response.StatusCode}) para o bem {item.i_bem}.");
                    Console.WriteLine($"📄 Detalhes: {responseContent}");
                    continue;
                }

                Console.WriteLine($"✅ Tombamento aguardado com sucesso para o bem {item.i_bem}.");
                Console.WriteLine($"📄 Resposta da API: {responseContent}");

                if (responseContent.Contains("message"))
                {
                    Console.WriteLine($"❌ A API retornou uma mensagem de erro para o bem {item.i_bem}: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro inesperado ao processar o bem {item.i_bem}: {ex.Message}");
            }

            Console.WriteLine(new string('-', 60));
        }
    }

    public async Task TombarBensBetha()
    {
        var bens = await SelecionarBensEnviadosBetha();
        if (bens == null || !bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem encontrado com data de tombamento!");
            return;
        }

        foreach (var item in bens)
        {
            Console.WriteLine($"🔄 Iniciando tombamento do bem 🏷️ {item.i_bem}...");

            var url = $"{_urlBase}/{item.id_cloud}/tombar";
            var dadosTombamento = new TombarBemPOST
            {
                nroPlaca = item.numero_placa,
                organograma = new OrganogramaTombarBemPOST { id = item.id_organograma ?? 1208919 },
                responsavel = new ResponsavelTombarBemPOST { id = item.id_responsavel ?? 43537257 },
                dhTombamento = DateTime.ParseExact((item?.dt_inicio_deprec ?? item.data_aquis), "MM/dd/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd") + " 00:00:00",
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
                    Console.WriteLine($"⚠️ Erro ao tombar o bem {item.i_bem} (HTTP {response.StatusCode})");
                    Console.WriteLine($"📄 Resposta da API: {responseContent}");
                    continue;
                }

                Console.WriteLine($"✅ Tombamento realizado com sucesso para o bem {item.i_bem}!");
                Console.WriteLine($"📄 Resposta da API: {responseContent}");

                if (responseContent.Contains("message"))
                {
                    Console.WriteLine($"⚠️ Aviso: A API retornou uma mensagem para o bem {item.i_bem}: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro inesperado ao tombar o bem {item.i_bem}: {ex.Message}");
            }

            Console.WriteLine(new string('-', 70));
        }
    }

    public async Task<List<BensBetha>> SelecionarBensEnviadosBetha()
    {
        const string query = "SELECT * FROM bens_cloud WHERE id_cloud IS NOT NULL;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var bens = (await connection.QueryAsync<BensBetha>(query)).ToList();
            Console.WriteLine($"✅ {bens.Count} bens encontrados para exclusão!");
            return bens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os bens: {ex.Message}");
            return null;
        }
    }

    public async Task ExcluirBensBethaCloud()
    {
        var bens = await SelecionarBensEnviadosBetha();
        Console.WriteLine("🔧 Excluindo bens da nuvem...");

        if (bens == null || !bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem encontrado para exclusão!");
            return;
        }

        foreach (var item in bens)
        {
            var url_base = $"{_urlBase}/{item.id_cloud}";
            Console.WriteLine($"🔹 Excluindo bem com ID {item.id_cloud}...");

            var response = await _httpClient.DeleteAsync(url_base);
            Console.WriteLine($"🗑️ Requisição DELETE enviada para: {url_base}");

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📄 Resposta da API: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Bem {item.i_bem} excluído com sucesso da nuvem. Limpando id_cloud...");

                var query = @"UPDATE bens_cloud SET id_cloud = NULL WHERE i_bem = @Codigo;";
                var parameters = new { Codigo = item.i_bem };

                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query, parameters);

                Console.WriteLine($"💾 id_cloud do bem {item.i_bem} removido do banco.");
            }
            else
            {
                Console.WriteLine($"❌ Falha ao excluir bem {item.i_bem} da nuvem. Status: {response.StatusCode}");
            }
        }
    }

    #endregion Conversão e Migração Sistema Betha

    #region Conversão e Migração Sistema Mercato

    private async Task<List<BensMercato>> SelecionarBensMercatoSemIdCloud()
    {
        const string query = "SELECT * FROM pat_bens WHERE id_cloud IS NULL;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var bens = (await connection.QueryAsync<BensMercato>(query)).ToList();
            Console.WriteLine($"✅ {bens.Count} bens sem ID Cloud foram encontrados!");
            return bens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os bens: {ex.Message}");
            return new List<BensMercato>();
        }
    }

    public async Task EnviarBensMercatoParaCloud()
    {
        var bens = await SelecionarBensMercatoSemIdCloud();
        if (!bens.Any())
        {
            Console.WriteLine("❌ Nenhum bem sem ID Cloud encontrado!");
            return;
        }

        foreach (var item in bens)
        {
            try
            {
                var payload = await MontarPayloadBemMercato(item);
                await EnviarBemMercato(payload, item.codigo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar o bem {item.codigo}: {ex.Message}");
            }
        }
    }

    #region Métodos de Seleção Auxiliares - Mercato

    public Task<string> SelecionarTipoBemMercatoByTipo(string tipo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM pat_tp_bens WHERE classificacao = @classificacao;", new { classificacao = tipo });

    public Task<string> SelecionarTipoUtilizacaoMercatoByCodigo(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM tp_classificacao WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarGrupoBensMercatoBySub(int? codigoSubgrupo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>(@"
            SELECT g.id_cloud
            FROM pat_subgrupo_bens s
            JOIN pat_grupo_bens g ON g.codigo = s.grupo_bens_cod
            WHERE s.codigo = @codigoSubgrupo;", new { codigoSubgrupo });

    public Task<string> SelecionarTipoAquisicaoMercatoByCodigo(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM pat_tp_origem WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarEstadoConservacaoMercatoByStatus(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM pat_tp_status WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarFornecedorMercatoByCodigo(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM fornecedor WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarLocalizacaoMercatoByCodigo(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM local WHERE codigo = @codigo;", new { codigo });

    public Task<GrupoBens> SelecionarDadosGrupoBensMercatoBySub(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<GrupoBens>(@"
            SELECT g.*
            FROM pat_subgrupo_bens s
            JOIN pat_grupo_bens g ON g.codigo = s.grupo_bens_cod
            WHERE s.codigo = @codigo;", new { codigo });

    public Task<SubgrupoBens> SelecionarDadosEspecieBensMercatoBySub(int? codigo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<SubgrupoBens>("SELECT * FROM pat_subgrupo_bens WHERE codigo = @codigo;", new { codigo });

    public Task<string> SelecionarMetodoDepreciacaoMercato(string tipo) =>
        _sqlHelper.QuerySingleOrDefaultAsync<string>("SELECT id_cloud FROM metodo_depreciacao_cloud WHERE tipo = @tipo;", new { tipo });

    #endregion Métodos de Seleção Auxiliares - Mercato

    private async Task<BensPOST> MontarPayloadBemMercato(BensMercato item)
    {
        var dadosEspecie = await SelecionarDadosEspecieBensMercatoBySub(item.subgrupo_bens_cod);
        var dadosGrupo = await SelecionarDadosGrupoBensMercatoBySub(item.subgrupo_bens_cod);

        var idCloudEspecieBem = dadosEspecie?.id_cloud;
        var idCloudGrupoBem = dadosGrupo.id_cloud;
        var depreciacaoAnual = dadosGrupo?.deprecia;
        var vidaUtilAnos = dadosEspecie?.vida_util_meses / 12;

        var idCloudTipoUtilizacao = item.tp_classificacao_cod != null ? await SelecionarTipoUtilizacaoMercatoByCodigo(item.tp_classificacao_cod) : null;
        var idCloudTipoBem = await SelecionarTipoBemMercatoByTipo(item.tipo);
        var idCloudTipoAquisicao = await SelecionarTipoAquisicaoMercatoByCodigo(item.tp_origem_cod);
        var idCloudEstadoConservacao = item.aquisicao_status_cod != null
            ? await SelecionarEstadoConservacaoMercatoByStatus(item.aquisicao_status_cod)
            : await SelecionarEstadoConservacaoMercatoByStatus(item.atual_status_cod);

        var idCloudFornecedor = item.fornecedor_cod != null ? await SelecionarFornecedorMercatoByCodigo(item.fornecedor_cod) : null;
        var idCloudLocalizacaoFisica = item.local_cod != null ? await SelecionarLocalizacaoMercatoByCodigo(item.local_cod) : null;
        var idCloudMetodoDepreciacao = await SelecionarMetodoDepreciacaoMercato("DEPRECIACAO");

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

    private async Task EnviarBemMercato(BensPOST bensPost, int codigoBem)
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

    public async Task<List<BensMercato>> SelecionarBensMercatoParaExclusao()
    {
        const string query = "SELECT * FROM pat_bens WHERE id_cloud IS NOT NULL AND id_cloud != '' AND id_cloud != '0';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var bens = (await connection.QueryAsync<BensMercato>(query)).ToList();
            Console.WriteLine($"✅ {bens.Count} bens encontrados para exclusão!");
            return bens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os bens: {ex.Message}");
            return null;
        }
    }

    public async Task ExcluirBensMercatoCloud()
    {
        var bens = await SelecionarBensMercatoParaExclusao();
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

    public async Task<List<BensMercato>> SelecionarBensMercatoComDataTombamento()
    {
        const string query = "SELECT * FROM pat_bens WHERE tombamento_data is not null ORDER BY tombamento_data, codigo;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var bens = (await connection.QueryAsync<BensMercato>(query)).ToList();
            Console.WriteLine($"✅ {bens.Count} bens com data de tombamento encontrados!");
            return bens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar os bens: {ex.Message}");
            return null;
        }
    }

    public async Task AguardarTombamentoMercato()
    {
        var bens = await SelecionarBensMercatoComDataTombamento();
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

    public async Task DesfazerTombamentoMercato()
    {
        var bens = await SelecionarBensMercatoComDataTombamento();
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

    public async Task TombarBensMercato()
    {
        var bens = await SelecionarBensMercatoComDataTombamento();
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

    #endregion Conversão e Migração Sistema Mercato
}