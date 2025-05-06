namespace Alpha.Models.BethaCloud;

public class BaixaPOST
{
    public TipoBaixa tipoBaixa { get; set; }
    public string dhBaixa { get; set; }
    public string motivo { get; set; }
}

public class TipoBaixa
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

public class BaixaBensPOST
{
    public BaixaIdPOST baixa { get; set; }
    public BemBaixaBensPOST bem { get; set; }
    public string notaExplicativa { get; set; }
}