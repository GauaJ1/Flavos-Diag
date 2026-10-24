using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Providers.Discos;

namespace FlavosDiag.App.ViewModels;

// Páginas de diagnóstico das fases 2 a 4. Cada uma mostra apenas o que foi realmente coletado na sessão;
// quando não há leitura, exibe o motivo — nunca um valor inventado.

/// <summary>Uma linha de informação exibida nas telas: rótulo, valor e eventual motivo de ausência.</summary>
public sealed class LinhaInfo
{
    public LinhaInfo(string rotulo, string valor, string? causa = null)
    {
        Rotulo = rotulo;
        Valor = valor;
        Causa = causa;
    }

    public string Rotulo { get; }

    public string Valor { get; }

    public string? Causa { get; }

    /// <summary>Verdadeiro quando o valor não pôde ser obtido: a tela destaca em tom de aviso.</summary>
    public bool Ausente => !string.IsNullOrEmpty(Causa);

    /// <summary>Monta a linha a partir de uma leitura, com formatador opcional.</summary>
    public static LinhaInfo De<T>(string rotulo, Leitura<T> leitura, Func<T, string>? formatador = null)
    {
        if (leitura.Disponivel && leitura.Valor is not null)
        {
            return new LinhaInfo(rotulo, formatador is null ? leitura.Valor.ToString()! : formatador(leitura.Valor));
        }

        return new LinhaInfo(rotulo, FormatacaoBr.NaoDisponivel(leitura.Causa), leitura.Causa ?? "Não disponível");
    }
}

public sealed partial class DiscoViewModel : PaginaBase
{
    public DiscoViewModel(MainViewModel principal) : base(principal, "Disco", "IconeDisco")
    {
    }

    public ObservableCollection<DiscoExibicao> Discos { get; } = new();

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private string? _resumo;

    public void Recarregar() => _ = CarregarAsync(recarregar: true);

    protected override Task CarregarAsync() => CarregarAsync(recarregar: false);

    private async Task CarregarAsync(bool recarregar)
    {
        if (recarregar || Principal.Discos.Count == 0)
        {
            // Aviso antes da leitura: o disco só é consultado (somente leitura) com o aceite do usuário.
            // Sem aceite, o provider não abre o disco com acesso de escrita e a saúde fica Inconclusivo.
            Principal.AutorizarLeituraDisco();
            await ExecutarAsync(async () =>
            {
                Principal.Discos = await Principal.Servicos.Discos.ListarAsync();
            }, "Não foi possível ler os discos.");
        }

        Discos.Clear();
        foreach (var disco in Principal.Discos)
        {
            Discos.Add(new DiscoExibicao(disco));
        }

        Resumo = Principal.Discos.Count == 0
            ? "Nenhum disco foi lido. Use o diagnóstico rápido para coletar."
            : $"{Principal.Discos.Count} disco(s) encontrado(s).";

        // Diz o que ficou faltando e por quê: sem aceite ou sem elevação a saúde não foi lida, e o
        // técnico precisa saber disso sem abrir o laudo.
        var causaLeitura = !Principal.Servicos.LeituraDiscoAutorizada.Autorizada
            ? "Leitura do disco não autorizada: clique em Atualizar para ver o aviso e autorizar a consulta de saúde."
            : !Principal.Servicos.Elevacao.Elevado
                ? "Sem administrador: o Windows só libera a leitura SMART/NVMe com elevação, então a saúde fica Inconclusivo."
                : null;

        Status = Principal.Discos.Count == 0
            ? null
            : causaLeitura
                ?? "A saúde vem dos dados que o próprio disco ou o Windows reportaram. Nenhum laudo substitui backup.";
    }

    [RelayCommand]
    private Task Atualizar() => CarregarAsync(recarregar: true);
}

