using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Alpha.Utils;

public static class StringHelper
{
    /// <summary>
    /// Limpa uma string removendo espaços extras, acentos e caracteres especiais.
    /// </summary>
    public static string LimparString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();
        input = RemoverAcentos(input);
        input = Regex.Replace(input, @"[^a-zA-Z0-9\s@._-]", string.Empty); // Mantém letras, números, espaço, @, ., _, -
        input = Regex.Replace(input, @"\s+", " "); // Substitui múltiplos espaços por um só

        return input;
    }

    /// <summary>
    /// Limpa e padroniza e-mails, removendo acentos e convertendo para minúsculas.
    /// </summary>
    public static string LimparEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return LimparString(email).ToLowerInvariant();
    }

    /// <summary>
    /// Remove todos os caracteres não numéricos de um telefone.
    /// </summary>
    public static string LimparTelefone(string telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone))
            return null;

        return new string(telefone.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Remove acentos e marcas diacríticas de uma string.
    /// </summary>
    public static string RemoverAcentos(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return texto;

        var normalized = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(capacity: texto.Length);

        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}