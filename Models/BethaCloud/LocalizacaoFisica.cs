using System.Collections.Generic;

namespace Alpha.Models.BethaCloud;

public class LocalizacaoFisicaGET
{
    public int offset { get; set; }
    public int limit { get; set; }
    public bool hasNext { get; set; }
    public List<ContentLocalizacaoFisicaGET> content { get; set; }
    public int total { get; set; }
    public object valor { get; set; }
    public object soma { get; set; }
    public object dados { get; set; }
}

public class ClassificacaoLocalizacaoFisicaGET
{
    public string valor { get; set; }
    public string descricao { get; set; }
}

public class ContentLocalizacaoFisicaGET
{
    public int id { get; set; }
    public List<LinkLocalizacaoFisicaGET> links { get; set; }
    public string descricao { get; set; }
    public int nivel { get; set; }
    public ClassificacaoLocalizacaoFisicaGET classificacao { get; set; }
    public object endereco { get; set; }
    public object observacao { get; set; }
    public object localizacaoFisicaPai { get; set; }
}

public class LinkLocalizacaoFisicaGET
{
    public string rel { get; set; }
    public string href { get; set; }
}

public class LocalizacaoFisicaPOST
{
    public string descricao { get; set; }
    public int nivel { get; set; }
    public ClassificacaoLocalizacaoFisicaPOST classificacao { get; set; }
    public EnderecoLocalizacaoFisicaPOST endereco { get; set; }
    public string observacao { get; set; }
    public LocalizacaoFisicaPaiLocalizacaoFisicaPOST localizacaoFisicaPai { get; set; }
}

public class ClassificacaoLocalizacaoFisicaPOST
{
    public string valor { get; set; }
    public string descricao { get; set; }
}

public class EnderecoLocalizacaoFisicaPOST
{
    public int id { get; set; }
}

public class LocalizacaoFisicaPaiLocalizacaoFisicaPOST
{
    public int id { get; set; }
}