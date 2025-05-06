using System;

namespace Alpha.Models.Alpha;

public class DepreciacaoMercato
{
    public int id { get; set; }
    public string id_cloud { get; set; }
    public int mes { get; set; }
    public int ano { get; set; }
    public string mes_ano { get; set; }
    public string descricao { get; set; }
    public decimal valor_total { get; set; }
    public int qtd_bens { get; set; }
    public bool finalizado { get; set; }
}

public class DepreciacaoMesMercato
{
    public int mes { get; set; }
    public int ano { get; set; }
    public decimal total { get; set; }
    public int qtd_bens { get; set; }
}

public class DepreciacaoBens
{
    public int codigo { get; set; }
    public int bens_cod { get; set; }
    public int mes { get; set; }
    public int ano { get; set; }
    public decimal depreciacao { get; set; }
    public decimal depreciacao_acum { get; set; }
    public decimal vlr_liquido_contabil { get; set; }
    public int meses_depreciados { get; set; }
    public int meses_restantes { get; set; }
    public DateTime data_depreciacao { get; set; }
    public string? id_cloud_depreciacao { get; set; }
    public string? id_cloud_bem { get; set; }
    public string? id_cloud { get; set; }
}

#region Models Betha

public class DepreciacaoCabecalhoBethaDba
{
    public string ano { get; set; }
    public string mes { get; set; }
    public string mes_ano { get; set; }
}

public class DepreciacoesBethaDba
{
    public int i_depreciacao { get; set; }
    public int i_bem { get; set; }
    public string data_depr { get; set; }
    public decimal saldo_ant { get; set; }
    public decimal percentual { get; set; }
    public string nro_portaria { get; set; }
    public string dt_portaria { get; set; }
    public string matricula_pessoal { get; set; }
    public string dt_autorizacao { get; set; }
    public decimal valor_calc { get; set; }
    public int i_reav_bem { get; set; }
    public int i_entidades { get; set; }
}

public class DepreciacaoCabecalho
{
    public int id { get; set; }
    public string id_cloud { get; set; }
    public string mes { get; set; }
    public string ano { get; set; }
    public string mes_ano { get; set; }
    public string observacao { get; set; }
}

public class DepreciacaoBensBethaDba
{
    public int i_depreciacao { get; set; }
    public int i_bem { get; set; }
    public string data_depr { get; set; }
    public decimal valor_calc { get; set; }
    public int i_entidades { get; set; }
}

#endregion Models Betha