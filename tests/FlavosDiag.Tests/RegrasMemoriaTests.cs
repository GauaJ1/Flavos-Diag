using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.Tests;

/// <summary>
/// Regras de veredito da memória. O ponto central: inventário não é teste. Nenhuma combinação sem teste
/// executado pode sair OK, e o teste rápido sem divergência também não prova integridade.
/// </summary>
public static class RegrasMemoriaTests
{
    private const long UmGiB = 1024L * 1024 * 1024;

    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos() => new (string, Func<Task>)[]
    {
        ("Regras memória: sem teste é Inconclusivo", SemTesteInconclusivo),
        ("Regras memória: teste NaoExecutado é Inconclusivo", TesteNaoExecutadoInconclusivo),
        ("Regras memória: teste sem divergência é Inconclusivo, nunca OK", TesteSemErroInconclusivo),
        ("Regras memória: uma divergência é Atenção e manda repetir", UmaDivergenciaAtencao),
        ("Regras memória: duas divergências na mesma execução é Atenção", DuasDivergenciasAtencao),
        ("Regras memória: duas execuções com erro é Falha", DuasExecucoesComErroFalha),
        ("Regras memória: menos de 4 GiB com teste limpo é Atenção", MenosDe4GiBComTesteAtencao),
        ("Regras memória: menos de 4 GiB sem teste continua Inconclusivo", MenosDe4GiBSemTesteInconclusivo),
        ("Regras memória: TotalBytes ausente sem teste é Inconclusivo com causa", TotalAbsenteSemTesteInconclusivo),
        ("Regras memória: diagnóstico rápido nunca dá OK na memória", DiagnosticoRapidoMemoriaInconclusiva),
        ("Regras memória: campo novo ExecucoesComErro tem padrão 1", CampoNovoTemPadraoUm),
    };

