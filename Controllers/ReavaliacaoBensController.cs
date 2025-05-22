using Alpha.Data;
using Alpha.Models.Alpha;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Controllers;

public class ReavaliacaoBensController
{
    private readonly PgConnect _pgConnect;
    private readonly OdbcConnect _odbcConnect;
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _urlBase;
    private readonly string _rota = "api/reavaliacoes";

    public ReavaliacaoBensController(PgConnect pgConnect, string token, string urlBase, OdbcConnect odbcConnect)
    {
        _pgConnect = pgConnect;
        _odbcConnect = odbcConnect;
        _urlBase = $"{urlBase}{_rota}";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public async Task<List<ReavaliacaoBemBethaDba>> SelecionarReavaliacaoBensBetha()
    {
        const string query = "SELECT * FROM bethadba.reav_bens;";
        try
        {
            using var connection = _odbcConnect.GetConnection();
            var reavaliacoes = (await connection.QueryAsync<ReavaliacaoBemBethaDba>(query)).AsList();
            Console.WriteLine($"✅ {reavaliacoes.Count} reavaliações de bens encontradas!");
            return reavaliacoes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao selecionar as reavaliações de bens: {ex.Message}");
            return [];
        }
    }

    public async Task InserirReavaliacaoBens()
    {
        var reavaliacoes = await SelecionarReavaliacaoBensBetha();
        if (reavaliacoes.Count == 0)
        {
            Console.WriteLine("⚠️ Nenhuma reavaliação de bens encontrada para inserir.");
            return;
        }

        foreach (var reavaliacao in reavaliacoes)
        {
            const string checkExistsQuery = "SELECT COUNT(*) FROM reavaliacao_cloud WHERE i_reav_bem = @i_reav_bem;";
            const string insertQuery = @"INSERT INTO reavaliacao_cloud
                                          (id_cloud, id_cloud_reavaliacao, i_reav_bem, i_bem, data_reav_bem, vlr_reav_bem, vlr_reav_resid, 
                                           valor_resid_ant, valor_deprec_ant, vlr_atual_ant, motivo_reav_bem, i_comissoes, matricula_pessoal, 
                                           nro_portaria, dt_portaria, tipo_reav, novo_perc_deprec, i_entidades, i_incorporacoes, vida_util_novo, 
                                           vida_util_ant, perc_deprec_ant, valor_depreciavel)
                                          VALUES 
                                          (@id_cloud, @id_cloud_reavaliacao, @i_reav_bem, @i_bem, @data_reav_bem, @vlr_reav_bem, @vlr_reav_resid, 
                                           @valor_resid_ant, @valor_deprec_ant, @vlr_atual_ant, @motivo_reav_bem, @i_comissoes, @matricula_pessoal, 
                                           @nro_portaria, @dt_portaria, @tipo_reav, @novo_perc_deprec, @i_entidades, @i_incorporacoes, @vida_util_novo, 
                                           @vida_util_ant, @perc_deprec_ant, @valor_depreciavel);";

            var parametros = new
            {
                id_cloud = "",
                id_cloud_reavaliacao = "",
                reavaliacao.i_reav_bem,
                reavaliacao.i_bem,
                reavaliacao.data_reav_bem,
                reavaliacao.vlr_reav_bem,
                reavaliacao.vlr_reav_resid,
                reavaliacao.valor_resid_ant,
                reavaliacao.valor_deprec_ant,
                reavaliacao.vlr_atual_ant,
                reavaliacao.motivo_reav_bem,
                reavaliacao.i_comissoes,
                reavaliacao.matricula_pessoal,
                reavaliacao.nro_portaria,
                reavaliacao.dt_portaria,
                reavaliacao.tipo_reav,
                reavaliacao.novo_perc_deprec,
                reavaliacao.i_entidades,
                reavaliacao.i_incorporacoes,
                reavaliacao.vida_util_novo,
                reavaliacao.vida_util_ant,
                reavaliacao.perc_deprec_ant,
                reavaliacao.valor_depreciavel
            };

            try
            {
                var count = await _pgConnect.ExecuteScalarAsync<int>(checkExistsQuery, new { reavaliacao.i_reav_bem });
                if (count == 0)
                {
                    Console.WriteLine($"🟢 Reavaliação de bens {reavaliacao.i_reav_bem} não encontrada, inserindo...");
                    await _pgConnect.ExecuteInsertAsync(insertQuery, parametros);
                }
                else
                {
                    Console.WriteLine($"⚠️ Reavaliação de bens {reavaliacao.i_reav_bem} já existe! Pulando...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao inserir a reavaliação de bens: {ex.Message}");
            }
        }
    }


}
