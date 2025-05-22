using Alpha.Controllers;
using Alpha.Data;
using Alpha.Utils;
using System.Threading.Tasks;

namespace Alpha.UseCase;

public class ProcesssaDados
{
    private readonly PgConnect _pgConnect;
    private readonly OdbcConnect _odbcConnect;
    private readonly string _token;
    private readonly string _urlBase;

    public ProcesssaDados(PgConnect pgConnect, OdbcConnect odbcConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _odbcConnect = odbcConnect;
        _token = token;
        _urlBase = urlBase;
    }

    public async Task Executar()
    {
        //await TratarBaixaBens();
        await TratarDepreciacoesBens();
    }

    public async Task TratarTiposBaixas()
    {
        TipoBaixaController tipoBaixaController = new TipoBaixaController(_pgConnect, _token, _urlBase, _odbcConnect);
        await tipoBaixaController.EnviarTiposBaixasBethaParaCloud();
    }

    public async Task TratarOrganogramas()
    {
        OrganogramaController organogramaController = new OrganogramaController(_pgConnect, _token, _urlBase);
        await organogramaController.BuscarOrganogramasCloud();
    }

    public async Task TratarMetodoDepreciacao()
    {
        MetodoDepreciacaoController metodoDepreciacaoController = new MetodoDepreciacaoController(_pgConnect, _token, _urlBase);
        await metodoDepreciacaoController.EnviarMetodoDepreciacaoPadrao();
    }

    public async Task TratarLocalizacoesFisicas()
    {
        LocalizacaoFisicaController localizacaoFisicaController = new LocalizacaoFisicaController(_pgConnect, _token, _urlBase);
        await localizacaoFisicaController.EnviarLocalizacoesParaCloud();
    }

    public async Task TratarTiposBens()
    {
        TipoBensController tipoBensController = new TipoBensController(_pgConnect, _token, _urlBase);
        await tipoBensController.EnviarTiposBensParaCloud();
    }

    public async Task TratarGrupoBens()
    {
        GrupoBensController grupoBensController = new GrupoBensController(_pgConnect, _token, _urlBase);
        await grupoBensController.BuscarGrupoBensCloud();
    }

    public async Task TratarEspecieBens()
    {
        EspecieBensController especieBensController = new EspecieBensController(_pgConnect, _token, _urlBase);
        await especieBensController.BuscarEspeciesBensCloud();
    }

    public async Task TratarEstadoConservacao()
    {
        EstadoConservacaoController estadoConservacaoController = new EstadoConservacaoController(_pgConnect, _token, _urlBase);
        await estadoConservacaoController.EnviarEstadosConservacaoParaCloud();
    }

    public async Task TratarTipoUtilizacao()
    {
        TipoUtilizacaoController tipoUtilizacaoController = new TipoUtilizacaoController(_pgConnect, _token, _urlBase);
        await tipoUtilizacaoController.EnviarTiposUtilizacaoParaCloud();
    }

    public async Task TratarTipoAquisicao()
    {
        TipoAquisicaoController tipoAquisicaoController = new TipoAquisicaoController(_pgConnect, _token, _urlBase);
        await tipoAquisicaoController.EnviarTiposAquisicaoParaCloud();
    }

    public async Task TratarFornecedores()
    {
        FornecedorController fc = new FornecedorController(_pgConnect, _token, _urlBase);
        //await fc.AtualizarFornecedoresSemCnpjCpf();
        //await fc.EnviarFornecedoresParaCloud();
        await fc.BuscarFornecedoresCloud();
    }

    public async Task TratarBens()
    {
        var sqlHelper = new SqlHelper(_pgConnect);
        BensController bemController = new BensController(_pgConnect, _token, _urlBase, sqlHelper);
        //await bemController.EnviarBensBethaParaCloud();
        await bemController.TombarBensBetha();
    }

    public async Task TratarReavaliacoes()
    {
        ReavaliacaoController reavaliacaoController = new ReavaliacaoController(_pgConnect, _token, _urlBase, _odbcConnect);
        await reavaliacaoController.EnviarReavaliacoesBethaParaCloud();
    }

    public async Task TratarReavaliacoesBens()
    {
        ReavaliacaoBensController reavaliacaoBensController = new ReavaliacaoBensController(_pgConnect, _token, _urlBase, _odbcConnect);
        await reavaliacaoBensController.InserirReavaliacaoBens();
    }

    public async Task TratarDepreciacoes()
    {
        var sqlHelper = new SqlHelper(_pgConnect);
        DepreciacaoController depreciacaoController = new DepreciacaoController(_pgConnect, _token, _urlBase, sqlHelper, _odbcConnect);
        //await depreciacaoController.InserirDepreciacoes();
        await depreciacaoController.EnviarDepreciacoesBethaParaCloud();
    }

    public async Task TratarDepreciacoesBens()
    {
        var sqlHelper = new SqlHelper(_pgConnect);
        DepreciacaoBensController dbens = new DepreciacaoBensController(_pgConnect, _token, _urlBase, sqlHelper, _odbcConnect);
        await dbens.EnviarDepreciacaoBensBethaParaCloud();
    }

    public async Task TratarBaixas()
    {
        var sqlHelper = new SqlHelper(_pgConnect);
        BaixaController baixaController = new BaixaController(_pgConnect, _token, _urlBase, sqlHelper, _odbcConnect);
        await baixaController.EnviarBaixasBethaParaCloud();
    }

    public async Task TratarBaixaBens()
    {
        var sqlHelper = new SqlHelper(_pgConnect);
        BaixaBensController baixaBensController = new BaixaBensController(_pgConnect, _token, _urlBase, sqlHelper, _odbcConnect);
        //await baixaBensController.EnviarBaixaBensBethaParaCloud();
        await baixaBensController.FinalizarBaixasBetha();
    }

    public async Task TratarMovimentos()
    {
        MovimentosController movimentosController = new MovimentosController(_pgConnect, _token, _urlBase, _odbcConnect);
        await movimentosController.ProcessarMovimentos();
    }
}