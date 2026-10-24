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

    /// <summary>
    /// Temperatura a partir da qual o gráfico de stress destaca a faixa de alerta. Não encerra o
    /// teste: quem decide parar é o técnico. Editada como texto para dar mensagem de erro clara
    /// em vez de aceitar silenciosamente um valor absurdo.
    /// </summary>
    [ObservableProperty]
    private string _limiarAlertaTexto = "95";

    [ObservableProperty]
    private bool _limiarAlertaValido = true;

    /// <summary>Temperatura de confiança alta que faz o stress parar sozinho.</summary>
    [ObservableProperty]
    private string _limiteParadaTexto = "100";

    /// <summary>Teto de duração do stress, em minutos (1 a 120).</summary>
    [ObservableProperty]
    private string _tetoDuracaoTexto = "30";

    [ObservableProperty]
    private string? _statusDump;

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
        LimiarAlertaTexto = configuracao.LimiarAlertaTemperaturaCelsius.ToString("0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        LimiteParadaTexto = configuracao.LimiteParadaAutomaticaCelsius.ToString("0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        TetoDuracaoTexto = configuracao.TetoDuracaoStressMinutos.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
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

        if (!ValidarLimiarAlerta(out var limiar, out var erroLimiar))
        {
            Erro = erroLimiar;
            Status = null;
            return;
        }

        configuracao.LimiarAlertaTemperaturaCelsius = limiar;

        if (!ValidarLimiteParada(out var limiteParada, out var erroParada))
        {
            Erro = erroParada;
            Status = null;
            return;
        }

        if (!ValidarTetoDuracao(out var teto, out var erroTeto))
        {
            Erro = erroTeto;
            Status = null;
            return;
        }

        configuracao.LimiteParadaAutomaticaCelsius = limiteParada;
        configuracao.TetoDuracaoStressMinutos = teto;

        var aviso = Services.ConfiguracaoStore.Salvar(configuracao);
        Status = aviso ?? "Configurações salvas.";
        Erro = aviso;
    }

    /// <summary>
    /// O limiar precisa estar dentro da faixa de temperatura utilizável (1–120 °C). Fora disso o
    /// destaque nunca apareceria ou apareceria sempre, e o técnico pensaria que o gráfico falhou.
    /// </summary>
    private bool ValidarLimiarAlerta(out double limiar, out string? erro)
    {
        erro = null;
        limiar = 95;

        var texto = LimiarAlertaTexto?.Trim().Replace('.', ',');
        if (!double.TryParse(
                texto,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
                out var valor))
        {
            LimiarAlertaValido = false;
            erro = "O limiar de alerta precisa ser um número (ex.: 95).";
            return false;
        }

        if (valor < 1 || valor > 120)
        {
            LimiarAlertaValido = false;
            erro = "O limiar de alerta precisa estar entre 1 e 120 °C.";
            return false;
        }

        LimiarAlertaValido = true;
        limiar = valor;
        return true;
    }

    /// <summary>
    /// Teto máximo de duração, o mesmo aplicado por <c>CargaCpuRunner.DuracaoMaximaPermitida</c>.
    /// </summary>
    public const int MaximoTetoDuracaoMinutos = 60;

    /// <summary>
    /// O limite de parada precisa ser maior que o limiar de alerta: parar abaixo do aviso faria o
    /// alerta nunca aparecer, e acima de 120 °C não protege nada.
    /// </summary>
    private bool ValidarLimiteParada(out double limite, out string? erro)
    {
        erro = null;
        limite = 100;
        var texto = LimiteParadaTexto?.Trim().Replace('.', ',');
        if (!double.TryParse(texto, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out var valor))
        {
            erro = "O limite de parada automática precisa ser um número (ex.: 100).";
            return false;
        }

        if (valor < 60 || valor > 120)
        {
            erro = "O limite de parada automática precisa estar entre 60 e 120 °C.";
            return false;
        }

        limite = valor;
        return true;
    }

    private bool ValidarTetoDuracao(out int minutos, out string? erro)
    {
        erro = null;
        minutos = 30;
        var texto = TetoDuracaoTexto?.Trim();
        if (!int.TryParse(texto, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out var valor))
        {
            erro = "O teto de duração precisa ser um número inteiro de minutos (ex.: 30).";
            return false;
        }

        // O limite da tela precisa ser o mesmo que o runner aplica; senão o técnico configura 90
        // minutos, o teste para em 60 e a mensagem culpa o tempo, não a divergência.
        if (valor < 1 || valor > MaximoTetoDuracaoMinutos)
        {
            erro = $"O teto de duração precisa estar entre 1 e {MaximoTetoDuracaoMinutos} minutos.";
            return false;
        }

        minutos = valor;
        return true;
    }

    /// <summary>Grava o diagnóstico técnico dos sensores (.json e .txt) na pasta de laudos.</summary>
    [RelayCommand]
    private async Task ExportarDiagnosticoSensoresAsync()
    {
        StatusDump = "Gerando diagnóstico…";
        try
        {
            var pasta = string.IsNullOrWhiteSpace(PastaExportacao)
                ? Services.ConfiguracaoStore.PastaDados
                : PastaExportacao.Trim();

            var dump = new Services.DiagnosticoSensoresDump(Principal.Servicos);
            var (json, textoArquivo) = await dump.ExportarAsync(pasta, MascararSeriais);

            StatusDump = "Gravado: " + json + " e " + System.IO.Path.GetFileName(textoArquivo);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusDump = "Não foi possível gravar o diagnóstico: " + ex.Message;
        }
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
