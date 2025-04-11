using System.Collections.Generic;

namespace Alpha.Models.BethaCloud;

public class BemValorBensPOST
{
    public MetodoDepreciacaoBensPOST metodoDepreciacao { get; set; }
    public MoedaBensPOST moeda { get; set; }
    public decimal? vlAquisicao { get; set; }
    public decimal? vlAquisicaoConvertido { get; set; }
    public decimal? vlResidual { get; set; }
    public decimal? vlDepreciavel { get; set; }
    public decimal? vlDepreciado { get; set; }
    public decimal? saldoDepreciar { get; set; }
    public int? capacidadeProdutiva { get; set; }
    public decimal? vlUltimaReavaliacao { get; set; }
    public decimal? vlLiquidoContabil { get; set; }
    public int? anosVidaUtil { get; set; }
    public decimal? taxaDepreciacaoAnual { get; set; }
    public string dtInicioDepreciacao { get; set; }
    public string dtUltimaReavaliacao { get; set; }
}

public class EspecieBemBensPOST
{
    public int? id { get; set; }
}

public class EstadoConservacaoBensPOST
{
    public int? id { get; set; }
}

public class FornecedorBensPOST
{
    public int? id { get; set; }
}

public class GrupoBemBensPOST
{
    public int? id { get; set; }
}

public class MetodoDepreciacaoBensPOST
{
    public int? id { get; set; }
}

public class MoedaBensPOST
{
    public int? id { get; set; }
    public string key { get; set; }
    public string nome { get; set; }
    public string sigla { get; set; }
    public string dtCotacao { get; set; }
    public int? fatorConversao { get; set; }
    public string formaCalculo { get; set; }
}

public class NumeroAnoEmpenhoBensPOST
{
    public string descricao { get; set; }
}

public class NumeroAnoProcessoBensPOST
{
    public string descricao { get; set; }
}

public class NumeroAnoSolicitacaoBensPOST
{
    public string descricao { get; set; }
}

public class OrganogramaBensPOST
{
    public int? id { get; set; }
}

public class ResponsavelBensPOST
{
    public int? id { get; set; }
}

public class LocalizacaoFisicaBensPOST
{
    public int? id { get; set; }
}

public class BensPOST
{
    public TipoBemBensPOST tipoBem { get; set; }
    public GrupoBemBensPOST grupoBem { get; set; }
    public EspecieBemBensPOST especieBem { get; set; }
    public LocalizacaoFisicaBensPOST localizacaoFisica { get; set; }
    public TipoUtilizacaoBemBensPOST tipoUtilizacaoBem { get; set; }
    public TipoAquisicaoBensPOST tipoAquisicao { get; set; }
    public FornecedorBensPOST fornecedor { get; set; }
    public ResponsavelBensPOST responsavel { get; set; }
    public EstadoConservacaoBensPOST estadoConservacao { get; set; }
    public TipoComprovanteBensPOST tipoComprovante { get; set; }
    public OrganogramaBensPOST organograma { get; set; }
    public string numeroComprovante { get; set; }
    public string descricao { get; set; }
    public string dataInclusao { get; set; }
    public string dataAquisicao { get; set; }
    public bool consomeCombustivel { get; set; }
    public SituacaoBemBensPOST situacaoBem { get; set; }
    public string numeroPlaca { get; set; }
    public string numeroRegistro { get; set; }
    public NumeroAnoProcessoBensPOST numeroAnoProcesso { get; set; }
    public NumeroAnoSolicitacaoBensPOST numeroAnoSolicitacao { get; set; }
    public List<NumeroAnoEmpenhoBensPOST> numeroAnoEmpenho { get; set; }
    public BemValorBensPOST bemValor { get; set; }
}

public class SituacaoBemBensPOST
{
    public string valor { get; set; }
    public string descricao { get; set; }
}

public class TipoAquisicaoBensPOST
{
    public int? id { get; set; }
}

public class TipoBemBensPOST
{
    public int? id { get; set; }
}

public class TipoComprovanteBensPOST
{
    public int? id { get; set; }
}

public class TipoUtilizacaoBemBensPOST
{
    public int? id { get; set; }
}

public class UnidadeMedidaBensPOST
{
    public int? id { get; set; }
}