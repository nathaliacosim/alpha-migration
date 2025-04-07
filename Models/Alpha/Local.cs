using System;

namespace Alpha.Models.Alpha;

public class Local
{
    public string id_cloud { get; set; }
    public int codigo { get; set; }
    public string descricao { get; set; }
    public int? setor_cod { get; set; }
    public DateTime? data_cadastro { get; set; }
    public TimeSpan? hora_cadastro { get; set; }
    public string user_cadastro { get; set; }
    public DateTime? data_atualiza { get; set; }
    public TimeSpan? hora_atualiza { get; set; }
    public string user_atualiza { get; set; }
    public int? responsavel_cod { get; set; }
}