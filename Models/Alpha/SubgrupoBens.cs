﻿using System;

namespace Alpha.Models.Alpha;

public class SubgrupoBens
{
    public string id_cloud { get; set; }
    public int codigo { get; set; }
    public string descricao { get; set; }
    public int? grupo_bens_cod { get; set; }
    public DateTime? data_cadastro { get; set; }
    public TimeSpan? hora_cadastro { get; set; }
    public string user_cadastro { get; set; }
    public DateTime? data_atualiza { get; set; }
    public TimeSpan? hora_atualiza { get; set; }
    public string user_atualiza { get; set; }
    public decimal? deprecia { get; set; }
    public string planoconta_cod { get; set; }
    public string planocontacontra_cod { get; set; }
    public decimal? perc_residual { get; set; }
    public int? vida_util_meses { get; set; }
}