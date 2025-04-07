using System;
using System.Linq;

namespace Alpha.Utils;

public static class GeradorDocumentos
{
    public static string GerarCNPJ()
    {
        Random random = new Random();
        int[] cnpj = new int[14];

        // Gera os primeiros 12 dígitos aleatórios
        for (int i = 0; i < 12; i++)
        {
            cnpj[i] = random.Next(0, 10);
        }

        // Calcula os dígitos verificadores
        cnpj[12] = CalcularDigitoVerificador(cnpj, 12);
        cnpj[13] = CalcularDigitoVerificador(cnpj, 13);

        return string.Join("", cnpj.Select(n => n.ToString()));
    }

    public static string GerarCPF()
    {
        Random random = new Random();
        int[] cpf = new int[11];

        // Gera os primeiros 9 dígitos aleatórios
        for (int i = 0; i < 9; i++)
        {
            cpf[i] = random.Next(0, 10);
        }

        // Calcula os dígitos verificadores
        cpf[9] = CalcularDigitoVerificador(cpf, 9);
        cpf[10] = CalcularDigitoVerificador(cpf, 10);

        return string.Join("", cpf.Select(n => n.ToString()));
    }

    private static int CalcularDigitoVerificador(int[] documento, int tamanho)
    {
        int[] multiplicadores = tamanho == 9 ?
            new int[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 } :
            tamanho == 10 ? new int[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 } :
            tamanho == 12 ? new int[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 } :
            new int[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        int soma = 0;
        for (int i = 0; i < tamanho; i++)
        {
            soma += documento[i] * multiplicadores[i];
        }

        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}