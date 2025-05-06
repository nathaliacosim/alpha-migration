namespace Alpha.Models.Alpha;

public class ReavaliacaoBethaDba
{
    public int id { get; set; }
    public int i_reavaliacao { get; set; }
    public int i_bem { get; set; }
    public string data_reav { get; set; }
    public decimal? saldo_ant { get; set; }
    public decimal? percentual { get; set; }
    public string nro_portaria { get; set; }
    public string dt_portaria { get; set; }
    public string matricula_pessoal { get; set; }
    public decimal? valor_calc { get; set; }
    public string motivo_valorizacao { get; set; }
    public int? i_comissoes { get; set; }
    public int? i_reav_bem { get; set; }
    public int? i_entidades { get; set; }
}

public class ReavaliacaoBemBethaDba
{
    public int id { get; set; }
    public int i_reav_bem { get; set; }
    public int i_bem { get; set; }
    public string data_reav_bem { get; set; }
    public decimal vlr_reav_bem { get; set; }
    public decimal vlr_reav_resid { get; set; }
    public decimal valor_resid_ant { get; set; }
    public decimal valor_deprec_ant { get; set; }
    public decimal vlr_atual_ant { get; set; }
    public string motivo_reav_bem { get; set; }
    public int? i_comissoes { get; set; }
    public string matricula_pessoal { get; set; }
    public string nro_portaria { get; set; }
    public string dt_portaria { get; set; }
    public string tipo_reav { get; set; }
    public decimal novo_perc_deprec { get; set; }
    public int i_entidades { get; set; }
    public int? i_incorporacoes { get; set; }
    public int vida_util_novo { get; set; }
    public int vida_util_ant { get; set; }
    public decimal perc_deprec_ant { get; set; }
    public decimal valor_depreciavel { get; set; }
}