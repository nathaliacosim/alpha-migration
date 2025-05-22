using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Models.BethaCloud;

public class TipoBaixaRootPOST
{
    public string descricao { get; set; }
    public ClassificacaoTipoBaixaPOST classificacao { get; set; }
}

public class ClassificacaoTipoBaixaPOST
{
    public string valor { get; set; }
    public string descricao { get; set; }
}
