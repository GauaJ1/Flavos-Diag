using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FlavosDiag.App.ViewModels;

// Páginas implementadas nas fases seguintes. Existem com estado "sem dados" explícito —
// nunca exibem dados fictícios nem ações sem efeito.

public sealed partial class DiscoViewModel : PaginaBase
{
    public DiscoViewModel(MainViewModel principal) : base(principal, "Disco", "IconeDisco")
    {
    }

    public ObservableCollection<object> Discos { get; } = new();

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => Carregado = false;

    protected override Task CarregarAsync()
    {
        Status = "Execute o diagnóstico rápido para ler a saúde dos discos.";
        return Task.CompletedTask;
    }
}

public sealed partial class MemoriaViewModel : PaginaBase
{
    public MemoriaViewModel(MainViewModel principal) : base(principal, "Memória", "IconeMemoria")
    {
    }

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => Carregado = false;

    protected override Task CarregarAsync()
    {
        Status = "Inventário de memória disponível na aba Sistema após o diagnóstico.";
        return Task.CompletedTask;
    }
}

public sealed partial class CpuViewModel : PaginaBase
{
    public CpuViewModel(MainViewModel principal) : base(principal, "CPU", "IconeCpu")
    {
    }

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => Carregado = false;

    protected override Task CarregarAsync()
    {
        Status = "Sensores e testes de CPU serão habilitados na Fase 3.";
        return Task.CompletedTask;
    }
}

public sealed partial class GpuViewModel : PaginaBase
{
    public GpuViewModel(MainViewModel principal) : base(principal, "GPU", "IconeGpu")
    {
    }

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => Carregado = false;

    protected override Task CarregarAsync()
    {
        Status = "Inventário de GPU disponível na aba Sistema.";
        return Task.CompletedTask;
    }
}

public sealed partial class BateriaViewModel : PaginaBase
{
    public BateriaViewModel(MainViewModel principal) : base(principal, "Bateria", "IconeBateria")
    {
    }

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => Carregado = false;

    protected override Task CarregarAsync()
    {
        Status = "A leitura detalhada da bateria entra na Fase 2.";
        return Task.CompletedTask;
    }
}

public sealed partial class RedeViewModel : PaginaBase
{
    public RedeViewModel(MainViewModel principal) : base(principal, "Rede", "IconeRede")
    {
    }

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => Carregado = false;

    protected override Task CarregarAsync()
    {
        Status = "Testes de rede entram na Fase 2.";
        return Task.CompletedTask;
    }
}

public sealed partial class ChecklistViewModel : PaginaBase
{
    public ChecklistViewModel(MainViewModel principal) : base(principal, "Checklist", "IconeChecklist")
    {
    }

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => Carregado = false;

    protected override Task CarregarAsync()
    {
        Status = "O checklist assistido entra na Fase 5.";
        return Task.CompletedTask;
    }
}

public sealed partial class ConfigViewModel : PaginaBase
{
    public ConfigViewModel(MainViewModel principal) : base(principal, "Configurações", "IconeConfig")
    {
    }

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => Carregado = false;

    protected override Task CarregarAsync()
    {
        Status = "Configurações completas entram na Fase 5.";
        return Task.CompletedTask;
    }
}
