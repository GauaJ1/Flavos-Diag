using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Xml.Linq;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Wmi;

namespace FlavosDiag.Providers.Bateria;

/// <summary>
/// Bateria do notebook: relatório do Windows (`powercfg /batteryreport`) como fonte principal e
/// classes WMI de bateria como complemento. Máquina sem bateria é <b>Não se aplica</b>, não erro.
/// O arquivo temporário do relatório é apagado ao final, inclusive em erro.
/// </summary>
public sealed class BateriaWindowsProvider : IBateriaProvider
{
    private readonly IWmiFonte? _wmi;

    public BateriaWindowsProvider(IWmiFonte? wmi = null)
    {
        _wmi = wmi;
    }

    public string Nome => "Windows (powercfg + WMI)";

    public async Task<InfoBateriaDetalhe> LerAsync(CancellationToken cancelamento = default)
    {
        var limitacoes = new List<DadoAusente>();
        var relatorio = await LerRelatorioAsync(cancelamento);
        var wmi = await LerWmiAsync(cancelamento);

        // "O relatório foi gerado" não prova que existe bateria: em desktop o powercfg gera o XML com
        // <Batteries></Batteries> vazio. Presença exige ao menos uma bateria no relatório ou no WMI.
        var temBateriaNoRelatorio = relatorio is { Baterias.Count: > 0 };
        var presente = temBateriaNoRelatorio || wmi.Count > 0;
        if (!presente)
        {
            limitacoes.Add(new DadoAusente(
                "Bateria",
                "Nenhuma bateria encontrada neste equipamento (computador de mesa ou sem bateria instalada). Não se aplica."));
            return Vazia(presente: false, limitacoes);
        }

        if (relatorio is null)
        {
            limitacoes.Add(new DadoAusente(
                "Relatório de bateria do Windows",
                "Não foi possível gerar o relatório do powercfg; os valores vêm apenas do WMI e podem faltar."));
        }

        var primeira = relatorio?.Baterias.FirstOrDefault();
        var capacidadeProjeto = primeira?.CapacidadeProjetoMwh is > 0
            ? Leitura<int>.Ok(primeira.CapacidadeProjetoMwh!.Value, "powercfg /batteryreport")
            : wmi.Select(l => l.CapacidadeProjetoMwh).FirstOrDefault(v => v.Disponivel) ?? Leitura<int>.NaoSuportado("Capacidade de projeto não informada pelo Windows.");

        var capacidadeAtual = primeira?.CapacidadeTotalMwh is > 0
            ? Leitura<int>.Ok(primeira.CapacidadeTotalMwh!.Value, "powercfg /batteryreport")
            : wmi.Select(l => l.CapacidadeTotalMwh).FirstOrDefault(v => v.Disponivel) ?? Leitura<int>.NaoSuportado("Capacidade atual (última carga completa) não informada.");

        Leitura<double> saude;
        if (capacidadeProjeto.Disponivel && capacidadeAtual.Disponivel && capacidadeProjeto.Valor > 0)
        {
            saude = Leitura<double>.Ok(Math.Round(capacidadeAtual.Valor * 100.0 / capacidadeProjeto.Valor, 1), "atual / projeto");
        }
        else
        {
            saude = Leitura<double>.NaoSuportado("Sem as duas capacidades não é possível calcular a saúde sem inventar número.");
            limitacoes.Add(new DadoAusente("Saúde da bateria", saude.Causa ?? "Capacidades indisponíveis."));
        }

        var ciclos = relatorio?.CiclosDisponiveis == true
            ? Leitura<int>.Ok(relatorio.Ciclos!.Value, "powercfg /batteryreport")
            : wmi.Select(l => l.Ciclos).FirstOrDefault(v => v.Disponivel) ?? Leitura<int>.NaoSuportado("Contagem de ciclos não exposta por esta bateria.");

        var estado = wmi.Select(l => l.Estado).FirstOrDefault(v => v.Disponivel)
                     ?? Leitura<string>.NaoSuportado("Estado de carga não disponível: execute sem bateria conectada ao carregador ou sem WMI de bateria.");

        return new InfoBateriaDetalhe
        {
            Presente = true,
            Nome = primeira?.Nome is { Length: > 0 }
                ? Leitura<string>.Ok(primeira.Nome, "powercfg /batteryreport")
                : wmi.Select(l => l.Nome).FirstOrDefault(v => v.Disponivel) ?? Leitura<string>.NaoSuportado("Nome da bateria não informado."),
            CapacidadeProjetoMwh = capacidadeProjeto,
            CapacidadeAtualMwh = capacidadeAtual,
            Ciclos = ciclos,
            Fabricante = primeira?.Fabricante is { Length: > 0 }
                ? Leitura<string>.Ok(primeira.Fabricante, "powercfg /batteryreport")
                : Leitura<string>.NaoSuportado("Fabricante não informado pelo firmware da bateria."),
            NumeroSerie = primeira?.NumeroSerie is { Length: > 0 }
                ? Leitura<string>.Ok(primeira.NumeroSerie, "powercfg /batteryreport")
                : Leitura<string>.NaoSuportado("Número de série da bateria não informado."),
            Estado = estado,
            SaudePercentual = saude,
            ColetadoEm = DateTimeOffset.Now,
            Limitacoes = limitacoes,
        };
    }

