using System.Collections.Generic;

namespace Alpha.Models.BethaCloud;

public class FornecedorGET
{
    public int offset { get; set; }
    public int limit { get; set; }
    public bool hasNext { get; set; }
    public List<ContentFornecedorGET> content { get; set; }
    public int total { get; set; }
    public object valor { get; set; }
    public object soma { get; set; }
    public object dados { get; set; }
}

public class ContentFornecedorGET
{
    public int id { get; set; }
    public List<LinkFornecedorGET> links { get; set; }
    public string nome { get; set; }
    public string cpfCnpj { get; set; }
    public TipoFornecedorGET tipo { get; set; }
    public SituacaoFornecedorGET situacao { get; set; }
    public string dataInclusao { get; set; }
    public PorteEmpresaFornecedorGET porteEmpresa { get; set; }
    public bool optanteSimples { get; set; }
    public List<object> contasBancarias { get; set; }
    public List<EmailFornecedorGET> emails { get; set; }
    public List<TelefoneFornecedorGET> telefones { get; set; }
}

public class EmailFornecedorGET
{
    public int id { get; set; }
    public string endereco { get; set; }
    public int ordem { get; set; }
    public object descricao { get; set; }
}

public class LinkFornecedorGET
{
    public string rel { get; set; }
    public string href { get; set; }
}

public class PorteEmpresaFornecedorGET
{
    public string valor { get; set; }
    public string descricao { get; set; }
}

public class SituacaoFornecedorGET
{
    public string valor { get; set; }
    public string descricao { get; set; }
}

public class TelefoneFornecedorGET
{
    public int id { get; set; }
    public string numero { get; set; }
    public object observacao { get; set; }
    public string tipo { get; set; }
    public int ordem { get; set; }
}

public class TipoFornecedorGET
{
    public string valor { get; set; }
    public string descricao { get; set; }
}

public class FornecedorPOST
{
    public string nome { get; set; }
    public string cpfCnpj { get; set; }
    public TipoFornecedorPOST tipo { get; set; }
    public SituacaoFornecedorPOST situacao { get; set; }
    public string dataInclusao { get; set; }
    public EnderecoFornecedorPOST endereco { get; set; }
    public List<EmailFornecedorPOST> emails { get; set; }
    public List<TelefoneFornecedorPOST> telefones { get; set; }
}

public class BairroFornecedorPOST
{
    public int id { get; set; }
    public string descricao { get; set; }
}

public class EmailFornecedorPOST
{
    public int id { get; set; }
    public string endereco { get; set; }
    public string descricao { get; set; }
    public int ordem { get; set; }
}

public class EnderecoFornecedorPOST
{
    public string descricao { get; set; }
    public MunicipioFornecedorPOST municipio { get; set; }
    public BairroFornecedorPOST bairro { get; set; }
    public LogradouroFornecedorPOST logradouro { get; set; }
    public string numero { get; set; }
    public string cep { get; set; }
}

public class LogradouroFornecedorPOST
{
    public int id { get; set; }
}

public class MunicipioFornecedorPOST
{
    public int id { get; set; }
}

public class SituacaoFornecedorPOST
{
    public string valor { get; set; }
    public string descricao { get; set; }
}

public class TelefoneFornecedorPOST
{
    public int id { get; set; }
    public string numero { get; set; }
    public string observacao { get; set; }
    public string tipo { get; set; }
    public int ordem { get; set; }
}

public class TipoFornecedorPOST
{
    public string valor { get; set; }
    public string descricao { get; set; }
}