using System.Collections.Generic;

namespace Alpha.Models.BethaCloud;

public class OrganogramaGET
{
    public int offset { get; set; }
    public int limit { get; set; }
    public bool hasNext { get; set; }
    public List<ContentOrganogramaGET> content { get; set; }
    public int total { get; set; }
    public object valor { get; set; }
    public object soma { get; set; }
    public object dados { get; set; }
}

public class ConfiguracaoOrganogramaOrganogramaGET
{
    public int id { get; set; }
    public List<LinkOrganogramaGET> links { get; set; }
}

public class ContentOrganogramaGET
{
    public int id { get; set; }
    public List<LinkOrganogramaGET> links { get; set; }
    public ConfiguracaoOrganogramaOrganogramaGET configuracaoOrganograma { get; set; }
    public string numeroOrganograma { get; set; }
    public string descricao { get; set; }
    public int nivel { get; set; }
}

public class LinkOrganogramaGET
{
    public string rel { get; set; }
    public string href { get; set; }
}