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

public class BaixaBensController
{
    private readonly PgConnect _pgConnect;
    private readonly OdbcConnect _odbcConnect;
    private readonly SqlHelper _sqlHelper;
    private readonly HttpClient _httpClient;
    private readonly string _urlBase;

    public BaixaBensController(PgConnect pgConnect, string token, string urlBase, SqlHelper sqlHelper, OdbcConnect odbcConnect)
    {
        _pgConnect = pgConnect ?? throw new ArgumentNullException(nameof(pgConnect));
        _odbcConnect = odbcConnect ?? throw new ArgumentNullException(nameof(odbcConnect));
        _sqlHelper = sqlHelper ?? throw new ArgumentNullException(nameof(sqlHelper));
        _urlBase = !string.IsNullOrWhiteSpace(urlBase) ? urlBase : throw new ArgumentException("URL base inválida.", nameof(urlBase));

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    #region Betha

    public async Task<List<BaixaBensBethaDba>> SelecionarBaixaBensBetha()
    {
        const string query = "SELECT i_baixa, i_motivo, i_bem, CONVERT(VARCHAR(10), data_baixa, 120) as data_baixa, i_entidades FROM bethadba.baixas;";
        try
        {
            using var connection = _odbcConnect.GetConnection();
            var baixas = (await connection.QueryAsync<BaixaBensBethaDba>(query)).AsList();
            Console.WriteLine($"✅ {baixas.Count} baixas encontradas!");
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new();
        }
    }

    public async Task InserirBaixasBensBetha()
    {
        var baixas = await SelecionarBaixaBensBetha();
        if (baixas.Count == 0)
        {
            Console.WriteLine("⚠️ Nenhuma baixa encontrada para inserir.");
            return;
        }

        foreach (var item in baixas)
        {
            const string checkExistsQuery = "SELECT COUNT(*) FROM baixas_cloud WHERE i_baixa = @i_baixa;";
            const string insertQuery = @"INSERT INTO baixas_cloud
                                          (id_cloud, i_baixa, i_motivo, i_bem, id_cloud_bem, id_cloud_baixa, data_baixa, i_entidades)
                                         VALUES
                                          (@id_cloud, @i_baixa, @i_motivo, @i_bem, @id_cloud_bem, @id_cloud_baixa, @data_baixa, @i_entidades);";

            var parametros = new
            {
                id_cloud = "",
                item.i_baixa,
                item.i_motivo,
                item.i_bem,
                id_cloud_bem = (int?)null,
                id_cloud_baixa = (int?)null,
                item.data_baixa,
                item.i_entidades
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
                Console.WriteLine($"❌ Erro ao inserir baixa_cloud (ID {item.i_baixa}): {ex.Message}");
            }
        }
    }

    public async Task<List<BaixasCabecalho>> SelecionarCabecalhoBaixasBetha()
    {
        const string query = "SELECT * FROM baixa_cabecalho_cloud WHERE ano = '2024';";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<BaixasCabecalho>(query)).AsList();
            Console.WriteLine($"✅ {baixas.Count} baixas encontradas!");
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new();
        }
    }

    private async Task<List<BaixaBem>> ObterBensDaBaixaBethaAsync(string idCabecalho)
    {
        const string query = "SELECT * FROM baixas_cloud WHERE id_cloud_baixa = @id_cabecalho;";
        using var connection = _pgConnect.GetConnection();
        return (await connection.QueryAsync<BaixaBem>(query, new { id_cabecalho = int.Parse(idCabecalho) })).ToList();
    }

    public async Task EnviarBaixaBensBethaParaCloud()
    {
        var cabecalhos = await SelecionarCabecalhoBaixasBetha();

        if (!cabecalhos.Any())
        {
            Console.WriteLine("⚠️ Nenhuma baixa encontrada no banco.");
            return;
        }

        foreach (var cabecalho in cabecalhos)
        {
            Console.WriteLine($"\n🛠️ Processando bens da baixa {cabecalho.id}...");

            var bens = await ObterBensDaBaixaBethaAsync(cabecalho.id_cloud);

            if (bens is null || !bens.Any())
            {
                Console.WriteLine($"❌ Nenhum bem encontrado para {cabecalho.id}.");
                continue;
            }

            Console.WriteLine($"📦 {bens.Count} bens encontrados.");

            int enviados = 0;

            foreach (var bem in bens)
            {
                Console.WriteLine($"🚀 Enviando bem {bem.i_bem}...");
                if (await EnviarBemBaixadoBethaAsync(bem))
                {
                    enviados++;
                    Console.WriteLine($"📈 Progresso: {enviados}/{bens.Count} bens enviados.");
                }

                Console.WriteLine("\n");
            }
        }
    }

    private async Task<bool> EnviarBemBaixadoBethaAsync(BaixaBem bem, int tentativas = 0)
    {
        const int maxTentativas = 3;

        var payload = new BaixaBensPOST
        {
            baixa = new BaixaIdPOST
            {
                id = bem.id_cloud_baixa
            },
            bem = new BemBaixaBensPOST
            {
                id = bem.id_cloud_bem
            },
            notaExplicativa = ""
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = $"{_urlBase}api/baixas/{bem.id_cloud_baixa}/bens";
        Console.WriteLine($"🔗 Enviando para: {url}");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"📄 Resposta da API para o bem {bem.i_bem}: {responseBody}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Bem {bem.i_bem} enviado com sucesso.");

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"⚠️ Resposta da API está vazia. Não é possível atualizar o banco.");
                    return false;
                }

                var queryUp = @"UPDATE baixas_cloud SET id_cloud = @IdCloud WHERE id = @Id;";
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

            Console.WriteLine($"❌ Falha ao enviar bem {bem.i_bem}: {response.StatusCode}");

            if (tentativas < maxTentativas)
            {
                Console.WriteLine($"🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                await Task.Delay(3000);
                return await EnviarBemBaixadoBethaAsync(bem, tentativas + 1);
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao enviar bem {bem.i_bem}: {ex.Message}");
            return false;
        }
    }

    private async Task<List<BaixaBem>> SelecionarBaixasEnviadasBetha()
    {
        const string query = "SELECT * FROM baixas_cloud WHERE id_cloud != '' ORDER BY data_baixa;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            var baixas = (await connection.QueryAsync<BaixaBem>(query)).ToList();
            return baixas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as baixas: {ex.Message}");
            return new List<BaixaBem>();
        }
    }

    public async Task FinalizarBaixasBetha()
    {
        var baixas = await SelecionarBaixasEnviadasBetha();

        if (!baixas.Any())
        {
            Console.WriteLine("❌ Nenhuma baixa enviada encontrada!");
            return;
        }

        foreach (var baixa in baixas)
        {
            var url = $"{_urlBase}api/baixas/{baixa.id_cloud_baixa}/finalizada/";
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

    #endregion Betha

    #region Mercato

    public async Task<List<BaixaMercato>> SelecionarBaixasMercato()
    {
        const string query = "SELECT * FROM pat_baixas WHERE id_cloud IS NOT NULL ORDER BY data_baixa;";
        try
        {
            using var connection = _pgConnect.GetConnection();
            return (await connection.QueryAsync<BaixaMercato>(query)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar baixas: {ex.Message}");
            return new();
        }
    }

    private async Task<List<BaixaBensMercato>> ObterBensDaBaixaMercatoAsync(string idCabecalho)
    {
        const string query = "SELECT * FROM pat_atualiza_bens WHERE id_cloud_baixa = @id_cabecalho;";
        using var connection = _pgConnect.GetConnection();
        return (await connection.QueryAsync<BaixaBensMercato>(query, new { id_cabecalho = idCabecalho })).ToList();
    }

    public async Task EnviarBaixaBensMercatoParaCloud()
    {
        var cabecalhos = await SelecionarBaixasMercato();

        if (!cabecalhos.Any())
        {
            Console.WriteLine("⚠️ Nenhuma baixa encontrada no banco.");
            return;
        }

        foreach (var cabecalho in cabecalhos)
        {
            Console.WriteLine($"\n🛠️ Processando bens da baixa {cabecalho.id}...");

            var bens = await ObterBensDaBaixaMercatoAsync(cabecalho.id_cloud);

            if (bens is null || !bens.Any())
            {
                Console.WriteLine($"❌ Nenhum bem encontrado para {cabecalho.id}.");
                continue;
            }

            Console.WriteLine($"📦 {bens.Count} bens encontrados.");

            int enviados = 0;

            foreach (var bem in bens)
            {
                Console.WriteLine($"🚀 Enviando bem {bem.codigo}...");
                if (await EnviarBemBaixadoMercatoAsync(bem))
                {
                    enviados++;
                    Console.WriteLine($"📈 Progresso: {enviados}/{bens.Count} bens enviados.");
                }

                Console.WriteLine("\n");
            }
        }
    }

    private async Task<bool> EnviarBemBaixadoMercatoAsync(BaixaBensMercato bem, int tentativas = 0)
    {
        const int maxTentativas = 3;

        var payload = new BaixaBensPOST
        {
            baixa = new BaixaIdPOST
            {
                id = int.Parse(bem.id_cloud_baixa)
            },
            bem = new BemBaixaBensPOST
            {
                id = int.Parse(bem.id_cloud_bem)
            },
            notaExplicativa = ""
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = $"{_urlBase}api/baixas/{bem.id_cloud_baixa}/bens";
        Console.WriteLine($"🔗 Enviando para: {url}");

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

                var queryUp = @"UPDATE pat_atualiza_bens SET id_cloud = @IdCloud WHERE codigo = @Codigo;";
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

            Console.WriteLine($"❌ Falha ao enviar bem {bem.codigo}: {response.StatusCode}");

            if (tentativas < maxTentativas)
            {
                Console.WriteLine($"🔁 Tentando novamente ({tentativas + 1}/{maxTentativas})...");
                await Task.Delay(3000);
                return await EnviarBemBaixadoMercatoAsync(bem, tentativas + 1);
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exceção ao enviar bem {bem.codigo}: {ex.Message}");
            return false;
        }
    }

    #endregion Mercato
}