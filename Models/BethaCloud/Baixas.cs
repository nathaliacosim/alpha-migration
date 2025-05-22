namespace Alpha.Models.BethaCloud;

public class BaixaPOST
{
    public TipoBaixaPOST tipoBaixa { get; set; }
    public string dhBaixa { get; set; }
    public string motivo { get; set; }
}

public class TipoBaixaPOST
{
    public int id { get; set; }
}

public class BaixaIdPOST
{
    public int id { get; set; }
}

public class BemBaixaBensPOST
{
    public int id { get; set; }
}

public class BaixaFornecedorPost
{
    public int id { get; set; }
}

public class BaixaBensPOST
{
    public BaixaFornecedorPost fornecedor { get; set; }
    public BaixaIdPOST baixa { get; set; }
    public BemBaixaBensPOST bem { get; set; }
    public string notaExplicativa { get; set; }
}