/// <summary>Disco pronto para exibição, com a saúde resumida.</summary>
public sealed class DiscoExibicao
{
    public DiscoExibicao(DiscoInfo disco)
    {
        Nome = disco.Modelo.Disponivel && !string.IsNullOrWhiteSpace(disco.Modelo.Valor)
            ? disco.Modelo.Valor!
            : "Disco " + disco.Numero;

        Tipo = disco.Midia.Disponivel
            ? disco.Midia.Valor switch
            {
                TipoMidia.Ssd => "SSD",
                TipoMidia.Hdd => "HDD",
                _ => "Tipo não identificado",
            }
            : FormatacaoBr.NaoDisponivel(disco.Midia.Causa);

        var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(disco.Saude);

        // Regra do projeto: nenhuma fonte respondida nunca vira OK.
        Veredito = !temLeitura
            ? Veredito.Inconclusivo
            : achados.Any(a => a.Critico)
                ? Veredito.Falha
                : achados.Any(a => a.Aviso)
                    ? Veredito.Atencao
                    : Veredito.Ok;

        VereditoTexto = Veredito.Rotulo();
        Detalhes = Montar(disco, achados);
    }

    public string Nome { get; }

    public string Tipo { get; }

    public Veredito Veredito { get; }

    public string VereditoTexto { get; }

    public IReadOnlyList<LinhaInfo> Detalhes { get; }

    private static IReadOnlyList<LinhaInfo> Montar(DiscoInfo disco, IReadOnlyList<AvaliadorSaudeDisco.Achado> achados)
    {
        var linhas = new List<LinhaInfo>
        {
            LinhaInfo.De("Capacidade", disco.TamanhoBytes, FormatacaoBr.Bytes),
            LinhaInfo.De("Barramento", disco.Barramento),
            LinhaInfo.De("Firmware", disco.Firmware),
            LinhaInfo.De("Número de série", disco.NumeroSerie, s => Mascaramento.Mascarar(s)),
        };

        var saude = disco.Saude;
        linhas.Add(new LinhaInfo(
            "Fontes de saúde consultadas",
            saude.Fontes.Count == 0 ? "Nenhuma respondeu" : string.Join(", ", saude.Fontes.Select(DescreverFonte)),
            saude.Fontes.Count == 0 ? "sem fonte de saúde" : null));

        if (saude.Nvme is { } nvme)
        {
            linhas.Add(LinhaInfo.De("Temperatura", nvme.TemperaturaCelsius, v => v + " °C"));
            linhas.Add(LinhaInfo.De("Vida usada", nvme.PercentualVidaUsada, v => $"{v:0} %"));
            linhas.Add(LinhaInfo.De("Horas ligado", nvme.HorasLigado, v => v + " h"));
            linhas.Add(LinhaInfo.De("Erros de integridade da mídia", nvme.ErrosIntegridadeMidia));
        }

        if (saude.Confiabilidade is { } confiabilidade)
        {
            linhas.Add(LinhaInfo.De("Temperatura (Windows)", confiabilidade.TemperaturaCelsius, v => v + " °C"));
            linhas.Add(LinhaInfo.De("Desgaste estimado", confiabilidade.DesgastePercentual, v => v + " %"));
            linhas.Add(LinhaInfo.De("Erros de leitura não corrigidos", confiabilidade.ErrosLeituraNaoCorrigidos));
        }

        foreach (var achado in achados)
        {
            linhas.Add(new LinhaInfo(achado.Titulo, achado.Valor, achado.Critico ? "crítico" : null));
        }

        foreach (var limitacao in saude.Limitacoes)
        {
            linhas.Add(new LinhaInfo("Limitação: " + limitacao.Item, limitacao.Motivo, "informação não coletada"));
        }

        return linhas;
    }

    private static string DescreverFonte(FonteSaudeDisco fonte) => fonte switch
    {
        FonteSaudeDisco.SmartAta => "Atributos do disco (ATA)",
        FonteSaudeDisco.Nvme => "Registro de saúde NVMe",
        FonteSaudeDisco.ConfiabilidadeWindows => "Contadores do Windows",
        FonteSaudeDisco.EstadoWindows => "Estado de saúde do Windows",
        _ => "Nenhuma",
    };
}
