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
        await TratarTipoUtilizacao();
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
        await grupoBensController.EnviarGruposBensParaCloud();
    }

    public async Task TratarSubgrupoBens()
    {
        SubgrupoBensController subgrupoBensController = new SubgrupoBensController(_pgConnect, _token, _urlBase);
        await subgrupoBensController.EnviarSubgruposBensParaCloud();
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

    public async Task TratarFornecedores()
    {
        FornecedorController fc = new FornecedorController(_pgConnect, _token, _urlBase);
        await fc.AtualizarFornecedoresSemCnpjCpf();
        //await fc.EnviarFornecedoresParaCloud();
        //await fc.BuscarFornecedoresCloud();
    }
}