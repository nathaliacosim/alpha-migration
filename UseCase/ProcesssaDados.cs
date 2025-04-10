using Alpha.Controllers;
using Alpha.Data;
using System.Threading.Tasks;

namespace Alpha.UseCase;

public class ProcesssaDados
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;
    private readonly string _urlBase;

    public ProcesssaDados(PgConnect pgConnect, string token, string urlBase)
    {
        _pgConnect = pgConnect;
        _token = token;
        _urlBase = urlBase;
    }

    public async Task Executar()
    {
        await TratarEstadoConservacao();
    }

    public async Task TratarMetodoDepreciacao()
    {
        //MetodoDepreciacaoController metodoDepreciacaoController = new MetodoDepreciacaoController(_pgConnect, _token);
        //await metodoDepreciacaoController.EnviarMetodoDepreciacaoPadrao();
    }

    public async Task TratarLocalizacoesFisicas()
    {
        //LocalizacaoFisicaController localizacaoFisicaController = new LocalizacaoFisicaController(_pgConnect, _token);
        //await localizacaoFisicaController.EnviarLocalizacoesParaCloud();
    }

    public async Task TratarTiposBens()
    {
        //TipoBensController tipoBensController = new TipoBensController(_pgConnect, _token);
        //await tipoBensController.EnviarTiposBensParaCloud();
    }

    public async Task TratarGrupoBens()
    {
        //GrupoBensController grupoBensController = new GrupoBensController(_pgConnect, _token);
        //await grupoBensController.EnviarGruposBensParaCloud();
    }

    public async Task TratarSubgrupoBens()
    {
        //SubgrupoBensController subgrupoBensController = new SubgrupoBensController(_pgConnect, _token);
        //await subgrupoBensController.EnviarSubgruposBensParaCloud();
    }

    public async Task TratarEstadoConservacao()
    {
        EstadoConservacaoController estadoConservacaoController = new EstadoConservacaoController(_pgConnect, _token, _urlBase);
        await estadoConservacaoController.EnviarEstadosConservacaoParaCloud();
    }

    public async Task TratarCentroCustos()
    {
        //CentroCustoController cc = new CentroCustoController(_pgConnect, _token);
        //await cc.InserirCentroCustosBethaDesktopNoPostgres();
    }

    public async Task TratarFornecedores()
    {
        //FornecedorController fc = new FornecedorController(_pgConnect, _token);
        //await fc.InserirFornecedoresBethaDesktopNoPostgres();
        //await fc.AtualizarFornecedoresSemCnpjCpf();
        //await fc.EnviarFornecedoresParaCloud();
        //await fc.BuscarFornecedoresCloud();
    }
}