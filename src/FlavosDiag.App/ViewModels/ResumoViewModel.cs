using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.Core;
using FlavosDiag.Core.Diagnostics;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.App.ViewModels;

public sealed partial class ResumoViewModel : PaginaBase
{
    public ResumoViewModel(MainViewModel principal) : base(principal, "Resumo", "IconeResumo")
    {
    }

    public ObservableCollection<ResultadoCategoria> Categorias { get; } = new();

    public ObservableCollection<string> Etapas { get; } = new();

    [ObservableProperty]
    private Veredito _vereditoGeral = VereditoInconclusivoInicial;

    [ObservableProperty]
    private string _resumoGeral = "Nenhum diagnóstico executado nesta sessão.";

    [ObservableProperty]
    private string _etapaAtual = string.Empty;

    [ObservableProperty]
    private double _progresso;

    [ObservableProperty]
    private bool _executando;

    [ObservableProperty]
    private string _duracaoTexto = string.Empty;

    private const Veredito VereditoInconclusivoInicial = Veredito.Inconclusivo;

    [ObservableProperty]
    private string? _ultimaLimitacao;

    protected override Task CarregarAsync() => Task.CompletedTask;

    [RelayCommand]
    public async Task DiagnosticoRapidoAsync(CancellationToken cancelamento)
    {
        if (Executando)
        {
            return;
        }

        Executando = true;
        Erro = null;
        Etapas.Clear();
        Categorias.Clear();
        Progresso = 0;
        var progresso = new Progress<string>(p =>
        {
            EtapaAtual = p;
            Etapas.Add(p);
            Progresso = Math.Min(0.95, Progresso + 0.16);
        });

        try
        {
            var diagnostico = new DiagnosticoRapido(
                Principal.Servicos.Inventario,
                Principal.Servicos.Discos,
                Principal.Servicos.Bateria,
                Principal.Servicos.Sensores,
                Principal.Servicos.Rede);

            var resultado = await diagnostico.ExecutarAsync(Principal.Configuracao.LimiteTemperaturaCpuCelsius, progresso, cancelamento);

            Principal.UltimoDiagnostico = resultado;
            Principal.Inventario = resultado.Inventario;
            Principal.Discos = resultado.Discos;
            Principal.Bateria = resultado.Bateria;
            Principal.Rede = resultado.Rede;
            Principal.AmostrasSensores = resultado.AmostrasSensores;

            foreach (var c in resultado.Categorias)
            {
                Categorias.Add(c);
            }

            VereditoGeral = resultado.Agregacao.Geral;
            ResumoGeral = resultado.Agregacao.Resumo;
            DuracaoTexto = "Duração: " + FormatacaoBr.Duracao(resultado.Duracao);
            UltimaLimitacao = resultado.Limitacoes.Count > 0
                ? $"{resultado.Limitacoes.Count} limitação(ões) registrada(s) — veja a aba Laudo."
                : "Nenhuma limitação registrada.";
            Progresso = 1;
            EtapaAtual = resultado.Cancelado ? "Cancelado." : "Concluído.";

            Principal.SistemaVm.Recarregar();
            Principal.DiscoVm.Recarregar();
            Principal.BateriaVm.Recarregar();
            Principal.RedeVm.Recarregar();
            Principal.MemoriaVm.Recarregar();
            Principal.CpuVm.Recarregar();
            Principal.LaudoVm.Recalcular();
        }
        catch (OperationCanceledException)
        {
            EtapaAtual = "Diagnóstico cancelado.";
            Erro = "Diagnóstico rápido cancelado pelo usuário.";
        }
        catch (Exception ex)
        {
            Erro = "Falha no diagnóstico rápido: " + ex.Message;
        }
        finally
        {
            Executando = false;
        }
    }
}
