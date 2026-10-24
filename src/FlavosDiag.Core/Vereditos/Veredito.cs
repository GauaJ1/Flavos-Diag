namespace FlavosDiag.Core.Vereditos;

/// <summary>Veredito de categoria. Regras em docs/REGRAS-VEREDITO.md.</summary>
public enum Veredito
{
    Ok = 0,
    Atencao = 1,
    Inconclusivo = 2,
    Falha = 3,

    /// <summary>Ex.: desktop sem bateria. Ignorado na agregação.</summary>
    NaoSeAplica = -1,
}

public static class VereditoTexto
{
    public static string Rotulo(this Veredito v) => v switch
    {
        Veredito.Ok => "OK",
        Veredito.Atencao => "Atenção",
        Veredito.Inconclusivo => "Inconclusivo",
        Veredito.Falha => "Falha",
        Veredito.NaoSeAplica => "Não se aplica",
        _ => "Inconclusivo",
    };

    /// <summary>Prioridade de dominância: Falha &gt; Inconclusivo &gt; Atenção &gt; OK.</summary>
    public static int Prioridade(this Veredito v) => v switch
    {
        Veredito.Falha => 4,
        Veredito.Inconclusivo => 3,
        Veredito.Atencao => 2,
        Veredito.Ok => 1,
        _ => 0,
    };

    public static Veredito MaisGrave(Veredito a, Veredito b)
    {
        if (a == Veredito.NaoSeAplica)
        {
            return b;
        }

        if (b == Veredito.NaoSeAplica)
        {
            return a;
        }

        return a.Prioridade() >= b.Prioridade() ? a : b;
    }
}
