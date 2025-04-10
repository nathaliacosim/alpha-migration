using System.Collections.Generic;

namespace Alpha.Models.BethaCloud;

public class GrupoBemPOST
{
    public string descricao { get; set; }
    public TipoBemGrupoBemPOST tipoBem { get; set; }
    public MetodoDepreciacaoGrupoBemPOST metodoDepreciacao { get; set; }
    public decimal? percentualDepreciacaoAnual { get; set; }
    public decimal? percentualValorResidual { get; set; }
}

public class MetodoDepreciacaoGrupoBemPOST
{
    public int id { get; set; }
}

public class TipoBemGrupoBemPOST
{
    public int id { get; set; }
}

public class GrupoBemGET
{
    public int offset { get; set; }
    public int limit { get; set; }
    public bool hasNext { get; set; }
    public List<ContentGrupoBemGET> content { get; set; }
    public int total { get; set; }
    public object valor { get; set; }
    public object soma { get; set; }
    public object dados { get; set; }
}

public class ContentGrupoBemGET
{
    public int id { get; set; }
    public List<LinkGrupoBemGET> links { get; set; }
    public string descricao { get; set; }
    public TipoBemGrupoBemGET tipoBem { get; set; }
    public MetodoDepreciacaoGrupoBemGET metodoDepreciacao { get; set; }
    public decimal? percentualDepreciacaoAnual { get; set; }
    public decimal? percentualValorResidual { get; set; }
    public int? vidaUtil { get; set; }
    public object camposAdicionais { get; set; }
}

public class LinkGrupoBemGET
{
    public string rel { get; set; }
    public string href { get; set; }
}

public class MetodoDepreciacaoGrupoBemGET
{
    public int id { get; set; }
}

public class TipoBemGrupoBemGET
{
    public int id { get; set; }
}