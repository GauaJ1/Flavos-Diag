using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Monitor;

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
    private string _limiarAlertaTexto = TextoNumero(LimitesTermicos.AlertaVisualCelsius);

    [ObservableProperty]
    private bool _limiarAlertaValido = true;

    /// <summary>Temperatura de confiança alta que faz o stress parar sozinho.</summary>
    [ObservableProperty]
    private string _limiteParadaTexto = TextoNumero(LimitesTermicos.ParadaAutomaticaCelsius);

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

        if (!ValidarLimitesTermicos(out var limiar, out var limiteParada, out var erroLimites))
        {
            Erro = erroLimites;
            Status = null;
            return;
        }

        configuracao.LimiarAlertaTemperaturaCelsius = limiar;

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
    /// <remarks>
    /// A faixa e a relação com os outros limites vêm de <see cref="LimitesTermicos"/>. A tela valida
    /// os dois campos de uma vez porque as invariantes são <b>entre</b> eles: um alerta de 96 °C é
    /// aceitável ou não dependendo da parada escolhida ao lado.
    /// </remarks>
    private bool ValidarLimitesTermicos(out double alerta, out double parada, out string? erro)
    {
        erro = null;
        alerta = LimitesTermicos.AlertaVisualCelsius;
        parada = LimitesTermicos.ParadaAutomaticaCelsius;
        LimiarAlertaValido = false;

        if (!LerNumero(LimiarAlertaTexto, out var valorAlerta))
        {
            erro = "O limiar de alerta precisa ser um número (ex.: 95).";
            return false;
        }

        if (!LerNumero(LimiteParadaTexto, out var valorParada))
        {
            erro = "O limite de parada automática precisa ser um número (ex.: 95).";
            return false;
        }

        var validado = LimitesTermicos.Validar(
            LimitesTermicos.VereditoCpuCelsius,
            valorAlerta,
            valorParada);

        if (validado.Corrigido)
        {
            // Recusa em vez de aceitar o número corrigido em silêncio: quem digitou precisa saber
            // que o valor na tela não é o que vai valer depois de salvar.
            erro = validado.MotivosTexto;
            return false;
        }

        LimiarAlertaValido = true;
        LimiteParadaValido = true;
        alerta = validado.AlertaVisualCelsius;
        parada = validado.ParadaAutomaticaCelsius;
        return true;
    }

    [ObservableProperty]
    private bool _limiteParadaValido = true;

    private static bool LerNumero(string? texto, out double valor) =>
        double.TryParse(
            texto?.Trim().Replace('.', ','),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
            out valor);

    private static string TextoNumero(double valor) =>
        valor.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    /// <summary>
    /// O teto máximo de duração, o mesmo aplicado por <c>CargaCpuRunner.DuracaoMaximaPermitida</c>.
    /// </summary>
    public static int MaximoTetoDuracaoMinutos => (int)FlavosDiag.Workloads.CargaCpuRunner.DuracaoMaximaPermitida.TotalMinutes;

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
