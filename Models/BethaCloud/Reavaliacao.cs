namespace Alpha.Models.BethaCloud;

public class Comissao
{
    public int id { get; set; }
}

public class Responsavel
{
    public int id { get; set; }
}

public class ReavaliacaoPost
{
    public int id { get; set; }
    public TipoReavaliacaoValorizacao tipoReavaliacaoValorizacao { get; set; }
    public TipoReavaliacaoDesvalorizacao tipoReavaliacaoDesvalorizacao { get; set; }
    public Comissao comissao { get; set; }
    public Responsavel responsavel { get; set; }
    public string dhReavaliacao { get; set; }
    public string criterioFundamentacao { get; set; }
    public string observacao { get; set; }
}

public class TipoReavaliacaoDesvalorizacao
{
    public int id { get; set; }
}

public class TipoReavaliacaoValorizacao
{
    public int id { get; set; }
}

public class BemReavaliacaoBemPOST
{
    public int id { get; set; }
}

public class ReavaliacaoReavaliacaoBemPOST
{
    public int id { get; set; }
}

public class ReavaliacaoBemPOST
{
    public ReavaliacaoReavaliacaoBemPOST reavaliacao { get; set; }
    public BemReavaliacaoBemPOST bem { get; set; }
    public object metodoDepreciacao { get; set; }
    public object notaExplicativa { get; set; }
    public decimal vlBem { get; set; }
}