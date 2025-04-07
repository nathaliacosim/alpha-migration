using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Models.BethaCloud;

public class TipoBemPOST
{
    public string descricao { get; set; }
    public ClassificacaoTipoBemPOST classificacao { get; set; }
}

public class ClassificacaoTipoBemPOST
{
    public string valor { get; set; }
    public string descricao { get; set; }
}