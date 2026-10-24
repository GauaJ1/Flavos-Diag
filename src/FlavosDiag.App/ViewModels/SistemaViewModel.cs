using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Wmi;

namespace FlavosDiag.App.ViewModels;

/// <summary>E1 — inventário de hardware.</summary>
public sealed partial class SistemaViewModel : PaginaBase
{
    public SistemaViewModel(MainViewModel principal) : base(principal, "Sistema", "IconeSistema")
    {
    }

    public ObservableCollection<LinhaCampo> CamposSistema { get; } = new();

    public ObservableCollection<LinhaCampo> CamposBios { get; } = new();

    public ObservableCollection<LinhaCampo> CamposCpu { get; } = new();

    public ObservableCollection<LinhaCampo> CamposMemoria { get; } = new();

    public ObservableCollection<ModuloMemoria> Modulos { get; } = new();

    public ObservableCollection<LinhaCampo> CamposCadaGpu { get; } = new();

    public ObservableCollection<LinhaCampo> CamposCadaTela { get; } = new();

    public ObservableCollection<LinhaCampo> CamposRede { get; } = new();

    public ObservableCollection<LinhaCampo> CamposBateria { get; } = new();

    public ObservableCollection<DispositivoComProblema> Dispositivos { get; } = new();

    public ObservableCollection<string> Limitacoes { get; } = new();

    [ObservableProperty]
    private string? _coletadoEm;

    public void Recarregar()
    {
        Carregado = false;
        AoAtivar();
    }

    protected override async Task CarregarAsync()
    {
        await ExecutarAsync(async () =>
        {
            if (Principal.Inventario is null)
            {
                Principal.Inventario = await Principal.Servicos.Inventario.ColetarAsync();
            }

            var inv = Principal.Inventario;
            Preencher(inv);
        }, "Não foi possível ler o inventário.");
    }

    [RelayCommand]
    private async Task AtualizarAsync()
    {
        Principal.Inventario = null;
        await CarregarAsync();
    }

