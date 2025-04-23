using System;

namespace Alpha.Models.Alpha;

public class Depreciacao
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

public class DepreciacaoMes
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