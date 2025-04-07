using System.Collections.Generic;

namespace Alpha.Models.BethaCloud;

/* POST */

public class MetodoDepreciacaoPOST
{
    public string descricao { get; set; }
    public ClassificacaoMetodoDepreciacaoPOST classificacao { get; set; }
    public TipoDepreciacaoPOST tipoDepreciacao { get; set; }
    public bool ativo { get; set; }
}

public class ClassificacaoMetodoDepreciacaoPOST
{
    public string valor { get; set; }
}

public class TipoDepreciacaoPOST
{
    public string valor { get; set; }
}

/* GET */

public class MetodoDepreciacaoGET
{
    public int offset { get; set; }
    public int limit { get; set; }
    public bool hasNext { get; set; }
    public List<ContentMetodoDepreciacaoGET> content { get; set; }
    public int total { get; set; }
    public object valor { get; set; }
    public object soma { get; set; }
    public object dados { get; set; }
}

public class ContentMetodoDepreciacaoGET
{
    public int id { get; set; }
    public List<LinkMetodoDepreciacaoGET> links { get; set; }
    public string descricao { get; set; }
    public ClassificacaoMetodoDepreciacaoGET classificacao { get; set; }
    public TipoDepreciacaoMetodoDepreciacaoGET tipoDepreciacao { get; set; }
    public string idScript { get; set; }
    public bool ativo { get; set; }
}

public class ClassificacaoMetodoDepreciacaoGET
{
    public string valor { get; set; }
    public string descricao { get; set; }
}

public class LinkMetodoDepreciacaoGET
{
    public string rel { get; set; }
    public string href { get; set; }
}

public class TipoDepreciacaoMetodoDepreciacaoGET
{
    public string valor { get; set; }
    public string descricao { get; set; }
}