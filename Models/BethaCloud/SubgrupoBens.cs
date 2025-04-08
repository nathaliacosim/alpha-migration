using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Models.BethaCloud;

public class EspecieBemPOST
{
    public GrupoBemEspecieBemPOST grupoBem { get; set; }
    public string descricao { get; set; }
}

public class GrupoBemEspecieBemPOST
{
    public int id { get; set; }
}

public class EspecieBemGET
{
    public int offset { get; set; }
    public int limit { get; set; }
    public bool hasNext { get; set; }
    public List<ContentEspecieBemGET> content { get; set; }
    public int total { get; set; }
    public object valor { get; set; }
    public object soma { get; set; }
    public object dados { get; set; }
}

public class ContentEspecieBemGET
{
    public int id { get; set; }
    public List<LinkEspecieBemGET> links { get; set; }
    public GrupoBemEspecieBemGET grupoBem { get; set; }
    public string descricao { get; set; }
}

public class GrupoBemEspecieBemGET
{
    public int id { get; set; }
}

public class LinkEspecieBemGET
{
    public string rel { get; set; }
    public string href { get; set; }
}
