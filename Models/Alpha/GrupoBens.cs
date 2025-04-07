using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpha.Models.Alpha;

public class GrupoBens
{
    public string id_cloud { get; set; }
    public int codigo { get; set; }
    public string descricao { get; set; }
    public int? tp_bens_cod { get; set; }
    public decimal? deprecia { get; set; }
    public DateTime? data_cadastro { get; set; }
    public TimeSpan? hora_cadastro { get; set; }
    public string user_cadastro { get; set; }
    public DateTime? data_atualiza { get; set; }
    public TimeSpan? hora_atualiza { get; set; }
    public string user_atualiza { get; set; }
}
