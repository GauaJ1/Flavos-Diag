using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;

namespace FlavosDiag.App.Services;

/// <summary>
/// Gera o diagnóstico técnico dos sensores: ambiente, CPU e <b>todos</b> os sensores, inclusive os
/// que não devolveram valor. É o arquivo que o dono envia ao suporte quando a temperatura não
/// aparece — sem ele, a única informação disponível seria "não funcionou".
/// </summary>
/// <remarks>
/// Sem números de série: o identificador da máquina passa pelo mascaramento já existente quando a
/// opção está ligada. Os sensores aparecem com tipo, nome, identificador, valor, mínimo e máximo,
/// porque é exatamente a combinação que revela sentinela (valor 0 com min 0 e max 0).
/// </remarks>
public sealed class DiagnosticoSensoresDump
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Servicos _servicos;

    public DiagnosticoSensoresDump(Servicos servicos) => _servicos = servicos;

    /// <summary>
    /// Coleta tudo e grava dois arquivos (.json e .txt) na pasta pedida. Devolve o caminho do JSON.
    /// </summary>
    public async Task<(string Json, string Texto)> ExportarAsync(
        string pasta,
        bool mascararIdentificadores,
        CancellationToken cancelamento = default)
    {
        var dados = await ColetarAsync(mascararIdentificadores, cancelamento);

        Directory.CreateDirectory(pasta);
        var carimbo = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var json = Path.Combine(pasta, $"diagnostico-sensores_{carimbo}.json");
        var text = Path.Combine(pasta, $"diagnostico-sensores_{carimbo}.txt");

        await File.WriteAllTextAsync(json, JsonSerializer.Serialize(dados, Opcoes), cancelamento);
        await File.WriteAllTextAsync(text, MontarTexto(dados), cancelamento);

        return (json, text);
    }

    /// <summary>Coleta os dados sem gravar (usado também pelo argumento --dump-sensores).</summary>
    public async Task<DumpSensores> ColetarAsync(bool mascararIdentificadores, CancellationToken cancelamento = default)
    {
        var elevado = ProcessoElevado();
        var pawnIo = ConsultarPawnIo();

        string nomeCpu = "não identificado";
        string? nucleos = null;
        try
        {
            var inventario = await _servicos.Inventario.ColetarAsync(cancelamento);
            if (inventario.Cpu.Nome.Disponivel && inventario.Cpu.Nome.Valor is { Length: > 0 } n)
            {
                nomeCpu = n;
            }

            if (inventario.Cpu.NucleosFisicos.Disponivel && inventario.Cpu.ThreadsLogicas.Disponivel)
            {
                nucleos = $"{inventario.Cpu.NucleosFisicos.Valor} núcleos / {inventario.Cpu.ThreadsLogicas.Valor} threads";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            nomeCpu = "não foi possível ler a CPU pelo WMI: " + ex.Message;
        }

        // Abre uma sessão só para o diagnóstico; é fechada em seguida para não segurar o driver.
        AmostraSensores? amostra = null;
        string? causaAbertura = null;
        ISensorProvider? sessao = null;
        try
        {
            sessao = _servicos.Sensores.Criar();
            var abertura = await sessao.AbrirAsync(cancelamento);
            causaAbertura = abertura.Disponivel ? null : abertura.Causa;
            amostra = sessao.Ler();
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            causaAbertura = "Falha ao abrir ou ler os sensores: " + ex.Message;
        }
        finally
        {
            if (sessao is not null)
            {
                await sessao.DisposeAsync();
            }
        }

        var valores = amostra?.Valores ?? Array.Empty<ValorSensor>();
        var semLeitura = amostra?.SemLeitura ?? Array.Empty<SensorSemLeitura>();

        var comLeituraTermicos = valores.Count(v =>
            v.Categoria == CategoriaSensor.Temperatura &&
            v.TipoHardware is TipoHardware.Cpu or TipoHardware.Gpu);
        var termicosTotais = comLeituraTermicos + semLeitura.Count(s =>
            s.Categoria == CategoriaSensor.Temperatura &&
            s.TipoHardware is TipoHardware.Cpu or TipoHardware.Gpu);

        var fatos = DiagnosticoSensores.Avaliar(elevado, pawnIo, termicosTotais, comLeituraTermicos, causaAbertura);

        return new DumpSensores
        {
            GeradoEm = DateTimeOffset.Now,
            Aplicativo = new InfoAplicativo
            {
                Versao = _servicos.Versao,
                Biblioteca = "LibreHardwareMonitorLib",
                VersaoBiblioteca = typeof(LibreHardwareMonitor.Hardware.Computer).Assembly
                    .GetName().Version?.ToString() ?? "desconhecida",
                ModoDemonstracao = _servicos.Demonstracao,
            },
            Ambiente = new InfoAmbiente
            {
                Windows = Environment.OSVersion.VersionString,
                Eh64Bits = Environment.Is64BitProcess,
                ProcessoElevado = elevado,
                PawnIoInstalado = pawnIo,
                PawnIoTexto = pawnIo switch
                {
                    true => "instalado",
                    false => "não instalado",
                    null => "não determinado",
                },
                ComandoSugerido = DiagnosticoSensores.SugereInstalacaoDeDriver(fatos)
                    ? DiagnosticoSensores.ComandoInstalacao
                    : null,
            },
            Causa = new InfoCausa
            {
                Classificacao = DiagnosticoSensores.Classificar(fatos).ToString(),
                Texto = DiagnosticoSensores.TextoDeCausa(fatos),
            },
            Cpu = new InfoCpuDump
            {
                Nome = nomeCpu,
                NucleosThreads = nucleos,
                SensoresTermicosReconhecidos = termicosTotais,
                SensoresTermicosComLeitura = comLeituraTermicos,
            },
            Sensores = valores.Select(v => new SensorDump
            {
                Id = v.Id,
                TipoHardware = v.TipoHardware.ToString(),
                NomeHardware = v.NomeHardware,
                NomeSensor = v.NomeSensor,
                Categoria = v.Categoria.ToString(),
                Valor = v.Valor,
                Unidade = v.Unidade,
                Qualidade = v.Qualidade.ToString(),
            }).ToList(),
            SensoresSemLeitura = semLeitura.Select(s => new SensorDump
            {
                Id = s.Id,
                TipoHardware = s.TipoHardware.ToString(),
                NomeHardware = s.NomeHardware,
                NomeSensor = s.NomeSensor,
                Categoria = s.Categoria.ToString(),
                Valor = s.ValorBruto,
                Minimo = s.Minimo,
                Maximo = s.Maximo,
                Unidade = s.Unidade,
                Qualidade = nameof(QualidadeSensor.SemLeitura),
                Motivo = s.Motivo,
            }).ToList(),
        };
    }

    private bool ProcessoElevado()
    {
        try
        {
            return _servicos.Elevacao.Elevado;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private bool? ConsultarPawnIo()
    {
        try
        {
            return FlavosDiag.Providers.Sensores.SensoresHardwareProvider.ConsultarPawnIo();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Versão em texto, para colar num relato de suporte.</summary>
    public static string MontarTexto(DumpSensores d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FLAVOS DIAG — DIAGNÓSTICO DE SENSORES");
        sb.AppendLine("Gerado em: " + d.GeradoEm.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("pt-BR")));
        sb.AppendLine();
        sb.AppendLine("APLICATIVO");
        sb.AppendLine("  Versão:              " + d.Aplicativo.Versao);
        sb.AppendLine("  Biblioteca:          " + d.Aplicativo.VersaoBiblioteca);
        sb.AppendLine("  Modo demonstração:   " + (d.Aplicativo.ModoDemonstracao ? "sim" : "não"));
        sb.AppendLine();
        sb.AppendLine("AMBIENTE");
        sb.AppendLine("  Windows:             " + d.Ambiente.Windows);
        sb.AppendLine("  Processo 64 bits:    " + (d.Ambiente.Eh64Bits ? "sim" : "não"));
        sb.AppendLine("  Elevado (admin):     " + (d.Ambiente.ProcessoElevado ? "sim" : "não"));
        sb.AppendLine("  PawnIO:              " + d.Ambiente.PawnIoTexto);
        if (d.Ambiente.ComandoSugerido is { } cmd)
        {
            sb.AppendLine("  Comando sugerido:    " + cmd);
        }

        sb.AppendLine();
        sb.AppendLine("CPU");
        sb.AppendLine("  Nome:                " + d.Cpu.Nome);
        if (d.Cpu.NucleosThreads is { } nt)
        {
            sb.AppendLine("  Núcleos/threads:     " + nt);
        }

        sb.AppendLine("  Sensores térmicos:   " + d.Cpu.SensoresTermicosReconhecidos + " reconhecido(s), " +
                      d.Cpu.SensoresTermicosComLeitura + " com leitura");
        sb.AppendLine();
        sb.AppendLine("CAUSA");
        sb.AppendLine("  " + d.Causa.Classificacao);
        sb.AppendLine("  " + (string.IsNullOrWhiteSpace(d.Causa.Texto) ? "há leitura; nada a relatar" : d.Causa.Texto));
        sb.AppendLine();
        sb.AppendLine($"SENSORES SEM LEITURA ({d.SensoresSemLeitura.Count})");
        if (d.SensoresSemLeitura.Count == 0)
        {
            sb.AppendLine("  (nenhum)");
        }

        foreach (var s in d.SensoresSemLeitura)
        {
            sb.AppendLine($"  [{s.TipoHardware}] {s.NomeHardware} / {s.NomeSensor}");
            sb.AppendLine($"      id:      {s.Id}");
            sb.AppendLine($"      valor:   {Texto(s.Valor)}   mín: {Texto(s.Minimo)}   máx: {Texto(s.Maximo)}");
            sb.AppendLine($"      motivo:  {s.Motivo}");
        }

        sb.AppendLine();
        sb.AppendLine($"SENSORES COM LEITURA ({d.Sensores.Count})");
        foreach (var s in d.Sensores)
        {
            sb.AppendLine($"  [{s.TipoHardware}] {s.NomeHardware} / {s.NomeSensor} = {Texto(s.Valor)} {s.Unidade}");
        }

        return sb.ToString();
    }

    private static string Texto(double? v) =>
        v is { } x ? x.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR")) : "—";
}

/// <summary>Dados do arquivo de diagnóstico de sensores.</summary>
public sealed record DumpSensores
{
    public required DateTimeOffset GeradoEm { get; init; }

    public required InfoAplicativo Aplicativo { get; init; }

    public required InfoAmbiente Ambiente { get; init; }

    public required InfoCausa Causa { get; init; }

    public required InfoCpuDump Cpu { get; init; }

    public required IReadOnlyList<SensorDump> Sensores { get; init; }

    public required IReadOnlyList<SensorDump> SensoresSemLeitura { get; init; }
}

public sealed record InfoAplicativo
{
    public required string Versao { get; init; }

    public required string Biblioteca { get; init; }

    public required string VersaoBiblioteca { get; init; }

    public required bool ModoDemonstracao { get; init; }
}

public sealed record InfoAmbiente
{
    public required string Windows { get; init; }

    public required bool Eh64Bits { get; init; }

    public required bool ProcessoElevado { get; init; }

    public required bool? PawnIoInstalado { get; init; }

    public required string PawnIoTexto { get; init; }

    public string? ComandoSugerido { get; init; }
}

public sealed record InfoCausa
{
    public required string Classificacao { get; init; }

    public required string Texto { get; init; }
}

public sealed record InfoCpuDump
{
    public required string Nome { get; init; }

    public string? NucleosThreads { get; init; }

    public required int SensoresTermicosReconhecidos { get; init; }

    public required int SensoresTermicosComLeitura { get; init; }
}

public sealed record SensorDump
{
    public required string Id { get; init; }

    public required string TipoHardware { get; init; }

    public string? NomeHardware { get; init; }

    public string? NomeSensor { get; init; }

    public required string Categoria { get; init; }

    public double? Valor { get; init; }

    public double? Minimo { get; init; }

    public double? Maximo { get; init; }

    public string? Unidade { get; init; }

    public required string Qualidade { get; init; }

    public string? Motivo { get; init; }
}
