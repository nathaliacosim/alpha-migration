using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Models.Alpha;


public class TipoBaixaBetha
{
    public int id { get; set; }
    public string id_cloud { get; set; }
    public int i_motivo { get; set; }
    public string descricao { get; set; }
    public string classificacao { get; set; }
    public int i_entidades { get; set; }
}

public class TipoBaixaBethaDba
{
    public int i_motivo { get; set; }
    public string descricao { get; set; }
    public int codigo_tce { get; set; }
    public int i_entidades { get; set; }
}