    private static InfoBateriaDetalhe Vazia(bool presente, List<DadoAusente> limitacoes) => new()
    {
        Presente = presente,
        Nome = Leitura<string>.NaoSuportado("Sem bateria."),
        CapacidadeProjetoMwh = Leitura<int>.NaoSuportado("Sem bateria."),
        CapacidadeAtualMwh = Leitura<int>.NaoSuportado("Sem bateria."),
        Ciclos = Leitura<int>.NaoSuportado("Sem bateria."),
        Fabricante = Leitura<string>.NaoSuportado("Sem bateria."),
        NumeroSerie = Leitura<string>.NaoSuportado("Sem bateria."),
        Estado = Leitura<string>.NaoSuportado("Sem bateria."),
        SaudePercentual = Leitura<double>.NaoSuportado("Sem bateria: não se aplica."),
        ColetadoEm = DateTimeOffset.Now,
        Limitacoes = limitacoes,
    };

    // ------------------------------------------------------------- powercfg

    public sealed record BateriaRelatorio(
        string? Nome,
        string? Fabricante,
        string? NumeroSerie,
        int? CapacidadeProjetoMwh,
        int? CapacidadeTotalMwh,
        int? Ciclos);

    public sealed record RelatorioBateria(IReadOnlyList<BateriaRelatorio> Baterias, bool CiclosDisponiveis, int? Ciclos, string? Erro);

