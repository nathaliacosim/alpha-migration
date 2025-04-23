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
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;
    private const string _rota = "api/depreciacoes";

    public DepreciacaoController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper)
    {
        _pgConnect = pgConnect;
        _sqlHelper = sqlHelper;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    private async Task<List<DepreciacaoMes>> SelecionarDadosDepreciacaoMes()
    {
        const string query = "SELECT ano, mes, sum(depreciacao) as total, count(bens_cod) as qtd_bens FROM pat_bens_depreciacao_mes GROUP BY ano, mes ORDER BY ano, mes;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            return (await connection.QueryAsync<DepreciacaoMes>(query)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar a tabela 'pat_bens_depreciacao_mes': {ex.Message}");
            return new List<DepreciacaoMes>();
        }
    }

    public async Task InserirDepreciacoes()
    {
        var dados = await SelecionarDadosDepreciacaoMes();

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

    private async Task<List<Depreciacao>> SelecionarDepreciacaoSemIdCloud()
    {
        const string query = "SELECT * FROM pat_cabecalho_depreciacao WHERE id_cloud IS NULL;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var depres = (await connection.QueryAsync<Depreciacao>(query)).ToList();
            Console.WriteLine($"✅ {depres.Count} depreciações sem ID Cloud foram encontradas!");
            return depres;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as depreciações: {ex.Message}");
            return new List<Depreciacao>();
        }
    }

    public async Task EnviarDepreciacoesParaCloud()
    {
        var dados = await SelecionarDepreciacaoSemIdCloud();
        if (!dados.Any())
        {
            Console.WriteLine("❌ Nenhuma depreciação sem ID Cloud encontrada!");
            return;
        }

        foreach (var item in dados)
        {
            try
            {
                await EnviarDepreciacao(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar a depreciação {item.id}: {ex.Message}");
            }
        }
    }

    private async Task EnviarDepreciacao(Depreciacao dados)
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

    private async Task<List<Depreciacao>> SelecionarDepreciacoesEnviadasParaCloud()
    {
        const string query = "SELECT * FROM pat_cabecalho_depreciacao WHERE id_cloud IS NOT NULL ORDER BY ano desc, mes desc;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var depres = (await connection.QueryAsync<Depreciacao>(query)).ToList();
            return depres;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as depreciações: {ex.Message}");
            return new List<Depreciacao>();
        }
    }

    public async Task ExcluirDepreciacoesCloud()
    {
        var dados = await SelecionarDepreciacoesEnviadasParaCloud();
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
}