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

public class BaixaController
{
    private readonly PgConnect _pgConnect;
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private const string _rota = "api/baixas";

    public BaixaController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper)
    {
        _pgConnect = pgConnect;
        _sqlHelper = sqlHelper;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    private async Task<List<BaixaGroupBy>> SelecionarBaixaBens()
    {
        const string query = @"
            SELECT atualiza_data as data_baixa, tp_baixa as tipo_baixa
            FROM pat_atualiza_bens
            WHERE anulado is null
            GROUP BY atualiza_data, tp_baixa
            ORDER BY atualiza_data;
        ";
        try
        {
            using var connection = _pgConnect.GetConnection();
            return (await connection.QueryAsync<BaixaGroupBy>(query)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar a tabela 'pat_atualiza_bens': {ex.Message}");
            return new List<BaixaGroupBy>();
        }
    }

    public async Task InserirBaixas()
    {
        var dados = await SelecionarBaixaBens();

        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma baixa encontrada para inserção.");
            return;
        }

        using var connection = _pgConnect.GetConnection();

        foreach (var item in dados)
        {
            var descricaoMotivo = "Baixado.";
            var idCloudTipoBaixa = 0;
            if (item.tipo_baixa == 2)
            {
                descricaoMotivo = "Baixado por motivos de: Doação.";
                idCloudTipoBaixa = 41879;
            }
            if (item.tipo_baixa == 3)
            {
                descricaoMotivo = "Baixado por motivos de: Furto/Roubo.";
                idCloudTipoBaixa = 41880;
            }
            if (item.tipo_baixa == 5)
            {
                descricaoMotivo = "Baixado por motivos de: Inservível/Obsoleto.";
                idCloudTipoBaixa = 41878;
            }
            if (item.tipo_baixa == 8)
            {
                descricaoMotivo = "Baixado por motivos de: Depreciado.";
                idCloudTipoBaixa = 41877;
            }

            const string inserirRegistroQuery = @"INSERT INTO pat_baixas
                                                    (id_cloud, data_baixa, codigo_tp_baixa, id_cloud_tp_baixa, descricao_motivo)
                                                  VALUES
                                                    (@id_cloud, @data_baixa, @codigo_tp_baixa, @id_cloud_tp_baixa, @descricao_motivo);";

            try
            {
                await connection.ExecuteAsync(inserirRegistroQuery, new
                {
                    id_cloud = "",
                    item.data_baixa,
                    codigo_tp_baixa = item.tipo_baixa,
                    id_cloud_tp_baixa = idCloudTipoBaixa,
                    descricao_motivo = descricaoMotivo
                });

                Console.WriteLine($"✅ Baixa {descricaoMotivo} inserida com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar baixa {descricaoMotivo}: {ex.Message}");
            }
        }
    }

    private async Task<List<Baixa>> SelecionarBaixasSemIdCloud()
    {
        const string query = "SELECT * FROM pat_baixas WHERE id_cloud IS NULL OR id_cloud = '';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<Baixa>(query)).ToList();
            Console.WriteLine($"✅ {baixas.Count} baixas sem ID Cloud foram encontradas!");
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new List<Baixa>();
        }
    }

    public async Task EnviarBaixasParaCloud()
    {
        var dados = await SelecionarBaixasSemIdCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma baixa sem ID Cloud encontrada!");
            return;
        }

        foreach (var item in dados)
        {
            try
            {
                await EnviarBaixas(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar a baixa {item.id}: {ex.Message}");
            }
        }
    }

    private async Task EnviarBaixas(Baixa dados)
    {
        var jsonBaixa = new BaixaPOST
        {
            dhBaixa = dados.data_baixa + " 00:00:00",
            tipoBaixa = new TipoBaixa
            {
                id = int.Parse(dados.id_cloud_tp_baixa)
            },
            motivo = dados.descricao_motivo
        };

        var json = JsonConvert.SerializeObject(jsonBaixa);
        Console.WriteLine($"📤 Enviando baixa {dados.id} para a nuvem...");

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_urlBase, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"📄 Resposta da API: {responseBody}");

        if (responseBody.Contains("message"))
        {
            Console.WriteLine($"❌ Erro ao enviar a baixa {dados.id}: {responseBody}");
        }

        var query = $"UPDATE pat_baixas SET id_cloud = '{responseBody}' WHERE id = {dados.id};";
        await _sqlHelper.ExecuteScalarAsync<int>(query);

        Console.WriteLine($"✅ Baixa {dados.id} enviada com sucesso!");
    }

    private async Task<List<Baixa>> SelecionarBaixasEnviadas()
    {
        const string query = "SELECT * FROM pat_baixas WHERE id_cloud IS NOT NULL ORDER BY data_baixa;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<Baixa>(query)).ToList();
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new List<Baixa>();
        }
    }

    public async Task FinalizarBaixas()
    {
        var baixas = await SelecionarBaixasEnviadas();

        if (!baixas.Any())
        {
            Console.WriteLine("❌ Nenhuma baixa enviada encontrada!");
            return;
        }

        foreach (var baixa in baixas)
        {
            var url = $"{_urlBase}/{baixa.id_cloud}/finalizada/";
            Console.WriteLine($"📤 URL: {url}\n");
            var payload = new { mensagem = "Finalização do pacote de baixa" };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            Console.WriteLine($"📤 Enviando finalização da baixa {baixa.id} para a nuvem...");
            try
            {
                Console.WriteLine($"\n🔚 Finalizando baixa {baixa.id_cloud}...");
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Baixa {baixa.id_cloud} finalizada com sucesso.\n");
                }
                else
                {
                    Console.WriteLine($"❌ Erro ao finalizar baixa {baixa.id_cloud}: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exceção ao finalizar baixa {baixa.id_cloud}: {ex.Message}");
            }
        }
    }
}