    private static async Task<RelatorioBateria?> LerRelatorioAsync(CancellationToken cancelamento)
    {
        var executavel = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
        if (!File.Exists(executavel))
        {
            return null;
        }

        var arquivo = Path.Combine(Path.GetTempPath(), "flavos-bateria-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            var psi = new ProcessStartInfo(executavel)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("/batteryreport");
            psi.ArgumentList.Add("/xml");
            psi.ArgumentList.Add("/output");
            psi.ArgumentList.Add(arquivo);

            using var processo = new Process { StartInfo = psi };
            try
            {
                processo.Start();
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return null;
            }

            var stdout = processo.StandardOutput.ReadToEndAsync();
            var stderr = processo.StandardError.ReadToEndAsync();
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
            limite.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                await processo.WaitForExitAsync(limite.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    processo.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                cancelamento.ThrowIfCancellationRequested();
                return null;
            }

            _ = stdout.Result;
            _ = stderr.Result;

            if (!File.Exists(arquivo))
            {
                return null;
            }

            var texto = await File.ReadAllTextAsync(arquivo, cancelamento);
            return InterpretarRelatorio(texto);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(arquivo))
                {
                    File.Delete(arquivo);
                }
            }
            catch (IOException)
            {
                // arquivo em uso por índice de busca do Windows: será limpo na próxima execução
            }
        }
    }

    /// <summary>Interpreta o XML do relatório de bateria (função pura, testável com fixture).</summary>
    public static RelatorioBateria InterpretarRelatorio(string xml)
    {
        var doc = XDocument.Parse(xml);
        var baterias = new List<BateriaRelatorio>();
        string? Desesc(string? v) => string.IsNullOrWhiteSpace(v) ? null : System.Net.WebUtility.HtmlDecode(v.Trim());

        foreach (var b in doc.Descendants().Where(e => e.Name.LocalName == "Battery"))
        {
            int? Ler(string nome)
            {
                var elemento = b.Descendants().FirstOrDefault(e => e.Name.LocalName == nome);
                var texto = elemento?.Value?.Trim();
                return int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : null;
            }

            string? Texto(string nome)
            {
                var elemento = b.Descendants().FirstOrDefault(e => e.Name.LocalName == nome);
                return Desesc(elemento?.Value);
            }

            baterias.Add(new BateriaRelatorio(
                Texto("Id"),
                Texto("Manufacturer"),
                Texto("SerialNumber"),
                Ler("DesignCapacity"),
                Ler("FullChargeCapacity"),
                Ler("CycleCount")));
        }

        // O relatório HTML traz a contagem de ciclos numa célula própria; no XML ela é opcional.
        var ciclos = baterias.Select(b => b.Ciclos).FirstOrDefault(c => c is > 0);
        return new RelatorioBateria(baterias, ciclos is not null, ciclos, null);
    }

    // ------------------------------------------------------------- WMI

    private sealed record LinhaBateria(
        Leitura<string> Nome,
        Leitura<string> Estado,
        Leitura<int> CapacidadeProjetoMwh,
        Leitura<int> CapacidadeTotalMwh,
        Leitura<int> Ciclos);

    private async Task<List<LinhaBateria>> LerWmiAsync(CancellationToken cancelamento)
    {
        var lista = new List<LinhaBateria>();
        if (_wmi is null)
        {
            return lista;
        }

        try
        {
            var linhas = await _wmi.ConsultarAsync(
                "SELECT Name, EstimatedChargeRemaining, BatteryStatus, DesignCapacity, FullChargeCapacity FROM Win32_Battery",
                "root\\cimv2",
                cancelamento).ConfigureAwait(false);

            foreach (var linha in linhas)
            {
                var estado = linha.Inteiro("BatteryStatus") switch
                {
                    1 => "Descarregando",
                    2 => "Conectada à tomada (carregando ou cheia)",
                    3 => "Totalmente carregada",
                    4 => "Tensão baixa",
                    5 => "Crítica",
                    6 => "Carregando",
                    7 => "Carregando e com tensão alta",
                    8 => "Carregando e com tensão baixa",
                    9 => "Carregando e crítica",
                    11 => "Em uso parcialmente carregada",
                    _ => null,
                };

                var nome = linha.Texto("Name");
                lista.Add(new LinhaBateria(
                    nome is null ? Leitura<string>.NaoSuportado("Win32_Battery não informou o nome.") : Leitura<string>.Ok(nome, "Win32_Battery"),
                    estado is null
                        ? Leitura<string>.NaoSuportado("Estado de carga não informado pelo WMI.")
                        : Leitura<string>.Ok(estado, "Win32_Battery.BatteryStatus"),
                    Leitura<int>.NaoSuportado("O WMI não expõe a capacidade de projeto em mWh."),
                    Leitura<int>.NaoSuportado("O WMI não expõe a capacidade atual em mWh."),
                    Leitura<int>.NaoSuportado("O WMI não expõe a contagem de ciclos.")));
            }

            var estaticas = await _wmi.ConsultarAsync(
                "SELECT DesignedCapacity, FullChargedCapacity, CycleCount FROM BatteryStaticData",
                "root\\WMI",
                cancelamento).ConfigureAwait(false);
            foreach (var estatica in estaticas)
            {
                var projeto = estatica.Inteiro("DesignedCapacity");
                var cheia = estatica.Inteiro("FullChargedCapacity");
                var ciclos = estatica.Inteiro("CycleCount");
                for (var i = 0; i < lista.Count; i++)
                {
                    lista[i] = lista[i] with
                    {
                        CapacidadeProjetoMwh = projeto is > 0
                            ? Leitura<int>.Ok((int)projeto.Value, "BatteryStaticData")
                            : lista[i].CapacidadeProjetoMwh,
                        CapacidadeTotalMwh = cheia is > 0
                            ? Leitura<int>.Ok((int)cheia.Value, "BatteryStaticData")
                            : lista[i].CapacidadeTotalMwh,
                        Ciclos = ciclos is > 0
                            ? Leitura<int>.Ok((int)ciclos.Value, "BatteryStaticData")
                            : lista[i].Ciclos,
                    };
                }
            }
        }
        catch (WmiIndisponivelException)
        {
            // Sem WMI de bateria: o relatório do powercfg continua sendo a fonte.
        }

        return lista;
    }
}
