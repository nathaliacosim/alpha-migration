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

public class DepreciacaoBensController
{
    private readonly PgConnect _pgConnect;
    private readonly OdbcConnect _odbcConnect;
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;

    public DepreciacaoBensController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper, OdbcConnect odbcConnect)
    {
        _pgConnect = pgConnect ?? throw new ArgumentNullException(nameof(pgConnect));
        _odbcConnect = odbcConnect ?? throw new ArgumentNullException(nameof(odbcConnect));
        _sqlHelper = sqlHelper ?? throw new ArgumentNullException(nameof(sqlHelper));
        _urlBase = !string.IsNullOrWhiteSpace(urlBase) ? urlBase : throw new ArgumentException("URL base inválida.", nameof(urlBase));

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    #region Conversão Betha

    public async Task<List<DepreciacaoBensBethaDba>> SelecionarDepreciacoesBensBetha()
    {
        const string query = @"SELECT i_depreciacao, i_bem, data_depr, valor_calc, i_entidades,
                            RIGHT('0' + CAST(MONTH(data_depr) AS VARCHAR), 2) AS mes,
                            CAST(YEAR(data_depr) AS VARCHAR) AS ano
                           FROM bethadba.depreciacoes;";

        try
        {
            using var connection = _odbcConnect.GetConnection();
            var depreciacoes = await connection.QueryAsync<DepreciacaoBensBethaDba>(query);
            Console.WriteLine($"✅ {depreciacoes.Count()} depreciações de bens encontradas!");

            return depreciacoes.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as depreciações de bens: {ex.Message}");
            return new List<DepreciacaoBensBethaDba>();
        }
    }

    public async Task InserirDepreciacoesBensBetha()
    {
        var dados = await SelecionarDepreciacoesBensBetha();
        if (dados.Count == 0)
        {
            Console.WriteLine("⚠️ Nenhuma depreciação encontrada no banco.");
            return;
        }

        foreach (var item in dados)
        {
            const string checkExistsQuery = @"SELECT COUNT(1) FROM depreciacoes_cloud WHERE i_depreciacao = @i_depreciacao;";
            const string insertQuery = @"INSERT INTO depreciacoes_cloud
                                    (id_cloud, i_depreciacao, i_bem, id_cloud_depreciacao, data_depreciacao, valor_depreciado, i_entidades)
                                     VALUES
                                    (@id_cloud, @i_depreciacao, @i_bem, @id_cloud_depreciacao, @data_depreciacao, @valor_depreciado, @i_entidades)";

            var parametros = new
            {
                id_cloud = "",
                item.i_depreciacao,
                item.i_bem,
                id_cloud_depreciacao = (int?)null,
                data_depreciacao = item.data_depr,
                valor_depreciado = item.valor_calc,
                item.i_entidades
            };

            try
            {
                int count = _pgConnect.ExecuteScalar<int>(checkExistsQuery, new { item.i_depreciacao });
                if (count == 0)
                {
                    _pgConnect.Execute(insertQuery, parametros);
                    Console.WriteLine($"✅ Depreciação de bens {item.i_depreciacao} inserida com sucesso!");
                }
                else
                {
                    Console.WriteLine($"⚠️ Depreciação de bens {item.i_depreciacao} já existe no banco.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao inserir a depreciação de bens: {ex.Message}");
            }
        }
    }

    #endregion Conversão Betha

    #region Conversão Mercato

    public async Task<List<DepreciacaoMercato>> SelecionarDepreciacoes()
    {
        const string query = "SELECT * FROM pat_cabecalho_depreciacao WHERE id_cloud IS NOT NULL AND finalizado = 'false' ORDER BY ano, mes;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            return (await connection.QueryAsync<DepreciacaoMercato>(query)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar depreciações: {ex.Message}");
            return new();
        }
    }

    public async Task EnviarDepreciacaoBensParaCloud()
    {
        var cabecalhos = await SelecionarDepreciacoes();

        if (!cabecalhos.Any())
        {
            Console.WriteLine("⚠️ Nenhuma depreciação encontrada no banco.");
            return;
        }

        foreach (var cabecalho in cabecalhos)
        {
            Console.WriteLine($"\n🛠️ Processando bens da depreciação {cabecalho.mes_ano}...");

            var bens = await ObterBensDaDepreciacaoAsync(cabecalho.id_cloud);

            if (bens is null || !bens.Any())
            {
                Console.WriteLine($"❌ Nenhum bem encontrado para {cabecalho.mes_ano}.");
                continue;
            }

            Console.WriteLine($"📦 {bens.Count}/{cabecalho.qtd_bens} bens encontrados.");

            int enviados = 0;

            foreach (var bem in bens)
            {
                Console.WriteLine($"🔍 Verificando se o bem {bem.codigo} já foi enviado...");

                if (await VerificaSeJaFoiEnviado(bem.id_cloud_depreciacao, bem.id_cloud))
                {
                    Console.WriteLine($"ℹ️ Bem {bem.codigo} já foi enviado anteriormente.");
                    enviados++;
                    continue;
                }

                Console.WriteLine($"🚀 Enviando bem {bem.codigo}...");
                if (await EnviarBemDepreciadoAsync(bem))
                {
                    enviados++;
                    Console.WriteLine($"📈 Progresso: {enviados}/{bens.Count} bens enviados.");
                }

                Console.WriteLine("\n");
            }

            if (enviados == cabecalho.qtd_bens)
            {
                Console.WriteLine($"✅ Todos os {enviados} bens da depreciação {cabecalho.mes_ano} foram enviados com sucesso.");
                await FinalizarDepreciacao(cabecalho.id_cloud);
                await Task.Delay(2000);
            }
            else
            {
                Console.WriteLine($"\n\n\n⚠️ {enviados}/{cabecalho.qtd_bens} bens enviados para {cabecalho.mes_ano}. Finalização **não** será feita.");
                Console.WriteLine($"⚠️ Verifique os bens que não foram enviados e tente novamente.");
                return;
            }
        }
    }

    private async Task<bool> VerificaSeJaFoiEnviado(string idCabecalho, string idBem)
    {
        if (idBem == null)
        {
            Console.WriteLine($"❌ ID do bem é nulo. Não é possível verificar se foi enviado.");
            return false;
        }

        var url = $"{_urlBase}api/depreciacoes/{idCabecalho}/bens/{idBem}";
        Console.WriteLine($"URL: {url}");

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Bem {idBem} já foi enviado!\n");
                return true;
            }
            else
            {
                Console.WriteLine($"❌ Bem {idBem} não encontrado na API. Status: {response.StatusCode}\n");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao verificar o bem {idBem}: {ex.Message}");
            return false;
        }
    }

    private async Task<List<DepreciacaoBens>> ObterBensDaDepreciacaoAsync(string idCabecalho)
    {
        const string query = "SELECT * FROM pat_bens_depreciacao_mes WHERE id_cloud_depreciacao = @id_cabecalho;";
        using var connection = _pgConnect.GetConnection();
        return (await connection.QueryAsync<DepreciacaoBens>(query, new { id_cabecalho = idCabecalho })).ToList();
    }

    private async Task<bool> EnviarBemDepreciadoAsync(DepreciacaoBens bem, int tentativas = 0)
    {
        if (bem.depreciacao <= 0)
        {
            Console.WriteLine($"⚠️ O bem {bem.codigo} não possui valor de depreciação. Não será enviado.");
            return true; // considera na contagem
        }

        const int maxTentativas = 3;

        var payload = new DepreciacaoBemPOST
        {
            depreciacao = new DepreciacaoDepreciacaoBemPOST
            {
                id = int.Parse(bem.id_cloud_depreciacao)
            },
            bem = new BemDepreciacaoBemPOST
            {
                id = int.Parse(bem.id_cloud_bem)
            },
            vlDepreciado = bem.depreciacao,
            notaExplicativa = ""
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = $"{_urlBase}api/depreciacoes/{bem.id_cloud_depreciacao}/bens";

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"📄 Resposta da API para o bem {bem.codigo}: {responseBody}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Bem {bem.codigo} enviado com sucesso.");

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"⚠️ Resposta da API está vazia. Não é possível atualizar o banco.");
                    return false;
                }

                var queryUp = @"UPDATE pat_bens_depreciacao_mes SET id_cloud = @IdCloud WHERE codigo = @Codigo;";
                var parameters = new
                {
                    IdCloud = responseBody,
                    Codigo = bem.codigo
                };

                using var connection = _pgConnect.GetConnection();
                var rowsAffected = await connection.ExecuteAsync(queryUp, parameters);

                if (rowsAffected > 0)
                {
                    Console.WriteLine($"💾 Registro do bem {bem.codigo} atualizado com id_cloud = '{responseBody}'.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"⚠️ Nenhum registro foi atualizado para o bem {bem.codigo}.");
                    return false;
                }
            }
            else
            {
                if (responseBody.Contains("alterar o valor depreciado, o valor atualizado ficará abaixo do valor residual"))
                {
                    Console.WriteLine($"⚠️ Bem {bem.codigo} não pode ser enviado (valor depreciado menor que o residual).");

                    var queryUp = @"UPDATE pat_bens_depreciacao_mes SET id_cloud = @IdCloud WHERE codigo = @Codigo;";
                    var parameters = new
                    {
                        IdCloud = "VALOR-NEGATIVO",
                        Codigo = bem.codigo
                    };

                    using var connection = _pgConnect.GetConnection();
                    await connection.ExecuteAsync(queryUp, parameters);

                    return true;
                }

                if (responseBody.Contains("o tipo de aquisição deve ser diferente de locação ou comodato"))
                {
                    Console.WriteLine($"⚠️ Bem {bem.codigo} não pode ser enviado (locação/comodato), será considerado como enviado.");

                    var queryUp = @"UPDATE pat_bens_depreciacao_mes SET id_cloud = @IdCloud WHERE codigo = @Codigo;";
                    var parameters = new
                    {
                        IdCloud = "BEM-IGNORADO",
                        Codigo = bem.codigo
                    };

                    using var connection = _pgConnect.GetConnection();
                    await connection.ExecuteAsync(queryUp, parameters);

                    return true;
                }

                Console.WriteLine($"❌ Falha ao enviar bem {bem.codigo}: {response.StatusCode}");

                if (tentativas < maxTentativas)
                {
                    Console.WriteLine($"🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                    await Task.Delay(3000);
                    return await EnviarBemDepreciadoAsync(bem, tentativas + 1);
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao enviar bem {bem.codigo}: {ex.Message}");
            return false;
        }
    }

    public async Task FinalizarDepreciacao(string idCabecalho)
    {
        var url = $"{_urlBase}api/depreciacoes/{idCabecalho}/finalizar/";
        var payload = new { mensagem = "Finalização do pacote de depreciação" };
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            Console.WriteLine($"\n🔚 Finalizando depreciação {idCabecalho}...");
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Depreciação {idCabecalho} finalizada com sucesso.\n");
                await MarcarDepreciacaoComoFinalizadaAsync(idCabecalho);
            }
            else
            {
                Console.WriteLine($"❌ Erro ao finalizar depreciação {idCabecalho}: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao finalizar depreciação {idCabecalho}: {ex.Message}");
        }
    }

    private async Task MarcarDepreciacaoComoFinalizadaAsync(string idCabecalho)
    {
        const string query = @"
        UPDATE pat_cabecalho_depreciacao
        SET finalizado = TRUE
        WHERE id_cloud = @id_cloud;
    ";

        try
        {
            using var connection = _pgConnect.GetConnection();
            var result = await connection.ExecuteAsync(query, new { id_cloud = idCabecalho });

            if (result > 0)
                Console.WriteLine($"📝 Status de finalização salvo no banco para {idCabecalho}.");
            else
                Console.WriteLine($"⚠️ Nenhum registro atualizado no banco para {idCabecalho}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao atualizar status de finalização no banco: {ex.Message}");
        }
    }

    #endregion Conversão Mercato
}