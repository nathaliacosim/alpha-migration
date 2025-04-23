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
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;

    public DepreciacaoBensController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper)
    {
        _pgConnect = pgConnect ?? throw new ArgumentNullException(nameof(pgConnect));
        _sqlHelper = sqlHelper ?? throw new ArgumentNullException(nameof(sqlHelper));
        _urlBase = !string.IsNullOrWhiteSpace(urlBase) ? urlBase : throw new ArgumentException("URL base inválida.", nameof(urlBase));

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public async Task<List<Depreciacao>> SelecionarDepreciacoes()
    {
        const string query = "SELECT * FROM pat_cabecalho_depreciacao WHERE id_cloud IS NOT NULL AND finalizado = 'false' ORDER BY ano, mes;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            return (await connection.QueryAsync<Depreciacao>(query)).ToList();
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

                if (await VerificaSeJaFoiEnviado(bem.id_cloud_depreciacao, bem.id_cloud_bem))
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
            }

            if (enviados == cabecalho.qtd_bens)
            {
                Console.WriteLine($"✅ Todos os {enviados} bens da depreciação {cabecalho.mes_ano} foram enviados com sucesso.");
                await FinalizarDepreciacao(cabecalho.id_cloud);
                await Task.Delay(2000);
            }
            else
            {
                Console.WriteLine($"⚠️ {enviados}/{cabecalho.qtd_bens} bens enviados para {cabecalho.mes_ano}. Finalização **não** será feita.");
                Console.WriteLine($"⚠️ Verifique os bens que não foram enviados e tente novamente.");
                return;
            }
        }
    }


    private async Task<bool> VerificaSeJaFoiEnviado(string idCabecalho, string idBem)
    {
        var url = $"{_urlBase}api/depreciacoes/{idCabecalho}/bens/{idBem}";
        Console.WriteLine($"🔍 Verificando se o bem {idBem} já foi enviado...");
        Console.WriteLine($"URL: {url}");

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                Console.WriteLine($"⚠️ Bem {idBem} não encontrado na API. Status: {response.StatusCode} - {response.ReasonPhrase}");
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
        const int maxTentativas = 5;

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
                return true;
            }

            Console.WriteLine($"❌ Falha ao enviar bem {bem.codigo}: {response.StatusCode} - {response.ReasonPhrase}");

            if (tentativas < maxTentativas)
            {
                Console.WriteLine($"\n🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                await Task.Delay(3000);
                return await EnviarBemDepreciadoAsync(bem, tentativas + 1);
            }

            return false;
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

}
