using System;

namespace Alpha.Models.Alpha;

public class BaixaBens
{
    public int codigo { get; set; }
    public int? ano { get; set; }
    public int bens_cod { get; set; }
    public string tp_pat_atualiza_bens { get; set; }
    public string tp_lei { get; set; }
    public int? tp_lei_num { get; set; }
    public DateTime? tp_lei_data { get; set; }
    public DateTime? atualiza_data { get; set; }
    public decimal? atualiza_valor { get; set; }
    public string atualiza_obs { get; set; }
    public DateTime? atualiza_data_ant { get; set; }
    public decimal? atualiza_valor_ant { get; set; }
    public int? atualiza_grupo_cod { get; set; }
    public int? atualiza_ref { get; set; }
    public DateTime? data_cadastro { get; set; }
    public TimeSpan? hora_cadastro { get; set; }
    public string user_cadastro { get; set; }
    public DateTime? data_atualiza { get; set; }
    public TimeSpan? hora_atualiza { get; set; }
    public string user_atualiza { get; set; }
    public decimal? atualiza_percentual { get; set; }
    public int? processo_nr { get; set; }
    public int? processo_ano { get; set; }
    public string ocorrencia { get; set; }
    public DateTime? dt_ocorrencida { get; set; }
    public int? avaliacao_estado { get; set; }
    public int? tp_baixa { get; set; }
    public int? licitacao_cod { get; set; }
    public int? licitacao_ano { get; set; }
    public int? comprador_cod { get; set; }
    public int? anulado { get; set; }
    public string id_cloud_tipo_baixa { get; set; }
    public string id_cloud { get; set; }
    public string id_cloud_baixa { get; set; }
    public string id_cloud_bem { get; set; }
}

public class BaixaGroupBy
{
    public string data_baixa { get; set; }
    public int tipo_baixa { get; set; }
}

public class Baixa
{
    public int id { get; set; }
    public string id_cloud { get; set; }
    public string data_baixa { get; set; }
    public int codigo_tp_baixa { get; set; }
    public string id_cloud_tp_baixa { get; set; }
    public string descricao_motivo { get; set; }
}