    private void Preencher(InventarioSistema inv)
    {
        ColetadoEm = "Lido em " + FormatacaoBr.DataHora(inv.ColetadoEm.LocalDateTime);

        Substituir(CamposSistema,
            LinhaFactory.De("Sistema operacional", inv.Sistema.Nome),
            LinhaFactory.De("Versão", inv.Sistema.Versao),
            LinhaFactory.De("Build", inv.Sistema.Build),
            LinhaFactory.De("Arquitetura", inv.Sistema.Arquitetura),
            LinhaFactory.De("Computador", inv.Sistema.NomeComputador),
            LinhaFactory.De("Usuário registrado", inv.Sistema.Usuario),
            LinhaFactory.De("Instalação", inv.Sistema.DataInstalacao, FormatacaoBr.Data),
            LinhaFactory.De("Ativação (informativa)", inv.Sistema.Ativacao));

        Substituir(CamposBios,
            LinhaFactory.De("BIOS — fabricante", inv.Bios.Fabricante),
            LinhaFactory.De("BIOS — versão", inv.Bios.Versao),
            LinhaFactory.De("BIOS — data", inv.Bios.Data, FormatacaoBr.Data),
            LinhaFactory.De("Modo de inicialização", inv.Bios.Modo),
            LinhaFactory.De("Placa-mãe — fabricante", inv.PlacaMae.Fabricante),
            LinhaFactory.De("Placa-mãe — modelo", inv.PlacaMae.Modelo),
            LinhaFactory.De("Placa-mãe — versão", inv.PlacaMae.Versao),
            LinhaFactory.De("Placa-mãe — número de série", inv.PlacaMae.NumeroSerie));

        Substituir(CamposCpu,
            LinhaFactory.De("Processador", inv.Cpu.Nome),
            LinhaFactory.De("Fabricante", inv.Cpu.Fabricante),
            LinhaFactory.De("Núcleos físicos", inv.Cpu.NucleosFisicos),
            LinhaFactory.De("Threads lógicas", inv.Cpu.ThreadsLogicas),
            LinhaFactory.De("Clock base", inv.Cpu.ClockBaseMhz, FormatacaoBr.Mhz),
            LinhaFactory.De("Clock máximo", inv.Cpu.ClockMaximoMhz, FormatacaoBr.Mhz),
            LinhaFactory.De("Cache L2", inv.Cpu.CacheL2Kb, v => v.ToString("N0", FormatacaoBr.Cultura) + " KB"),
            LinhaFactory.De("Cache L3", inv.Cpu.CacheL3Kb, v => v.ToString("N0", FormatacaoBr.Cultura) + " KB"),
            LinhaFactory.De("Arquitetura", inv.Cpu.Arquitetura));

        Substituir(CamposMemoria,
            LinhaFactory.De("Memória total", inv.Memoria.TotalBytes, b => FormatacaoBr.Gib(b / 1024.0 / 1024 / 1024)),
            LinhaFactory.De("Slots em uso", inv.Memoria.SlotsUsados),
            LinhaFactory.De("Slots totais", inv.Memoria.SlotsTotais),
            LinhaFactory.De("Timings (CAS/RAS/CL)", inv.Memoria.Timings));

        Substituir(Modulos, inv.Memoria.Modulos);

        Substituir(CamposCadaGpu, inv.Gpus.SelectMany(g => new[]
        {
            LinhaFactory.De("GPU", g.Nome),
            LinhaFactory.De("VRAM dedicada", g.VramDedicadaBytes, b => FormatacaoBr.Bytes(b)),
            LinhaFactory.De("Driver", g.VersaoDriver),
            LinhaFactory.De("Data do driver", g.DataDriver, FormatacaoBr.Data),
            LinhaFactory.De("Fabricante", g.Fabricante),
        }).ToArray());

        Substituir(CamposCadaTela, inv.Monitores.SelectMany(m => new[]
        {
            LinhaFactory.De("Monitor", m.Nome),
            LinhaCampoExt.Resumo("Resolução",
                m.Largura.Disponivel && m.Altura.Disponivel ? $"{m.Largura.Valor} × {m.Altura.Valor}" : null,
                m.Largura.Causa ?? m.Altura.Causa),
            LinhaFactory.De("Taxa de atualização", m.TaxaAtualizacaoHz, v => v + " Hz"),
        }).ToArray());

        Substituir(CamposRede, inv.Adaptadores.SelectMany(a => new[]
        {
            LinhaFactory.De("Adaptador", a.Nome),
            LinhaFactory.De("Descrição", a.Descricao),
            LinhaFactory.De("Endereço físico (MAC)", a.Mac),
            LinhaFactory.De("Velocidade do link", a.VelocidadeBitsPorSegundo, FormatacaoBr.VelocidadeLink),
            LinhaFactory.De("Tipo", a.Tipo),
            new LinhaCampo("Estado", a.Ativo ? "Conectado" : "Desconectado"),
        }).ToArray());

        Substituir(CamposBateria,
            new LinhaCampo("Bateria", inv.Bateria.Presente ? "Presente" : "Não há bateria (ou removida)"),
            LinhaFactory.De("Nome", inv.Bateria.Nome),
            LinhaFactory.De("Fabricante", inv.Bateria.Fabricante));

        Substituir(Dispositivos, inv.DispositivosComProblema);

        Substituir(Limitacoes, inv.Limitacoes.Select(l => $"{l.Item}: {l.Motivo}").ToArray());
    }

    private static void Substituir<T>(ObservableCollection<T> colecao, params T[] itens)
    {
        colecao.Clear();
        foreach (var i in itens)
        {
            colecao.Add(i);
        }
    }

    private static void Substituir<T>(ObservableCollection<T> colecao, IEnumerable<T> itens) => Substituir(colecao, itens.ToArray());
}

public static class LinhaCampoExt
{
    public static LinhaCampo Resumo(string rotulo, string? valor, string? causa) =>
        valor is null ? new LinhaCampo(rotulo, FormatacaoBr.NaoDisponivel(causa), causa ?? "Não disponível") : new LinhaCampo(rotulo, valor);
}
