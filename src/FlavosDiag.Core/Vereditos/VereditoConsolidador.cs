using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Core.Vereditos;

public sealed record ResultadoAgregacao(Veredito Geral, string Resumo, IReadOnlyList<string> CategoriasEmFalha);

/// <summary>Agregação do veredito geral — implementação literal de docs/REGRAS-VEREDITO.md §2.</summary>
public static class VereditoConsolidador
{
    public static ResultadoAgregacao Agregar(IReadOnlyList<ResultadoCategoria>? categorias)
    {
        var aplicaveis = (categorias ?? Array.Empty<ResultadoCategoria>())
            .Where(c => c.Veredito != Veredito.NaoSeAplica)
            .ToList();

        if (aplicaveis.Count == 0)
        {
            return new ResultadoAgregacao(
                Veredito.Inconclusivo,
                "Nenhuma categoria pôde ser avaliada.",
                Array.Empty<string>());
        }

        var falhas = aplicaveis.Where(c => c.Veredito == Veredito.Falha).Select(c => c.Categoria).ToList();
        if (falhas.Count > 0)
        {
            return new ResultadoAgregacao(
                Veredito.Falha,
                "Falha em: " + string.Join(", ", falhas) + ".",
                falhas);
        }

        var inconclusivos = aplicaveis.Where(c => c.Veredito == Veredito.Inconclusivo).Select(c => c.Categoria).ToList();
        if (inconclusivos.Count > 0)
        {
            return new ResultadoAgregacao(
                Veredito.Inconclusivo,
                "Sem evidência suficiente em: " + string.Join(", ", inconclusivos) + ".",
                Array.Empty<string>());
        }

        var atencao = aplicaveis.Where(c => c.Veredito == Veredito.Atencao).Select(c => c.Categoria).ToList();
        if (atencao.Count > 0)
        {
            return new ResultadoAgregacao(
                Veredito.Atencao,
                "Atenção em: " + string.Join(", ", atencao) + ".",
                Array.Empty<string>());
        }

        return new ResultadoAgregacao(
            Veredito.Ok,
            "Todas as categorias avaliadas estão dentro do esperado.",
            Array.Empty<string>());
    }
}
