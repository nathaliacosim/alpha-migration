﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Models.BethaCloud;

public class TipoUtilizacaoPOST
{
    public string descricao { get; set; }
    public ClassificacaoTipoUtilizacaoPOST classificacao { get; set; }
}

public class ClassificacaoTipoUtilizacaoPOST
{
    public string valor { get; set; }
    public string descricao { get; set; }
}