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
    private readonly OdbcConnect _odbcConnect;
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private const string _rota = "api/baixas";

    public BaixaController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper, OdbcConnect odbcConnect)
    {
        _pgConnect = pgConnect;
        _sqlHelper = sqlHelper;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _odbcConnect = odbcConnect;
    }

    private async Task<List<BaixasBethaDba>> SelecionarBaixasBetha()
    {
        const string query = "SELECT i_baixa, CONVERT(VARCHAR(10), data_baixa, 120) as dt_baixa, i_bem, i_motivo, historico AS observacao FROM bethadba.baixas;";
        try
        {
            using var connection = _odbcConnect.GetConnection();
            var baixas = (await connection.QueryAsync<BaixasBethaDba>(query)).ToList();
            Console.WriteLine($"✅ {baixas.Count} baixas encontradas!");
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new List<BaixasBethaDba>();
        }
    }

    public async Task InserirBaixasBetha()
    {
        var dados = await SelecionarBaixasBetha();

        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma baixa encontrada para inserção.");
            return;
        }

        using var connection = _pgConnect.GetConnection();

        foreach (var item in dados)
        {
            const string checkExistsQuery = @"SELECT COUNT(1) FROM baixa_cabecalho_cloud WHERE i_baixa = @i_baixa";
            const string insertQuery = @"INSERT INTO baixa_cabecalho_cloud 
                                           (id_cloud, mes, ano, mes_ano, observacao, dt_baixa, i_baixa, i_bem, i_motivo, id_cloud_tipo_baixa, finalizado, id_cloud_finalizacao)
                                         VALUES 
                                            (@id_cloud, @mes, @ano, @mes_ano, @observacao, @dt_baixa, @i_baixa, @i_bem, @i_motivo, @id_cloud_tipo_baixa, @finalizado, @id_cloud_finalizacao)";

            var dataBaixa = item.dt_baixa.Split('-');
            var ano = dataBaixa[0];
            var mes = dataBaixa[1].PadLeft(2, '0');
            var mes_ano = mes + ano;

            var parametros = new
            {
                id_cloud = "",
                item.i_baixa,
                item.i_bem,
                ano,
                mes,
                mes_ano,
                observacao = item.observacao?.Trim() ?? null,
                item.dt_baixa,
                item.i_motivo,
                id_cloud_tipo_baixa = (int?)null,
                finalizado = 'N',
                id_cloud_finalizacao = (string)null
            };

            try
            {
                int count = _pgConnect.ExecuteScalar<int>(checkExistsQuery, new { item.i_baixa });

                if (count == 0)
                {
                    _pgConnect.Execute(insertQuery, parametros);
                    Console.WriteLine($"✅ Registro {item.i_baixa} inserido com sucesso! 🎉");
                }
                else
                {
                    Console.WriteLine($"⚠️ Registro {item.i_baixa} já existe no banco.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao inserir baixa_cabecalho_cloud (ID {item.i_baixa}): {ex.Message}");
            }
        }
    }

    private async Task<List<BaixasCabecalho>> SelecionarBaixasBethaSemIdCloud()
    {
        const string query = "SELECT * FROM baixa_cabecalho_cloud WHERE id_cloud IS NULL OR id_cloud = '';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<BaixasCabecalho>(query)).ToList();
            Console.WriteLine($"✅ {baixas.Count} baixas sem ID Cloud foram encontradas!");
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new List<BaixasCabecalho>();
        }
    }

    public async Task EnviarBaixasBethaParaCloud()
    {
        var dados = await SelecionarBaixasBethaSemIdCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma baixa sem ID Cloud encontrada!");
            return;
        }

        foreach (var item in dados)
        {
            try
            {
                await EnviarBaixasBetha(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar a baixa {item.id}: {ex.Message}");
            }
        }
    }

    private async Task EnviarBaixasBetha(BaixasCabecalho dados)
    {
        var jsonBaixa = new BaixaPOST
        {
            tipoBaixa = new TipoBaixaPOST
            {
                id = dados.id_cloud_tipo_baixa
            },
            dhBaixa = $"{dados.dt_baixa} 00:00:00",
            motivo = dados.observacao == null || dados.observacao.Trim() == "" ? "NÃO INFORMADO." : dados.observacao
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

        var query = $"UPDATE baixa_cabecalho_cloud SET id_cloud = '{responseBody}' WHERE id = {dados.id};";
        await _sqlHelper.ExecuteScalarAsync<int>(query);

        Console.WriteLine($"✅ Baixa {dados.id} enviada com sucesso!");
    }

    private async Task<List<BaixasCabecalho>> SelecionarBaixasEnviadasBetha()
    {
        const string query = "SELECT * FROM baixa_cabecalho_cloud WHERE id_cloud IS NOT NULL ORDER BY dt_baixa;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<BaixasCabecalho>(query)).ToList();
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new List<BaixasCabecalho>();
        }
    }

    public async Task ExcluirBaixasBethaCloud()
    {
        var baixas = await SelecionarBaixasEnviadasBetha();
        if (!baixas.Any())
        {
            Console.WriteLine("❌ Nenhuma baixa enviada encontrada!");
            return;
        }
        foreach (var baixa in baixas)
        {
            var url_base = $"{_urlBase}/{baixa.id_cloud}";
            Console.WriteLine($"🔹 Excluindo bem com ID {baixa.id_cloud}...");

            var response = await _httpClient.DeleteAsync(url_base);
            Console.WriteLine($"🗑️ Requisição DELETE enviada para: {url_base}");

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📄 Resposta da API: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Baixa {baixa.id} excluída com sucesso da nuvem. Limpando id_cloud...");

                var query = @"UPDATE baixa_cabecalho_cloud SET id_cloud = NULL WHERE id = @Codigo;";
                var parameters = new { Codigo = baixa.id };

                using var connection = _pgConnect.GetConnection();
                await connection.ExecuteAsync(query, parameters);

                Console.WriteLine($"💾 id_cloud da baixa {baixa.id} removida do banco.");
            }
            else
            {
                Console.WriteLine($"❌ Falha ao excluir baixa {baixa.id} da nuvem. Status: {response.StatusCode}");
            }
        }
    }


    #region Conversão Mercato
    private async Task<List<BaixaGroupByMercato>> SelecionarBaixaBensMercato()
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
            return (await connection.QueryAsync<BaixaGroupByMercato>(query)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar a tabela 'pat_atualiza_bens': {ex.Message}");
            return new List<BaixaGroupByMercato>();
        }
    }

    public async Task InserirBaixasMercato()
    {
        var dados = await SelecionarBaixaBensMercato();

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

    private async Task<List<BaixaMercato>> SelecionarBaixasMercatoSemIdCloud()
    {
        const string query = "SELECT * FROM pat_baixas WHERE id_cloud IS NULL OR id_cloud = '';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<BaixaMercato>(query)).ToList();
            Console.WriteLine($"✅ {baixas.Count} baixas sem ID Cloud foram encontradas!");
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new List<BaixaMercato>();
        }
    }

    public async Task EnviarBaixasMercatoParaCloud()
    {
        var dados = await SelecionarBaixasMercatoSemIdCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma baixa sem ID Cloud encontrada!");
            return;
        }

        foreach (var item in dados)
        {
            try
            {
                await EnviarBaixasMercato(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar a baixa {item.id}: {ex.Message}");
            }
        }
    }

    private async Task EnviarBaixasMercato(BaixaMercato dados)
    {
        var jsonBaixa = new BaixaPOST
        {
            dhBaixa = dados.data_baixa + " 00:00:00",
            tipoBaixa = new TipoBaixaPOST
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

    private async Task<List<BaixaMercato>> SelecionarBaixasEnviadasMercato()
    {
        const string query = "SELECT * FROM pat_baixas WHERE id_cloud IS NOT NULL ORDER BY data_baixa;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<BaixaMercato>(query)).ToList();
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new List<BaixaMercato>();
        }
    }

    public async Task FinalizarBaixasMercato()
    {
        var baixas = await SelecionarBaixasEnviadasMercato();

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
    #endregion Conversão Mercato
}