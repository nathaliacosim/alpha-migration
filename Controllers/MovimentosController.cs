using Alpha.Data;
using Alpha.Models.Alpha;
using Alpha.Models.BethaCloud;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class MovimentosController
{
    private readonly PgConnect _pgConnect;
    private readonly OdbcConnect _odbcConnect;
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _urlBase;

    public MovimentosController(PgConnect pgConnect, string token, string urlBase, OdbcConnect odbcConnect)
    {
        _pgConnect = pgConnect;
        _odbcConnect = odbcConnect;
        _token = token;
        _urlBase = urlBase;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    private async Task<List<string>> ObterMovimentos()
    {
        const string query = @"SELECT DISTINCT to_char(data_movimento, 'YYYY-MM') AS mesAno 
                               FROM (
                                    SELECT dt_baixa::DATE AS data_movimento FROM baixa_cabecalho_cloud
                                    UNION
                                    SELECT TO_DATE('01-' || mes || '-' || ano, 'DD-MM-YYYY') AS data_movimento FROM depreciacao_cabecalho_cloud WHERE finalizado is null
                                    UNION
                                    SELECT data_reav::DATE AS data_movimento FROM reavaliacao_cabecalho_cloud
                               ) AS todas_datas
                               ORDER BY mesAno;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var meses = await connection.QueryAsync<string>(query).ConfigureAwait(false);
            return meses.AsList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao obter meses com movimentação: {ex.Message}");
            return new List<string>();
        }
    }

    private async Task<List<string>> ObterMovimentosOrdenados()
    {
        Console.WriteLine("🔍 Buscando meses com movimentações...");
        var meses = await ObterMovimentos();
        return meses.OrderBy(m => m).ToList();
    }

    public async Task ProcessarMovimentos()
    {
        try
        {
            var mesesComMovimentacao = await ObterMovimentosOrdenados();
            if (!mesesComMovimentacao.Any())
            {
                Console.WriteLine("ℹ️ Nenhum mês com movimentação encontrado.");
                return;
            }

            foreach (var mesAno in mesesComMovimentacao)
            {
                await ProcessarMes(mesAno);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERRO NO PROCESSAMENTO GERAL: {ex.Message}");
            throw;
        }
    }

    private async Task ProcessarMes(string mesAno)
    {
        Console.WriteLine($"\n📅 ========= PROCESSANDO MÊS: {mesAno} =========");

        try
        {
            // 1. Processar todas as depreciações do mês primeiro
            var sucessoDepreciacoes = await ProcessarDepreciacoesDoMes(mesAno);

            if (!sucessoDepreciacoes)
            {
                Console.WriteLine($"⏭️ Pulando baixas/reavaliações do mês {mesAno} devido a falhas nas depreciações");
                return;
            }

            // 2. Processar baixas e reavaliações por dia
            await ProcessarBaixasEReavaliacoesDoMes(mesAno);

            Console.WriteLine($"\n✅ MÊS {mesAno} CONCLUÍDO COM SUCESSO");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ ERRO NO MÊS {mesAno}: {ex.Message}");
            throw;
        }
    }

    private async Task<bool> ProcessarDepreciacoesDoMes(string mesAno)
    {
        Console.WriteLine($"\n📊 PROCESSANDO DEPRECIAÇÕES {mesAno}");
        var depreciacoes = await SelecionarCabecalhoDepreciacoesAsync(mesAno);

        if (!depreciacoes.Any())
        {
            Console.WriteLine($"ℹ️ Nenhuma depreciação encontrada para {mesAno}.");
            return true;
        }

        Console.WriteLine($"📈 Encontradas {depreciacoes.Count} depreciações");

        foreach (var depreciacao in depreciacoes)
        {
            if (!await ProcessarDepreciacao(depreciacao))
            {
                return false;
            }
        }
        return true;
    }

    private async Task<bool> ProcessarDepreciacao(DepreciacaoCabecalho depreciacao)
    {
        Console.WriteLine($"\n📦 Depreciação ID: {depreciacao.id_cloud} ({depreciacao.qtd_itens} itens)");

        var itens = await SelecionarBensDepreciacoesAsync(depreciacao.id_cloud);
        if (itens.Count != depreciacao.qtd_itens)
        {
            Console.WriteLine($"⚠️ Aviso: Quantidade de itens divergente ({itens.Count}/{depreciacao.qtd_itens})");
        }

        int enviadosComSucesso = 0;

        foreach (var bem in itens)
        {
            //if (await VerificaSeDepreciacaoJaFoiEnviada(bem.id_cloud_depreciacao.ToString(), bem.id_cloud))
            //{
            //    Console.WriteLine($"ℹ️ Bem {bem.i_bem} já enviado");
            //    enviadosComSucesso++;
            //    continue;
            //}

            Console.WriteLine($"🔄 Enviando bem {bem.i_bem}...");
            if (await EnviarBemDepreciadoAsync(bem))
            {
                enviadosComSucesso++;
                Console.WriteLine($"✅ Sucesso ({enviadosComSucesso}/{itens.Count})");
            }
            else
            {
                Console.WriteLine($"\n❌ Falha crítica no bem {bem.i_bem}");
                return false;
            }
        }

       
            await FinalizarDepreciacao(depreciacao.id_cloud);
            await Task.Delay(4000); // Pequena pausa entre depreciações
        return true;       
       

    }

    private async Task<List<T>> BuscarDados<T>(string query, object parametros)
    {
        try
        {
            using var connection = _pgConnect.GetConnection();
            return (await connection.QueryAsync<T>(query, parametros).ConfigureAwait(false)).AsList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao buscar dados: {ex.Message}");
            return new List<T>();
        }
    }

    private async Task ProcessarBaixasEReavaliacoesDoMes(string mesAno)
    {
        Console.WriteLine("\n🔄 PROCESSANDO BAIXAS E REAVALIAÇÕES");

        var (ano, mes) = ParseMesAno(mesAno);
        int diasNoMes = DateTime.DaysInMonth(ano, mes);

        for (int dia = 1; dia <= diasNoMes; dia++)
        {
            string dataDia = $"{mesAno}-{dia:D2}";
            await ProcessarMovimentosDoDia(dataDia);
        }
    }

    private (int ano, int mes) ParseMesAno(string mesAno)
    {
        return (
            int.Parse(mesAno.Substring(0, 4)),
            int.Parse(mesAno.Substring(5, 2))
        );
    }

    private async Task ProcessarMovimentosDoDia(string data)
    {
        Console.WriteLine($"\n📅 DIA {data}");

        // Processar baixas
        //var baixas = await BuscaBaixas(data);
        //if (baixas.Any())
        //{
        //    Console.WriteLine($"📉 Encontradas {baixas.Count} baixas");
        //    await EnviarBaixaParaCloudAsync(baixas);
        //}

        // Processar reavaliações
        var reavaliacoes = await BuscaReavaliacoes(data);
        if (reavaliacoes.Any())
        {
            Console.WriteLine($"📈 Encontradas {reavaliacoes.Count} reavaliações");
            await EnviarReavaliacaoParaCloudAsync(reavaliacoes);
        }
    }

    private async Task<List<DepreciacaoCabecalho>> SelecionarCabecalhoDepreciacoesAsync(string mesAno)
    {
        var splitMesAno = mesAno.Split('-');
        var ano = int.Parse(splitMesAno[0]);
        var mes = int.Parse(splitMesAno[1]);
        var _mesAno = $"{mes:D2}{ano}";
        Console.WriteLine($"🔍 Buscando cabeçalho de depreciações para o período {_mesAno}...");

        const string query = @"SELECT * FROM depreciacao_cabecalho_cloud WHERE mes_ano::text = @_mesAno;";
        var parametros = new { _mesAno = _mesAno.ToString() };

        try
        {
            using var connection = _pgConnect.GetConnection();
            var resultado = await connection.QueryAsync<DepreciacaoCabecalho>(query, parametros);
            return resultado.AsList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao buscar cabeçalho de depreciações para o período {_mesAno}: {ex}");
            return new List<DepreciacaoCabecalho>();
        }
    }

    private async Task<List<DepreciacaoBem>> SelecionarBensDepreciacoesAsync(string id_cloud_depreciacao)
    {
        const string query = @"SELECT * FROM depreciacoes_cloud WHERE id_cloud_depreciacao = @id_cloud_depreciacao;";
        var parametros = new { id_cloud_depreciacao = int.Parse(id_cloud_depreciacao) };

        try
        {
            using var connection = _pgConnect.GetConnection();
            var resultado = await connection.QueryAsync<DepreciacaoBem>(query, parametros);
            return resultado.AsList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao buscar os bens da depreciação {id_cloud_depreciacao}: {ex}");
            return new List<DepreciacaoBem>();
        }
    }

    private async Task<List<BaixaBem>> BuscaBaixas(string data) => await BuscarDados<BaixaBem>("SELECT * FROM baixas_cloud WHERE data_baixa = @data", new { data }).ConfigureAwait(false);
    private async Task<List<ReavaliacaoBem>> BuscaReavaliacoes(string data) => await BuscarDados<ReavaliacaoBem>("SELECT * FROM reavaliacao_cloud WHERE data_reav_bem = @data", new { data }).ConfigureAwait(false);

    private async Task<bool> VerificaSeDepreciacaoJaFoiEnviada(string idCabecalho, string idBem)
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

    private async Task<bool> EnviarBemDepreciadoAsync(DepreciacaoBem bem, int tentativas = 0)
    {
        if (bem.valor_depreciado <= 0)
        {
            Console.WriteLine($"⚠️ O bem {bem.i_bem} não possui valor de depreciação. Não será enviado.");
            return true;
        }

        const int maxTentativas = 3;

        var payload = new DepreciacaoBemPOST
        {
            depreciacao = new DepreciacaoDepreciacaoBemPOST
            {
                id = bem.id_cloud_depreciacao
            },
            bem = new BemDepreciacaoBemPOST
            {
                id = bem.id_cloud_bem
            },
            vlDepreciado = bem.valor_depreciado,
            notaExplicativa = ""
        };

        var json = JsonConvert.SerializeObject(payload);
        Console.WriteLine($"🔗 Enviando bem {bem.i_bem} para: {json}");
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = $"{_urlBase}api/depreciacoes/{bem.id_cloud_depreciacao}/bens";

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"📄 Resposta da API para o bem {bem.i_bem}: {responseBody}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Bem {bem.i_bem} enviado com sucessoooo.");

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"⚠️ Resposta da API está vazia. Não é possível atualizar o banco.");
                    return false;
                }

                var queryUp = @"UPDATE depreciacoes_cloud SET id_cloud = @IdCloud WHERE id = @Id;";
                var parameters = new
                {
                    IdCloud = responseBody,
                    Id = bem.id
                };

                using var connection = _pgConnect.GetConnection();
                var rowsAffected = await connection.ExecuteAsync(queryUp, parameters);

                if (rowsAffected > 0)
                {
                    Console.WriteLine($"💾 Registro do bem {bem.i_bem} atualizado com id_cloud = '{responseBody}'.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"⚠️ Nenhum registro foi atualizado para o bem {bem.i_bem}.");
                    return false;
                }
            }
            else
            {
                if (responseBody.Contains("alterar o valor depreciado, o valor atualizado ficará abaixo do valor residual"))
                {
                    Console.WriteLine($"⚠️ Bem {bem.i_bem} não pode ser enviado (valor depreciado menor que o residual).");

                    var queryUp = @"UPDATE depreciacoes_cloud SET id_cloud = @IdCloud WHERE id = @Codigo;";
                    var parameters = new
                    {
                        IdCloud = "BEM-NEGATIVO",
                        Codigo = bem.id
                    };

                    using var connection = _pgConnect.GetConnection();
                    await connection.ExecuteAsync(queryUp, parameters);

                    return true;
                }

                if (responseBody.Contains("o tipo de aquisição deve ser diferente de locação ou comodato"))
                {
                    Console.WriteLine($"⚠️ Bem {bem.i_bem} não pode ser enviado (locação/comodato), será considerado como enviado.");

                    var queryUp = @"UPDATE depreciacoes_cloud SET id_cloud = @IdCloud WHERE id = @Codigo;";
                    var parameters = new
                    {
                        IdCloud = "BEM-COMODATO",
                        Codigo = bem.id
                    };

                    using var connection = _pgConnect.GetConnection();
                    await connection.ExecuteAsync(queryUp, parameters);

                    return true;
                }

                Console.WriteLine($"❌ Falha ao enviar bem {bem.i_bem}: {response.StatusCode}");

                if (tentativas < maxTentativas)
                {
                    Console.WriteLine($"🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                    await Task.Delay(1000000);
                    return await EnviarBemDepreciadoAsync(bem, tentativas + 1);
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao enviar bem {bem.i_bem}: {ex.Message}");
            return false;
        }
    }

    public async Task FinalizarDepreciacao(string idCabecalho)
    {
        var url = $"{_urlBase}api/depreciacoes/{idCabecalho}/finalizar/";
        var payload = new { mensagem = "Finalização do pacote de depreciação" };
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        const int maxTentativas = 3;
        const int delayMs = 2000; // 2 segundos

        for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
        {
            try
            {
                Console.WriteLine($"\n🔚 Finalizando depreciação {idCabecalho}... (Tentativa {tentativa})");
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Depreciação {idCabecalho} finalizada com sucesso.\n");

                    const string finaliza = @"UPDATE depreciacao_cabecalho_cloud SET finalizado = 'S' WHERE id_cloud = @id_cloud;";
                    var parameters = new { id_cloud = idCabecalho };

                    using var connection = _pgConnect.GetConnection();
                    await connection.ExecuteAsync(finaliza, parameters);

                    return; // Sucesso, sai do loop.
                }

                Console.WriteLine($"❌ Erro ao finalizar depreciação {idCabecalho}: {response.StatusCode} - {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exceção ao finalizar depreciação {idCabecalho}: {ex.Message}");
            }

            if (tentativa < maxTentativas)
            {
                Console.WriteLine($"⏳ Aguardando {delayMs / 1000} segundos para nova tentativa...");
                await Task.Delay(delayMs);
            }
        }

        Console.WriteLine($"⚠️ Todas as tentativas de finalização da depreciação {idCabecalho} foram esgotadas.");
    }


    private async Task<bool> EnviarBaixaParaCloudAsync(List<BaixaBem> baixaBens, int tentativas = 0)
    {
        const int maxTentativas = 3;

        foreach (var item in baixaBens)
        {
            var payload = new BaixaBensPOST
            {
                baixa = new BaixaIdPOST
                {
                    id = item.id_cloud_baixa
                },
                bem = new BemBaixaBensPOST
                {
                    id = item.id_cloud_bem
                },
                notaExplicativa = ""
            };

            var json = JsonConvert.SerializeObject(payload);
            Console.WriteLine($"🔗 Enviando bem {item.i_bem} para: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{_urlBase}api/baixas/{item.id_cloud_baixa}/bens";

            Console.WriteLine($"🔗 Enviando bem {item.i_bem} para: {url}");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📄 Resposta da API para o bem {item.i_bem}: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Bem {item.i_bem} enviado com sucesso.");

                    if (string.IsNullOrWhiteSpace(responseBody))
                    {
                        Console.WriteLine($"⚠️ Resposta da API está vazia. Não é possível atualizar o banco.");
                        continue;
                    }                   

                    var queryUp = @"UPDATE baixas_cloud SET id_cloud = @IdCloud WHERE id = @Codigo;";
                    var parameters = new
                    {
                        IdCloud = responseBody,
                        Codigo = item.id
                    };

                    using var connection = _pgConnect.GetConnection();
                    var rowsAffected = await connection.ExecuteAsync(queryUp, parameters);

                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"💾 Registro do bem {item.i_bem} atualizado com id_cloud = '{responseBody}'.");

                        await FinalizarBaixa(item.id_cloud_baixa.ToString());
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Nenhum registro foi atualizado para o bem {item.i_bem}.");
                    }
                } else if (responseBody.Contains("não pode ser inserido pois está baixado"))
                {
                    Console.WriteLine($"⚠️ Bem {item.i_bem} já foi baixado, será considerado como enviado.");
                    continue;
                }
                else
                {
                    Console.WriteLine($"❌ Falha ao enviar bem {item.i_bem}: {response.StatusCode} - {responseBody}");

                    if (tentativas < maxTentativas)
                    {
                        Console.WriteLine($"🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                        await Task.Delay(3000);
                        return await EnviarBaixaParaCloudAsync(new List<BaixaBem> { item }, tentativas + 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exceção ao enviar bem {item.i_bem}: {ex}");
            }
        }

        return true;
    }

    private async Task FinalizarBaixa(string id_cloud_baixa)
    {
        var url = $"{_urlBase}api/baixas/{id_cloud_baixa}/finalizada";
        var payload = new { mensagem = "Finalização do pacote de baixa" };
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            Console.WriteLine($"\n🔚 Finalizando baixa {id_cloud_baixa}...");
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Baixa {id_cloud_baixa} finalizada com sucesso.\n");
                const string finaliza = @"UPDATE baixa_cabecalho_cloud SET finalizado = 'S' WHERE id_cloud = @id_cloud;";
                var parameters = new { id_cloud = id_cloud_baixa };
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(finaliza, parameters);
            }
            else
            {
                Console.WriteLine($"❌ Erro ao finalizar baixa {id_cloud_baixa}: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao finalizar baixa {id_cloud_baixa}: {ex.Message}");
        }
    }

    private async Task<bool> EnviarReavaliacaoParaCloudAsync(List<ReavaliacaoBem> reavaliacaoBens, int tentativas = 0) 
    { 
        const int maxTentativas = 3;

        foreach (var itens in reavaliacaoBens)
        {
            var payload = new ReavaliacaoBemPOST
            {
               reavaliacao = new ReavaliacaoReavaliacaoBemPOST
               {
                   id = int.Parse(itens.id_cloud_reavaliacao),
               },
               bem = new BemReavaliacaoBemPOST
               {
                   id = int.Parse(itens.id_cloud_bem)
               },
               vlBem = itens.vlr_reav_bem,
               notaExplicativa = ""
            };

            var json = JsonConvert.SerializeObject(payload);
            Console.WriteLine($"🔗 Enviando bem {itens.i_bem} para: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{_urlBase}api/reavaliacoes/{itens.id_cloud_reavaliacao}/bens";

            Console.WriteLine($"🔗 Enviando bem {itens.i_bem} para: {url}");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📄 Resposta da API para o bem {itens.i_bem}: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Bem {itens.i_bem} enviado com sucesso.");

                    if (string.IsNullOrWhiteSpace(responseBody))
                    {
                        Console.WriteLine($"⚠️ Resposta da API está vazia. Não é possível atualizar o banco.");
                        continue;
                    }

                    var queryUp = @"UPDATE reavaliacao_cloud SET id_cloud = @IdCloud WHERE id = @Codigo;";
                    var parameters = new
                    {
                        IdCloud = responseBody,
                        Codigo = itens.id
                    };

                    using var connection = _pgConnect.GetConnection();
                    var rowsAffected = await connection.ExecuteAsync(queryUp, parameters);

                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"💾 Registro do bem {itens.i_bem} atualizado com id_cloud = '{responseBody}'.");
                        await FinalizarReavaliacao(itens.id_cloud_reavaliacao.ToString());
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Nenhum registro foi atualizado para o bem {itens.i_bem}.");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Falha ao enviar bem {itens.i_bem}: {response.StatusCode} - {responseBody}");

                    if (tentativas < maxTentativas)
                    {
                        Console.WriteLine($"🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                        await Task.Delay(3000);
                        return await EnviarReavaliacaoParaCloudAsync(reavaliacaoBens, tentativas + 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exceção ao enviar bem {itens.i_bem}: {ex}");
            }
        }

        return true;
    }

    private async Task FinalizarReavaliacao(string id_cloud_reavaliacao)
    {
        var url = $"{_urlBase}api/reavaliacao/{id_cloud_reavaliacao}/finalizar";
        var payload = new { mensagem = "Finalização do pacote de reavaliação" };
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            Console.WriteLine($"\n🔚 Finalizando reavaliação {id_cloud_reavaliacao}...");
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Reavaliação {id_cloud_reavaliacao} finalizada com sucesso.\n");
                const string finaliza = @"UPDATE reavaliacao_cabecalho_cloud SET finalizado = 'S' WHERE id_cloud = @id_cloud;";
                var parameters = new { id_cloud = id_cloud_reavaliacao };
                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(finaliza, parameters);
            }
            else
            {
                Console.WriteLine($"❌ Erro ao finalizar reavaliação {id_cloud_reavaliacao}: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao finalizar reavaliação {id_cloud_reavaliacao}: {ex.Message}");
        }
    }
}
