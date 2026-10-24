using System.Security.Cryptography;
using System.Text;

namespace FlavosDiag.Core;

/// <summary>
/// Mascaramento de identificadores (número de série, MAC). Padrão do laudo: mascarado.
/// Regra (decisão própria D-mascaramento): preserva os 4 primeiros e os 4 últimos quando há
/// 12 ou mais caracteres visíveis; caso contrário, exibe apenas asteriscos (nunca vaza o tamanho real).
/// </summary>
public static class Mascaramento
{
    public const string Mascara = "****";

    public static string Mascarar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var limpo = valor.Trim();
        if (limpo.Length >= 12)
        {
            return limpo[..4] + Mascara + limpo[^4..];
        }

        return Mascara;
    }

    /// <summary>Hash estável (SHA-256 hex truncado) para chave de histórico sem guardar o serial.</summary>
    public static string HashChave(params string?[] partes)
    {
        var texto = string.Join('|', partes.Select(p => (p ?? string.Empty).Trim().ToUpperInvariant()));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