    private static Task SemTesteInconclusivo()
    {
        var r = Regras.Memoria(Memoria(16 * UmGiB));

        Assert.Equal(Veredito.Inconclusivo, r.Veredito);
        Assert.Contains("teste", r.Resumo, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(r.Recomendacoes);
        Assert.Contains(r.Recomendacoes, rec => rec.Acao.Contains("teste", StringComparison.OrdinalIgnoreCase));

        // O inventário continua visível como evidência, mas não sustenta veredito.
        Assert.Contains(r.Evidencias, e => e.Titulo == "Memória total" && e.Valor.Contains("16,0 GiB"));
        return Task.CompletedTask;
    }

    private static Task TesteNaoExecutadoInconclusivo()
    {
        var r = Regras.Memoria(Memoria(16 * UmGiB), Teste(status: StatusTesteMemoria.NaoExecutado, bytesTestados: 0));

        Assert.Equal(Veredito.Inconclusivo, r.Veredito);
        Assert.NotEmpty(r.Recomendacoes);
        return Task.CompletedTask;
    }

    private static Task TesteSemErroInconclusivo()
    {
        var r = Regras.Memoria(Memoria(16 * UmGiB), Teste(status: StatusTesteMemoria.SemErro, erros: 0, bytesTestados: 512L * 1024 * 1024));

        Assert.Equal(Veredito.Inconclusivo, r.Veredito);
        Assert.NotEqual(Veredito.Ok, r.Veredito);

        // O teste apareceu nas evidências: os bytes verificados foram medidos.
        Assert.Contains(r.Evidencias, e => e.Titulo == "Divergências" && e.Valor == "0");
        Assert.NotEmpty(r.Limitacoes);
        return Task.CompletedTask;
    }

    private static Task UmaDivergenciaAtencao()
    {
        var r = Regras.Memoria(Memoria(16 * UmGiB), Teste(status: StatusTesteMemoria.Erro, erros: 1, execucoes: 1));

        Assert.Equal(Veredito.Atencao, r.Veredito);
        Assert.Contains("repet", r.Resumo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(r.Recomendacoes, rec => rec.Acao.Contains("Repetir", StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Duas palavras divergentes na MESMA execução são um evento só, não dois: pode ser um bit preso
    /// num endereço. A regra unificada manda repetir antes de condenar o módulo.
    /// </summary>
    private static Task DuasDivergenciasAtencao()
    {
        var r = Regras.Memoria(Memoria(16 * UmGiB), Teste(status: StatusTesteMemoria.Erro, erros: 2, execucoes: 1));

        Assert.Equal(Veredito.Atencao, r.Veredito);
        Assert.NotEmpty(r.Recomendacoes);
        return Task.CompletedTask;
    }

    private static Task DuasExecucoesComErroFalha()
    {
        // Uma divergência por execução em duas execuções distintas: já confirma o defeito.
        var r = Regras.Memoria(Memoria(16 * UmGiB), Teste(status: StatusTesteMemoria.Erro, erros: 2, execucoes: 2));

        Assert.Equal(Veredito.Falha, r.Veredito);
        Assert.Contains(r.Evidencias, e => e.Titulo == "Execuções com erro" && e.Valor == "2");
        return Task.CompletedTask;
    }

    private static Task MenosDe4GiBComTesteAtencao()
    {
        // Regra de tamanho continua valendo quando o teste rodou — e o teste rodou limpo.
        var r = Regras.Memoria(Memoria(2 * UmGiB), Teste(status: StatusTesteMemoria.SemErro, erros: 0, bytesTestados: 512L * 1024 * 1024));

        Assert.Equal(Veredito.Atencao, r.Veredito);
        Assert.Contains("2,0 GiB", r.Resumo);
        Assert.Contains("pode limitar o uso do computador", r.Resumo);
        return Task.CompletedTask;
    }

    private static Task MenosDe4GiBSemTesteInconclusivo()
    {
        // Regra 1 domina: sem teste, mesmo com memória pequena, o veredito é Inconclusivo (não Atenção).
        var r = Regras.Memoria(Memoria(2 * UmGiB));

        Assert.Equal(Veredito.Inconclusivo, r.Veredito);
        Assert.NotEqual(Veredito.Atencao, r.Veredito);
        return Task.CompletedTask;
    }

    private static Task TotalAbsenteSemTesteInconclusivo()
    {
        var mem = Memoria(0) with { TotalBytes = Leitura<long>.Falha("WMI não retornou módulos físicos") };
        var r = Regras.Memoria(mem);

        Assert.Equal(Veredito.Inconclusivo, r.Veredito);
        Assert.Contains(r.Limitacoes, l => l.Contains("WMI", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(r.Recomendacoes);
        return Task.CompletedTask;
    }

    private static Task DiagnosticoRapidoMemoriaInconclusiva()
    {
        // DiagnosticoRapido.cs chama exatamente Regras.Memoria(inventario.Memoria), sem teste: a categoria
        // nunca pode sair OK. A chamada é a mesma aqui; as etapas reais do diagnóstico (60 s de sensores)
        // não são necessárias para provar a regra e não devem rodar em teste automatizado.
        var inventario = new { Memoria = Memoria(16 * UmGiB) };
        var memoria = Regras.Memoria(inventario.Memoria);

        Assert.Equal(Veredito.Inconclusivo, memoria.Veredito);
        Assert.NotEqual(Veredito.Ok, memoria.Veredito);
        Assert.False(string.IsNullOrWhiteSpace(memoria.Resumo));

        // A agregação da categoria também precisa resultar Inconclusivo (nunca promovida a OK).
        var agregado = VereditoConsolidador.Agregar(new[]
        {
            new ResultadoCategoria { Categoria = Regras.CategoriaMemoria, Veredito = memoria.Veredito, Resumo = memoria.Resumo },
        });
        Assert.Equal(Veredito.Inconclusivo, agregado.Geral);
        return Task.CompletedTask;
    }

    private static Task CampoNovoTemPadraoUm()
    {
        // Quem já criava o record sem o campo continua com uma execução com erro declarada.
        var teste = Teste(status: StatusTesteMemoria.Erro, erros: 1);
        Assert.Equal(1, teste.ExecucoesComErro);
        return Task.CompletedTask;
    }

    private static InfoMemoria Memoria(long totalBytes) => new()
    {
        TotalBytes = Leitura<long>.Ok(totalBytes, "Win32_PhysicalMemory"),
        SlotsUsados = Leitura<int>.Ok(2),
        SlotsTotais = Leitura<int>.Ok(4),
        Timings = Leitura<string>.NaoSuportado("Timings não expostos pelo WMI"),
        Modulos = Array.Empty<ModuloMemoria>(),
    };

    private static ResultadoTesteMemoria Teste(
        StatusTesteMemoria status,
        long erros = 0,
        int execucoes = 1,
        long bytesTestados = 512L * 1024 * 1024) => new()
        {
            ExecutadoEm = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.FromHours(-3)),
            BytesTestados = bytesTestados,
            Padroes = status == StatusTesteMemoria.NaoExecutado
                ? Array.Empty<string>()
                : new[] { "0x00 (todos os bits em zero)", "0xFF (todos os bits em um)" },
            ErrosDetectados = erros,
            ExecucoesComErro = execucoes,
            Duracao = TimeSpan.FromSeconds(40),
            Cancelado = false,
            AvisoTexto = "teste",
            Status = status,
        };
}
