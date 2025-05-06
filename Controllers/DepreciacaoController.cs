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

public class DepreciacaoController
{
    private readonly PgConnect _pgConnect;
    private readonly OdbcConnect _odbcConnect;
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private const string _rota = "api/depreciacoes";

    public DepreciacaoController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper, OdbcConnect odbcConnect)
    {
        _pgConnect = pgConnect;
        _sqlHelper = sqlHelper;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _odbcConnect = odbcConnect;
    }

    #region Conversão Betha

    public async Task<List<DepreciacaoCabecalhoBethaDba>> SelecionarDepreciacoesMesBetha()
    {
        const string query = @"SELECT DISTINCT
                                 RIGHT('0' + CAST(MONTH(data_depr) AS VARCHAR), 2) AS mes,
                                 CAST(YEAR(data_depr) AS VARCHAR) AS ano,
                                 RIGHT('0' + CAST(MONTH(data_depr) AS VARCHAR), 2) + CAST(YEAR(data_depr) AS VARCHAR) AS mes_ano
                               FROM bethadba.depreciacoes
                               ORDER BY ano, mes ASC;";
        try
        {
            using var connection = _odbcConnect.GetConnection();
            var depreciacoes = await connection.QueryAsync<DepreciacaoCabecalhoBethaDba>(query);
            Console.WriteLine($"✅ {depreciacoes.Count()} depreciações encontradas!");
            return depreciacoes.AsList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as depreciações: {ex.Message}");
            return new List<DepreciacaoCabecalhoBethaDba>();
        }
    }

    public async Task InserirDepreciacoes()
    {
        var dados = await SelecionarDepreciacoesMesBetha();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma depreciação encontrada para inserção.");
            return;
        }

        foreach (var item in dados)
        {
            const string checkExistsQuery = @"SELECT COUNT(1) FROM depreciacao_cabecalho_cloud WHERE mes_ano = @mes_ano";
            const string insertQuery = @"INSERT INTO depreciacao_cabecalho_cloud (id_cloud, mes, ano, mes_ano, observacao)
                                         VALUES (@id_cloud, @mes, @ano, @mes_ano, @observacao)";

            var parametros = new
            {
                id_cloud = "",
                item.mes,
                item.ano,
                item.mes_ano,
                observacao = ""
            };

            try
            {
                int count = await _sqlHelper.ExecuteScalarAsync<int>(checkExistsQuery, new { item.mes_ano });

                if (count == 0)
                {
                    Console.WriteLine($"🟢 Depreciação {item.mes_ano} não encontrada, inserindo...");
                    await _pgConnect.ExecuteInsertAsync(insertQuery, parametros);
                }
                else
                {
                    Console.WriteLine($"ℹ️ Depreciação {item.mes_ano} já existe! Pulando...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao inserir a depreciação {item.mes_ano}: {ex.Message}");
            }
        }
    }

    private async Task<List<DepreciacaoCabecalho>> SelecionarDepreciacaoBethaSemIdCloud()
    {
        const string query = "SELECT * FROM depreciacao_cabecalho_cloud WHERE id_cloud IS NULL OR id_cloud = '';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var depres = (await connection.QueryAsync<DepreciacaoCabecalho>(query)).ToList();
            Console.WriteLine($"✅ {depres.Count} depreciações sem ID Cloud foram encontradas!");
            return depres;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as depreciações: {ex.Message}");
            return new List<DepreciacaoCabecalho>();
        }
    }

    public async Task EnviarDepreciacoesBethaParaCloud()
    {
        var dados = await SelecionarDepreciacaoBethaSemIdCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma depreciação sem ID Cloud encontrada!");
            return;
        }
        foreach (var item in dados)
        {
            try
            {
                await EnviarDepreciacaoBetha(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar a depreciação {item.id}: {ex.Message}");
            }
        }
    }

    private async Task EnviarDepreciacaoBetha(DepreciacaoCabecalho dados)
    {
        var jsonDepreciacao = new DepreciacoesPOST
        {
            mesAno = $"{dados.mes_ano}"
        };

        var json = JsonConvert.SerializeObject(jsonDepreciacao);
        Console.WriteLine($"📤 Enviando depreciação {dados.id} para a nuvem...");

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_urlBase, content);

        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"📄 Resposta da API: {responseBody}");

        if (!responseBody.Contains("message"))
        {
            Console.WriteLine($"✅ Depreciação {dados.id} enviada com sucesso!");

            var query = $"UPDATE depreciacao_cabecalho_cloud SET id_cloud = '{responseBody}' WHERE id = {dados.id};";
            await _sqlHelper.ExecuteScalarAsync<int>(query);
        }
        else
        {
            Console.WriteLine($"❌ Erro ao enviar a depreciação {dados.id}: {responseBody}");
        }
    }

    private async Task<List<DepreciacaoCabecalho>> SelecionarDepreciacoesBethaEnviadasParaCloud()
    {
        const string query = "SELECT * FROM depreciacao_cabecalho_cloud WHERE id_cloud IS NOT NULL ORDER BY ano desc, mes desc;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var depres = (await connection.QueryAsync<DepreciacaoCabecalho>(query)).ToList();
            return depres;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as depreciações: {ex.Message}");
            return new List<DepreciacaoCabecalho>();
        }
    }

    public async Task ExcluirDepreciacoesBethaCloud()
    {
        var dados = await SelecionarDepreciacoesBethaEnviadasParaCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma depreciação enviada para a nuvem encontrada!");
            return;
        }

        foreach (var item in dados)
        {
            var idCloud = item.id_cloud;
            var url = $"{_urlBase}/{idCloud}";

            Console.WriteLine($"🗑️ Excluindo depreciação {item.mes_ano} da nuvem...");
            var response = await _httpClient.DeleteAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📄 Resposta da API: {responseBody}");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Depreciação {item.mes_ano} excluída com sucesso!");
                var query = $"UPDATE depreciacao_cabecalho_cloud SET id_cloud = null WHERE id_cloud = '{idCloud}';";
                await _sqlHelper.ExecuteScalarAsync<int>(query);

                await Task.Delay(5000).ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine($"❌ Erro ao excluir a depreciação {item.mes_ano}: {responseBody}");
            }
        }
    }

    #endregion Conversão Betha

    #region Conversão Mercato

    private async Task<List<DepreciacaoMesMercato>> SelecionarDadosDepreciacaoMesMercato()
    {
        const string query = "SELECT ano, mes, sum(depreciacao) as total, count(bens_cod) as qtd_bens FROM pat_bens_depreciacao_mes GROUP BY ano, mes ORDER BY ano, mes;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            return (await connection.QueryAsync<DepreciacaoMesMercato>(query)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar a tabela 'pat_bens_depreciacao_mes': {ex.Message}");
            return new List<DepreciacaoMesMercato>();
        }
    }

    public async Task InserirDepreciacoesMercato()
    {
        var dados = await SelecionarDadosDepreciacaoMesMercato();

        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma depreciação encontrada para inserção.");
            return;
        }

        using var connection = _pgConnect.GetConnection();

        foreach (var item in dados)
        {
            var mes = item.mes;
            var ano = item.ano;
            var mesAno = $"{mes:D2}{ano}";
            var descricaoMesAno = $"{mes:D2}/{ano}";

            const string verificaSeExisteQuery = @"
            SELECT id
            FROM pat_cabecalho_depreciacao
            WHERE mes = @Mes AND ano = @Ano;
        ";

            const string inserirRegistroQuery = @"
            INSERT INTO pat_cabecalho_depreciacao
            (mes, ano, mes_ano, descricao, valor_total, qtd_bens, finalizado)
            VALUES
            (@Mes, @Ano, @MesAno, @Descricao, @Total, @QtdBens, false);
        ";

            try
            {
                Console.WriteLine($"🔍 Verificando existência da depreciação {descricaoMesAno}...");

                var id = await connection.ExecuteScalarAsync<int?>(
                    verificaSeExisteQuery,
                    new { Mes = mes, Ano = ano });

                if (id == null)
                {
                    await connection.ExecuteAsync(inserirRegistroQuery, new
                    {
                        Mes = mes,
                        Ano = ano,
                        MesAno = mesAno,
                        Descricao = $"Depreciação {descricaoMesAno}",
                        Total = item.total,
                        QtdBens = item.qtd_bens
                    });

                    Console.WriteLine($"✅ Depreciação {descricaoMesAno} inserida com sucesso.");
                }
                else
                {
                    Console.WriteLine($"ℹ️ Depreciação {descricaoMesAno} já existente. Nenhuma ação tomada.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar depreciação {descricaoMesAno}: {ex.Message}");
            }
        }
    }

    private async Task<List<DepreciacaoMercato>> SelecionarDepreciacaoMercatoSemIdCloud()
    {
        const string query = "SELECT * FROM pat_cabecalho_depreciacao WHERE id_cloud IS NULL;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var depres = (await connection.QueryAsync<DepreciacaoMercato>(query)).ToList();
            Console.WriteLine($"✅ {depres.Count} depreciações sem ID Cloud foram encontradas!");
            return depres;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as depreciações: {ex.Message}");
            return new List<DepreciacaoMercato>();
        }
    }

    public async Task EnviarDepreciacoesMercatoParaCloud()
    {
        var dados = await SelecionarDepreciacaoMercatoSemIdCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma depreciação sem ID Cloud encontrada!");
            return;
        }

        foreach (var item in dados)
        {
            try
            {
                await EnviarDepreciacaoMercato(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar a depreciação {item.id}: {ex.Message}");
            }
        }
    }

    private async Task EnviarDepreciacaoMercato(DepreciacaoMercato dados)
    {
        var jsonDepreciacao = new DepreciacoesPOST
        {
            mesAno = $"{dados.mes_ano}"
        };

        var json = JsonConvert.SerializeObject(jsonDepreciacao);
        Console.WriteLine($"📤 Enviando depreciação {dados.id} para a nuvem...");

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_urlBase, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"📄 Resposta da API: {responseBody}");

        if (responseBody.Contains("message"))
        {
            Console.WriteLine($"❌ Erro ao enviar a depreciação {dados.id}: {responseBody}");
        }

        var query = $"UPDATE pat_cabecalho_depreciacao SET id_cloud = '{responseBody}' WHERE id = {dados.id};";
        await _sqlHelper.ExecuteScalarAsync<int>(query);

        Console.WriteLine($"✅ Depreciação {dados.id} enviada com sucesso!");
    }

    private async Task<List<DepreciacaoMercato>> SelecionarDepreciacoesMercatoEnviadasParaCloud()
    {
        const string query = "SELECT * FROM pat_cabecalho_depreciacao WHERE id_cloud IS NOT NULL ORDER BY ano desc, mes desc;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var depres = (await connection.QueryAsync<DepreciacaoMercato>(query)).ToList();
            return depres;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as depreciações: {ex.Message}");
            return new List<DepreciacaoMercato>();
        }
    }

    public async Task ExcluirDepreciacoesMercatoCloud()
    {
        var dados = await SelecionarDepreciacoesMercatoEnviadasParaCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma depreciação enviada para a nuvem encontrada!");
            return;
        }

        foreach (var item in dados)
        {
            var idCloud = item.id_cloud;
            var url = $"{_urlBase}/{idCloud}";

            Console.WriteLine($"🗑️ Excluindo depreciação {item.mes_ano} da nuvem...");
            var response = await _httpClient.DeleteAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📄 Resposta da API: {responseBody}");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Depreciação {item.mes_ano} excluída com sucesso!");
                var query = $"UPDATE pat_cabecalho_depreciacao SET id_cloud = null WHERE id_cloud = '{idCloud}';";
                await _sqlHelper.ExecuteScalarAsync<int>(query);
                await Task.Delay(5000).ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine($"❌ Erro ao excluir a depreciação {item.mes_ano}: {responseBody}");
            }
        }
    }

    #endregion Conversão Mercato
}