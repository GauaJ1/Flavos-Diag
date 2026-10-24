using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.App.ViewModels;

/// <summary>
/// Checklist assistido (E10). Os itens são fixos e o técnico marca cada um conforme testa o equipamento.
/// O estado fica apenas na sessão: nada é gravado sem o técnico salvar o laudo.
/// </summary>
public sealed partial class ChecklistViewModel : PaginaBase
{
    private readonly List<ItemChecklistExibicao> _exibicoes = new();

    public ChecklistViewModel(MainViewModel principal) : base(principal, "Checklist", "IconeChecklist")
    {
    }

    /// <summary>Itens do checklist com o comando de marcação de cada um.</summary>
    public ObservableCollection<ItemChecklistExibicao> Itens { get; } = new();

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private string? _resumo;

    protected override Task CarregarAsync()
    {
        AtualizarItens();
        return Task.CompletedTask;
    }

    public void Recarregar() => AtualizarItens();

    /// <summary>
    /// Marca o status de um item. Quem decide o resultado de um teste manual é o técnico: o app registra
    /// a resposta e leva o item para o laudo, sem inferir nada.
    /// </summary>
    public void Marcar(ItemChecklistExibicao exibicao, StatusChecklist status)
    {
        var indice = Principal.Checklist.IndexOf(exibicao.Item);
        if (indice < 0)
        {
            return;
        }

        Principal.Checklist[indice] = exibicao.Item with { Status = status };
        AtualizarItens();
    }

    public void Observar(ItemChecklistExibicao exibicao, string? observacao)
    {
        var indice = Principal.Checklist.IndexOf(exibicao.Item);
        if (indice >= 0)
        {
            Principal.Checklist[indice] = exibicao.Item with { Observacao = observacao };
        }
    }

    [RelayCommand]
    private void Zerar()
    {
        for (var i = 0; i < Principal.Checklist.Count; i++)
        {
            Principal.Checklist[i] = Principal.Checklist[i] with { Status = StatusChecklist.Pendente, Observacao = null };
        }

        AtualizarItens();
    }

    private void AtualizarItens()
    {
        Itens.Clear();
        _exibicoes.Clear();
        foreach (var item in Principal.Checklist)
        {
            var exibicao = new ItemChecklistExibicao(item, Marcar);
            _exibicoes.Add(exibicao);
            Itens.Add(exibicao);
        }

        var total = Principal.Checklist.Count;
        var respondidos = Principal.Checklist.Count(i => i.Status != StatusChecklist.Pendente);
        var falhas = Principal.Checklist.Count(i => i.Status == StatusChecklist.Falha);

        Resumo = $"{respondidos} de {total} itens respondidos" + (falhas > 0 ? $", {falhas} com falha" : string.Empty);
        Status = respondidos == total
            ? "Todos os itens foram respondidos. O checklist entra no laudo."
            : "Marque cada item depois de testar o equipamento. Itens pendentes aparecem como tais no laudo.";
    }
}

/// <summary>Item do checklist pronto para exibição, com os comandos de marcação.</summary>
public sealed class ItemChecklistExibicao
{
    private readonly Action<ItemChecklistExibicao, StatusChecklist> _marcar;

    public ItemChecklistExibicao(ItemChecklist item, Action<ItemChecklistExibicao, StatusChecklist> marcar)
    {
        Item = item;
        _marcar = marcar;
        MarcarOkCommand = new RelayCommand(() => _marcar(this, StatusChecklist.Ok));
        MarcarFalhaCommand = new RelayCommand(() => _marcar(this, StatusChecklist.Falha));
        MarcarNaoSeAplicaCommand = new RelayCommand(() => _marcar(this, StatusChecklist.NaoSeAplica));
        LimparCommand = new RelayCommand(() => _marcar(this, StatusChecklist.Pendente));
    }

    public ItemChecklist Item { get; }

    public string StatusTexto => Item.Status switch
    {
        StatusChecklist.Ok => "OK",
        StatusChecklist.Falha => "Falha",
        StatusChecklist.NaoSeAplica => "Não se aplica",
        _ => "Pendente",
    };

    public IRelayCommand MarcarOkCommand { get; }

    public IRelayCommand MarcarFalhaCommand { get; }

    public IRelayCommand MarcarNaoSeAplicaCommand { get; }

    public IRelayCommand LimparCommand { get; }
}
/// <summary>
/// Configurações (E-identidade). Grava em `%LOCALAPPDATA%\FlavosDiag\config.json`; se a gravação falhar,
/// o motivo aparece na tela em vez de ser engolido.
/// </summary>
public sealed partial class ConfigViewModel : PaginaBase
{
    public ConfigViewModel(MainViewModel principal) : base(principal, "Configurações", "IconeConfig")
    {
    }

    [ObservableProperty]
    private string _nomeTecnico = string.Empty;

    [ObservableProperty]
    private string _empresa = string.Empty;

    [ObservableProperty]
    private string _cnpj = string.Empty;

    [ObservableProperty]
    private string _contato = string.Empty;

    [ObservableProperty]
    private string _servidorTesteRede = string.Empty;

    [ObservableProperty]
    private string _alvosPing = string.Empty;

    [ObservableProperty]
    private string _nomeDnsTeste = string.Empty;

    [ObservableProperty]
    private bool _mascararSeriais = true;

    [ObservableProperty]
    private string _pastaExportacao = string.Empty;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _temperaturaCpuValida = true;

    protected override Task CarregarAsync()
    {
        var configuracao = Principal.Configuracao;
        NomeTecnico = configuracao.NomeTecnico;
        Empresa = configuracao.Empresa;
        Cnpj = configuracao.Cnpj;
        Contato = configuracao.Contato;
        ServidorTesteRede = configuracao.ServidorTesteRede;
        AlvosPing = configuracao.AlvosPing;
        NomeDnsTeste = configuracao.NomeDnsTeste;
        MascararSeriais = configuracao.MascararSeriais;
        PastaExportacao = configuracao.PastaExportacao;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Salvar()
    {
        var configuracao = Principal.Configuracao;
        configuracao.NomeTecnico = NomeTecnico.Trim();
        configuracao.Empresa = Empresa.Trim();
        configuracao.Cnpj = Cnpj.Trim();
        configuracao.Contato = Contato.Trim();
        configuracao.ServidorTesteRede = ServidorTesteRede.Trim();
        configuracao.AlvosPing = AlvosPing.Trim();
        configuracao.NomeDnsTeste = NomeDnsTeste.Trim();
        configuracao.MascararSeriais = MascararSeriais;
        configuracao.PastaExportacao = PastaExportacao.Trim();

        var aviso = Services.ConfiguracaoStore.Salvar(configuracao);
        Status = aviso ?? "Configurações salvas.";
        Erro = aviso;
    }

    [RelayCommand]
    private void EscolherPasta()
    {
        var (ok, pasta) = Principal.EscolherPasta();
        if (ok && !string.IsNullOrWhiteSpace(pasta))
        {
            PastaExportacao = pasta!;
        }
    }
}
