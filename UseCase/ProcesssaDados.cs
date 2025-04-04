using Alpha.Controllers;
using Alpha.Data;
using System.Threading.Tasks;

namespace Alpha.UseCase;

public class ProcesssaDados
{
    private readonly PgConnect _pgConnect;
    private readonly string _token;

    public ProcesssaDados(PgConnect pgConnect, string token)
    {
        _pgConnect = pgConnect;
        _token = token;
    }

    public async Task Executar()
    {
        await TratarMetodoDepreciacao();
        //await TratarFornecedores();
        //await TratarCentroCustos();
    }

    public async Task TratarMetodoDepreciacao()
    {
        MetodoDepreciacaoController metodoDepreciacaoController = new MetodoDepreciacaoController(_pgConnect, _token);
        await metodoDepreciacaoController.EnviarMetodoDepreciacaoPadrao();
    }

    public async Task TratarCentroCustos()
    {
        //CentroCustoController cc = new CentroCustoController(_pgConnection, _odbcConnection, _token);

        //await cc.InserirCentroCustosBethaDesktopNoPostgres();
    }

    public async Task TratarFornecedores()
    {
        //FornecedorController fc = new FornecedorController(_pgConnection, _odbcConnection, _token);

        //await fc.InserirFornecedoresBethaDesktopNoPostgres();
        //await fc.AtualizarFornecedoresSemCnpjCpf();
        //await fc.EnviarFornecedoresParaCloud();
        //await fc.BuscarFornecedoresCloud();
    }
}