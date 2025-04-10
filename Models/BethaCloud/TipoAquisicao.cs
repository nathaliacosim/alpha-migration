using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Models.BethaCloud;

public class TipoAquisicaoPOST
{
    public string descricao { get; set; }
    public ClassificacaoTipoAquisicaoPOST classificacao { get; set; }
}

public class ClassificacaoTipoAquisicaoPOST
{
    public string valor { get; set; }
    public string descricao { get; set; }
}
