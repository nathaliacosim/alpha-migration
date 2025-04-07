using System;

namespace Alpha.Models.Alpha;

public class Fornecedor
{
    public string id_cloud { get; set; }
    public int? codigo { get; set; }
    public int? situacao { get; set; }
    public string tipo { get; set; }
    public string tipo_cad { get; set; }
    public string nome { get; set; }
    public string fantasia { get; set; }
    public string cpf { get; set; }
    public string rg { get; set; }
    public string im { get; set; }
    public string crc_oab { get; set; }
    public DateTime? datanasc { get; set; }
    public DateTime? encerramento { get; set; }
    public string encerramento_motivo { get; set; }
    public int? encerramento_user { get; set; }
    public string estadocivil { get; set; }
    public string cep { get; set; }
    public int? logradouro_cod { get; set; }
    public int? endereco_numero { get; set; }
    public string residencial { get; set; }
    public string comercial { get; set; }
    public string celular { get; set; }
    public int? profissao_cod { get; set; }
    public string email { get; set; }
    public int? setor_cod { get; set; }
    public byte[] foto { get; set; }
    public string historico { get; set; }
    public DateTime? data_cadastro { get; set; }
    public TimeSpan? hora_cadastro { get; set; }
    public string user_cadastro { get; set; }
    public DateTime? data_atualiza { get; set; }
    public TimeSpan? hora_atualiza { get; set; }
    public string user_atualiza { get; set; }
    public int? cancelado { get; set; }
    public DateTime? data_cancelado { get; set; }
    public string user_cancelado { get; set; }
    public int? tipo_empresa { get; set; }
    public string rg_orgao { get; set; }
    public string rg_uf { get; set; }
    public string im_anterior { get; set; }
    public string pasep { get; set; }
    public string reg_junta { get; set; }
    public DateTime? reg_junta_data { get; set; }
    public int? pro_junta { get; set; }
    public int? pro_junta_ano { get; set; }
    public int? pro_ult_alteracao { get; set; }
    public DateTime? pro_dtult_alteracao { get; set; }
    public int? prof_liberal { get; set; }
    public int? autonomo { get; set; }
    public string pai { get; set; }
    public string mae { get; set; }
    public int? estabelecido { get; set; }
    public int? imovel_cod { get; set; }
    public int? endereco_cod { get; set; }
    public string endereco_complemento { get; set; }
    public string fax { get; set; }
    public int? pagamento { get; set; }
    public int? qtd { get; set; }
    public int? issqn_tipo { get; set; }
    public DateTime? issqn_perini { get; set; }
    public DateTime? issqn_perfin { get; set; }
    public decimal? issqn_estimado { get; set; }
    public DateTime? issqn_abertura { get; set; }
    public DateTime? issqn_suspensao { get; set; }
    public string url { get; set; }
    public int? cidade_natal_cod { get; set; }
    public int? nacionalidade_cod { get; set; }
    public string registro_m19 { get; set; }
    public DateTime? data_chegada { get; set; }
    public decimal? capital_social { get; set; }
    public DateTime? validade_contrato { get; set; }
}