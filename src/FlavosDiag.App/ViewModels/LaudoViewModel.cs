using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.App.Services;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Reports;

namespace FlavosDiag.App.ViewModels;

/// <summary>
/// Página do laudo: monta o documento a partir do diagnóstico da sessão e grava JSON + PDF
/// na pasta escolhida pelo técnico. Sem diagnóstico, nada é gerado — nunca um laudo vazio.
/// </summary>
public sealed partial class LaudoViewModel : PaginaBase
{
    private readonly ExportadorLaudo _exportador;

    public LaudoViewModel(MainViewModel principal, ExportadorLaudo? exportador = null)
        : base(principal, "Laudo", "IconeLaudo")
    {
        _exportador = exportador ?? new ExportadorLaudo();
    }

    /// <summary>Laudo montado a partir do último diagnóstico (null enquanto não houver diagnóstico).</summary>
    [ObservableProperty]
    private Laudo? _laudoAtual;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _podeExportar;

    [ObservableProperty]
    private Veredito _vereditoGeral = Veredito.Inconclusivo;

    [ObservableProperty]
    private string _resumoGeral = string.Empty;

    [ObservableProperty]
    private string _numeroOs = string.Empty;

    [ObservableProperty]
    private bool _mascararSeriais = true;

    [ObservableProperty]
    private string? _avisoExportacao;

    [ObservableProperty]
    private bool _demonstracao;

    public ObservableCollection<ResultadoCategoria> Categorias { get; } = new();

    public ObservableCollection<string> Limitacoes { get; } = new();

    public ObservableCollection<Recomendacao> Recomendacoes { get; } = new();

    public string AvisoPontuacao => Laudo.AvisoPontuacao;

    /// <summary>Recalcula a prévia a partir do diagnóstico atual. Chamado após o diagnóstico rápido.</summary>
    public void Recalcular()
    {
        var diagnostico = Principal.UltimoDiagnostico;
        if (diagnostico is null)
        {
            LaudoAtual = null;
            PodeExportar = false;
            Status = "Nenhum diagnóstico nesta sessão. Execute o diagnóstico rápido na aba Resumo para poder montar o laudo.";
            Categorias.Clear();
            Limitacoes.Clear();
            Recomendacoes.Clear();
            return;
        }

        try
        {
            var configuracao = Principal.Configuracao;
            var tecnico = new DadosTecnico
            {
                Nome = configuracao.NomeTecnico,
                Empresa = configuracao.Empresa,
                Cnpj = configuracao.Cnpj,
                Contato = configuracao.Contato,
            };

            var laudo = LaudoBuilder.A_partirDiagnostico(
                diagnostico,
                tecnico,
                NumeroOs,
                Principal.Versao,
                MascararSeriais,
                Principal.Demonstracao,
                Principal.Checklist,
                configuracao.LimiteTemperaturaCpuCelsius);

            LaudoAtual = laudo;
            VereditoGeral = laudo.VereditoGeral;
            ResumoGeral = laudo.Resumo;
            Demonstracao = laudo.ModoDemonstracao;
            PodeExportar = true;
            Status = $"Laudo pronto: {laudo.Categorias.Count} categoria(s), {laudo.Evidencias.Count} evidência(s), {laudo.Limitacoes.Count} limitação(ões).";

            Categorias.Clear();
            foreach (var c in laudo.Categorias)
            {
                Categorias.Add(c);
            }

            Limitacoes.Clear();
            foreach (var l in laudo.Limitacoes)
            {
                Limitacoes.Add(l);
            }

            Recomendacoes.Clear();
            foreach (var r in laudo.Recomendacoes)
            {
                Recomendacoes.Add(r);
            }
        }
        catch (Exception ex)
        {
            LaudoAtual = null;
            PodeExportar = false;
            Status = "Não foi possível montar o laudo: " + ex.Message;
        }
    }

    protected override Task CarregarAsync()
    {
        Recalcular();
        return Task.CompletedTask;
    }

    partial void OnMascararSeriaisChanged(bool value)
    {
        // Reflete a preferência do técnico na configuração (usada nos próximos laudos).
        Principal.Configuracao.MascararSeriais = value;
        if (LaudoAtual is not null)
        {
            Recalcular();
        }
    }

    [RelayCommand]
    private void Atualizar() => Recalcular();

    [RelayCommand]
    private async Task ExportarAsync(CancellationToken cancelamento)
    {
        if (LaudoAtual is null)
        {
            AvisoExportacao = "Não há laudo para exportar. Execute o diagnóstico rápido primeiro.";
            return;
        }

        var (ok, pasta) = Principal.EscolherPasta();
        if (!ok || string.IsNullOrWhiteSpace(pasta))
        {
            AvisoExportacao = "Exportação cancelada: nenhuma pasta escolhida.";
            return;
        }

        AvisoExportacao = null;
        await ExecutarAsync(async () =>
        {
            var resultado = await _exportador.ExportarAsync(LaudoAtual, pasta, cancelamento);
            AvisoExportacao = "Laudo gravado:" + Environment.NewLine +
                              "JSON: " + resultado.PastaJson + Environment.NewLine +
                              "PDF: " + resultado.ArquivoPdf;
        }, "Não foi possível gravar o laudo na pasta escolhida.");
    }
}
