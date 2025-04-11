using System;

namespace Alpha.Models.Alpha;

public class Bens
{
    public int codigo { get; set; }
    public string descricao { get; set; }
    public string tp_recurso_cod { get; set; }
    public int? tp_origem_cod { get; set; }
    public int? lic_metodo_cod { get; set; }
    public string tp_avaliacao_numero { get; set; }
    public int? tp_classificacao_cod { get; set; }
    public int? tp_empenho_cod { get; set; }
    public string tp_lei { get; set; }
    public int? tp_lei_num { get; set; }
    public DateTime? tp_lei_data { get; set; }
    public string tipo { get; set; }
    public int? local_cod { get; set; }
    public string unidade_cod { get; set; }
    public int? plaqueta { get; set; }
    public string processo_num { get; set; }
    public int? processo_ano { get; set; }
    public int? fornecedor_cod { get; set; }
    public int? notafiscal { get; set; }
    public string nfiscalserie { get; set; }
    public DateTime? nfdata { get; set; }
    public decimal? nfvalor { get; set; }
    public DateTime? tombamento_data { get; set; }
    public DateTime? alienacao_data { get; set; }
    public DateTime? garantia_data { get; set; }
    public decimal? avaliacao_atual_valor { get; set; }
    public DateTime? avaliacao_atual_data { get; set; }
    public int? empenho_num { get; set; }
    public int? empenho_ano { get; set; }
    public int? lei_numero { get; set; }
    public int? seguro_fornecedor_cod { get; set; }
    public string seguro_apolice { get; set; }
    public DateTime? seguro_apolice_data { get; set; }
    public DateTime? seguro_apolice_venc { get; set; }
    public int? atual_status_cod { get; set; }
    public int? aquisicao_status_cod { get; set; }
    public string user_cadastro { get; set; }
    public DateTime? data_cadastro { get; set; }
    public TimeSpan? hora_cadastro { get; set; }
    public DateTime? data_atualiza { get; set; }
    public TimeSpan? hora_atualiza { get; set; }
    public string user_atualiza { get; set; }
    public int? subgrupo_bens_cod { get; set; }
    public int? subgrupo_pro_cod { get; set; }
    public string produto_cod { get; set; }
    public string tp_avaliacao { get; set; }
    public int? baixado { get; set; }
    public DateTime? data_baixa { get; set; }
    public int? comodato { get; set; }
    public string obs_aq2 { get; set; }
    public string obs_aquisicao { get; set; }
    public int? atual_status_cod_avalia { get; set; }
    public decimal? seguro_valor { get; set; }
    public string raca { get; set; }
    public string cor { get; set; }
    public DateTime? data_nasc { get; set; }
    public decimal? vlr_residual { get; set; }
    public string id_cloud { get; set; }
}