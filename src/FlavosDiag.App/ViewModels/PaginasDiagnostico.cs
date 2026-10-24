using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;
using FlavosDiag.Workloads;

namespace FlavosDiag.App.ViewModels;

public sealed partial class MemoriaViewModel : PaginaBase
{
    public MemoriaViewModel(MainViewModel principal) : base(principal, "Memória", "IconeMemoria")
    {
    }

    public ObservableCollection<LinhaInfo> Linhas { get; } = new();

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private string? _vereditoTexto;

    [ObservableProperty]
    private bool _testando;

    [ObservableProperty]
    private double _progresso;

    public void Recarregar() => _ = CarregarAsync(recarregar: true);

    protected override Task CarregarAsync() => CarregarAsync(recarregar: false);

    private async Task CarregarAsync(bool recarregar)
    {
        await ExecutarAsync(async () =>
        {
            if (recarregar)
            {
                Principal.Inventario = await Principal.Servicos.Inventario.ColetarAsync();
            }
            else if (Principal.Inventario is null)
            {
                Principal.Inventario = await Principal.Servicos.Inventario.ColetarAsync();
            }

            Montar();
        }, "Não foi possível ler o inventário de memória.");
    }

    private void Montar()
    {
        Linhas.Clear();

        if (Principal.Inventario?.Memoria is { } memoria)
        {
            Linhas.Add(LinhaInfo.De("Total instalado", memoria.TotalBytes, FormatacaoBr.Bytes));
            if (memoria.SlotsUsados.Disponivel && memoria.SlotsTotais.Disponivel)
            {
                Linhas.Add(new LinhaInfo("Slots ocupados", $"{memoria.SlotsUsados.Valor} de {memoria.SlotsTotais.Valor}"));
            }

            Linhas.Add(LinhaInfo.De("Timings", memoria.Timings));

            foreach (var modulo in memoria.Modulos)
            {
                var rotulo = modulo.Local.Disponivel && !string.IsNullOrWhiteSpace(modulo.Local.Valor) ? modulo.Local.Valor! : "Módulo";
                Linhas.Add(new LinhaInfo(rotulo, DescreverModulo(modulo),
                    modulo.CapacidadeBytes.Disponivel ? null : modulo.CapacidadeBytes.Causa));
            }
        }

        var teste = Principal.TesteMemoria;
        if (teste is not null)
        {
            Linhas.Add(new LinhaInfo("Teste — bytes verificados",
                teste.BytesTestados > 0 ? FormatacaoBr.Bytes(teste.BytesTestados) : "Nenhum"));
            Linhas.Add(new LinhaInfo("Teste — padrões aplicados",
                teste.Padroes.Count == 0 ? "Nenhum" : $"{teste.Padroes.Count}: " + string.Join(", ", teste.Padroes)));
            Linhas.Add(new LinhaInfo("Teste — divergências", teste.ErrosDetectados.ToString()));
            Linhas.Add(new LinhaInfo("Teste — cobertura", $"{teste.CoberturaPercentual:0.0} % da memória livre"));
            if (teste.PrimeiroDeslocamentoErro is { } deslocamento)
            {
                Linhas.Add(new LinhaInfo("Primeira divergência", "deslocamento " + deslocamento));
            }

            if (!string.IsNullOrWhiteSpace(teste.AvisoTexto))
            {
                Linhas.Add(new LinhaInfo("Aviso", teste.AvisoTexto!));
            }

            VereditoTexto = teste.VereditoMemoria.Disponivel
                ? teste.VereditoMemoria.Valor.Rotulo()
                : FormatacaoBr.NaoDisponivel(teste.VereditoMemoria.Causa);
        }

        Status = teste is null
            ? "Nenhum teste de memória foi executado nesta sessão. O teste rápido verifica se os pentes retêm os dados gravados."
            : "Resultado do último teste desta sessão.";
    }

    private static string DescreverModulo(ModuloMemoria modulo)
    {
        var partes = new List<string>();
        if (modulo.CapacidadeBytes.Disponivel)
        {
            partes.Add(FormatacaoBr.Bytes(modulo.CapacidadeBytes.Valor));
        }

        if (modulo.Tipo.Disponivel)
        {
            partes.Add(modulo.Tipo.Valor ?? "tipo não informado");
        }

        if (modulo.VelocidadeConfiguradaMhz.Disponivel)
        {
            partes.Add(modulo.VelocidadeConfiguradaMhz.Valor + " MHz");
        }
        else if (modulo.VelocidadeNominalMhz.Disponivel)
        {
            partes.Add(modulo.VelocidadeNominalMhz.Valor + " MHz (nominal)");
        }

        if (modulo.Fabricante.Disponivel)
        {
            partes.Add(modulo.Fabricante.Valor ?? "fabricante não informado");
        }

        return partes.Count == 0 ? "Sem dados" : string.Join(" · ", partes);
    }

    /// <summary>
    /// Executa o teste rápido de memória. Pede confirmação: reserva memória e a máquina pode ficar mais lenta.
    /// </summary>
    [RelayCommand]
    private async Task Testar()
    {
        if (Testando)
        {
            return;
        }

        if (!Principal.Confirmar(
                "Teste de memória",
                "O teste reserva uma parte da memória livre e verifica se os dados gravados são lidos corretamente.\n\n" +
                "Durante o teste o computador pode ficar mais lento. Ele pode ser interrompido a qualquer momento.\n\n" +
                "Deseja iniciar?"))
        {
            return;
        }

        Testando = true;
        Progresso = 0;
        try
        {
            var progresso = new Progress<double>(p => Progresso = Math.Clamp(p * 100, 0, 100));
            var resultado = await Task.Run(() => Principal.Servicos.Memoria.TestarAsync(0, progresso, CancellationToken.None));
            Principal.TesteMemoria = resultado;
            Montar();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Erro = "Não foi possível concluir o teste de memória. Detalhe: " + ex.Message;
        }
        finally
        {
            Testando = false;
        }
    }
}

public sealed partial class CpuViewModel : PaginaBase
{
    public CpuViewModel(MainViewModel principal) : base(principal, "CPU", "IconeCpu")
    {
    }

    public ObservableCollection<LinhaInfo> Linhas { get; } = new();

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private string? _temperaturaAgora;

    [ObservableProperty]
    private bool _estressando;

    [ObservableProperty]
    private string? _textoEstresse;

    public void Recarregar() => _ = CarregarAsync(recarregar: true);

    protected override Task CarregarAsync() => CarregarAsync(recarregar: false);

    private async Task CarregarAsync(bool recarregar)
    {
        await ExecutarAsync(async () =>
        {
            if (recarregar || Principal.Inventario is null)
            {
                Principal.Inventario = await Principal.Servicos.Inventario.ColetarAsync();
            }

            Montar();
        }, "Não foi possível ler os dados do processador.");
    }

    private void Montar()
    {
        Linhas.Clear();
        if (Principal.Inventario?.Cpu is { } cpu)
        {
            Linhas.Add(LinhaInfo.De("Modelo", cpu.Nome));
            Linhas.Add(LinhaInfo.De("Fabricante", cpu.Fabricante));
            Linhas.Add(new LinhaInfo("Núcleos / threads",
                cpu.NucleosFisicos.Disponivel && cpu.ThreadsLogicas.Disponivel
                    ? $"{cpu.NucleosFisicos.Valor} físicos / {cpu.ThreadsLogicas.Valor} lógicos"
                    : FormatacaoBr.NaoDisponivel(cpu.NucleosFisicos.Causa ?? cpu.ThreadsLogicas.Causa),
                cpu.NucleosFisicos.Disponivel ? null : cpu.NucleosFisicos.Causa));
            Linhas.Add(LinhaInfo.De("Clock base", cpu.ClockBaseMhz, FormatacaoBr.Mhz));
            Linhas.Add(LinhaInfo.De("Clock máximo", cpu.ClockMaximoMhz, FormatacaoBr.Mhz));
        }

        if (Principal.EstresseCpu is { } estresse)
        {
            Linhas.Add(new LinhaInfo("Último estresse — motivo", estresse.MotivoParadaTexto ?? estresse.MotivoParada.ToString()));
            Linhas.Add(new LinhaInfo("Último estresse — duração", FormatacaoBr.Duracao(estresse.DuracaoReal)));
            Linhas.Add(LinhaInfo.De("Temperatura máxima", estresse.TemperaturaMaximaCelsius, v => $"{v:0} °C"));
        }

        if (Principal.BenchmarkCpu is { } benchmark)
        {
            Linhas.Add(new LinhaInfo("Benchmark — um núcleo", $"{benchmark.PontuacaoSingle:0.0} pontos"));
            Linhas.Add(new LinhaInfo("Benchmark — todos os núcleos", $"{benchmark.PontuacaoMulti:0.0} pontos"));
            Linhas.Add(new LinhaInfo("Benchmark — aviso", "Pontuação própria do Flavos Diag. Não é comparável com outros programas."));
        }

        var ultima = Principal.AmostrasSensores.Count > 0 ? Principal.AmostrasSensores[^1] : null;
        var temperatura = ultima is null ? null : CargaCpuRunner.TemperaturaDaCpu(ultima);
        TemperaturaAgora = temperatura is null
            ? "Sem leitura de temperatura"
            : $"{temperatura:0} °C";

        Status = Principal.Inventario?.Cpu is null
            ? "Os dados do processador aparecem após a leitura do sistema."
            : "Carga de CPU: mantém o processador ocupado e monitora a temperatura.";
    }

    [RelayCommand]
    private async Task Estressar()
    {
        if (Estressando)
        {
            return;
        }

        var minutos = 5;
        if (!Principal.Confirmar(
                "Teste de estresse da CPU",
                $"O processador será mantido sob carga total por até {minutos} minutos.\n\n" +
                "O teste para sozinho ao atingir 90 °C. Se não houver leitura de temperatura, para em 2 minutos.\n\n" +
                "Use apenas com o equipamento em superfície ventilada. Deseja continuar?"))
        {
            return;
        }

        Estressando = true;
        try
        {
            var progresso = new Progress<ProgressoEstresse>(p =>
            {
                var decorrido = $"{(int)p.Decorrido.TotalMinutes}:{p.Decorrido.Seconds:00}";
                TextoEstresse = p.TemperaturaCelsius is { } t
                    ? $"Temperatura agora: {t:0} °C · decorrido {decorrido}"
                    : $"Sem leitura de temperatura · decorrido {decorrido}";
            });

            // Assinatura real: (configuração, núcleos, threads, leitura de sensores, progresso, cancelamento).
            var nucleos = Principal.Inventario?.Cpu.NucleosFisicos.Disponivel == true
                ? Principal.Inventario.Cpu.NucleosFisicos.Valor
                : Environment.ProcessorCount;
            var threads = Principal.Inventario?.Cpu.ThreadsLogicas.Disponivel == true
                ? Principal.Inventario.Cpu.ThreadsLogicas.Valor
                : Environment.ProcessorCount;

            Func<AmostraSensores?> leituraSensores = () => Principal.AmostrasSensores.Count > 0
                ? Principal.AmostrasSensores[^1]
                : null;

            var resultado = await Principal.Servicos.CargaCpu.EstressarAsync(
                new ConfiguracaoEstresse
                {
                    Perfil = PerfilCarga.Pesado,
                    DuracaoMaxima = TimeSpan.FromMinutes(minutos),
                    LimiteTemperaturaCelsius = 90,
                },
                nucleos,
                threads,
                leituraSensores,
                progresso,
                CancellationToken.None);

            Principal.EstresseCpu = resultado;
            TextoEstresse = resultado.MotivoParadaTexto;
            Montar();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Erro = "Não foi possível concluir o teste de estresse. Detalhe: " + ex.Message;
        }
        finally
        {
            Estressando = false;
        }
    }

    [RelayCommand]
    private async Task Benchmarquar()
    {
        await ExecutarAsync(async () =>
        {
            var benchmark = await Principal.Servicos.CargaCpu.BenchmarkAsync(
                threads: Environment.ProcessorCount,
                progresso: null,
                cancelamento: CancellationToken.None);
            Principal.BenchmarkCpu = benchmark;
            Montar();
        }, "Não foi possível concluir o benchmark.");
    }
}

public sealed partial class GpuViewModel : PaginaBase
{
    public GpuViewModel(MainViewModel principal) : base(principal, "GPU", "IconeGpu")
    {
    }

    public ObservableCollection<LinhaInfo> Linhas { get; } = new();

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _podeTestar = true;

    [ObservableProperty]
    private string? _textoTeste;

    public void Recarregar() => _ = CarregarAsync(recarregar: true);

    protected override Task CarregarAsync() => CarregarAsync(recarregar: false);

    private async Task CarregarAsync(bool recarregar)
    {
        await ExecutarAsync(async () =>
        {
            if (recarregar || Principal.Inventario is null)
            {
                Principal.Inventario = await Principal.Servicos.Inventario.ColetarAsync();
            }

            Montar();
        }, "Não foi possível ler os dados da placa de vídeo.");
    }

    private void Montar()
    {
        Linhas.Clear();

        foreach (var gpu in Principal.Inventario?.Gpus ?? Array.Empty<InfoGpu>())
        {
            Linhas.Add(LinhaInfo.De("Adaptador", gpu.Nome));
            Linhas.Add(LinhaInfo.De("Memória de vídeo", gpu.VramDedicadaBytes, FormatacaoBr.Bytes));
            Linhas.Add(LinhaInfo.De("Driver", gpu.VersaoDriver));
            Linhas.Add(LinhaInfo.De("Data do driver", gpu.DataDriver, FormatacaoBr.Data));
        }

        if (Principal.ResultadoGpu is { } resultado)
        {
            Linhas.Add(new LinhaInfo("Prova — adaptador usado", resultado.Adaptador));
            if (resultado.Estresse is { } estresse)
            {
                Linhas.Add(new LinhaInfo("Prova — motivo da parada", estresse.MotivoParadaTexto ?? estresse.MotivoParada.ToString()));
                Linhas.Add(new LinhaInfo("Prova — quadros processados", estresse.CiclosTotais.ToString()));
                Linhas.Add(new LinhaInfo("Prova — divergências de pixel", estresse.ErrosDeCalculo.ToString()));
                Linhas.Add(LinhaInfo.De("Prova — temperatura máxima", estresse.TemperaturaMaximaCelsius, v => $"{v:0} °C"));
            }

            if (!string.IsNullOrWhiteSpace(resultado.CausaAusencia))
            {
                Linhas.Add(new LinhaInfo("Prova — não executada", resultado.CausaAusencia!, "sem teste"));
            }

            if (resultado.Benchmark is { } benchmark)
            {
                Linhas.Add(new LinhaInfo("Pontuação", $"{benchmark.Pontuacao:0.0} pontos"));
                Linhas.Add(new LinhaInfo("Aviso", benchmark.TextoAviso));
            }
        }

        Status = Principal.ResultadoGpu is null
            ? "Nenhuma prova de GPU foi executada nesta sessão."
            : "Resultado da última prova desta sessão.";
    }

    [RelayCommand]
    private async Task Testar()
    {
        if (!PodeTestar)
        {
            return;
        }

        if (!Principal.Confirmar(
                "Prova de GPU",
                "Será executada uma carga curta na placa de vídeo, sem janela visível.\n\n" +
                "A temperatura é monitorada quando o equipamento tem sensor; sem sensor a prova para em 2 minutos.\n\n" +
                "Deseja continuar?"))
        {
            return;
        }

        PodeTestar = false;
        try
        {
            var resultado = await Principal.Servicos.Gpu.TestarAsync(
                new ConfiguracaoEstresse
                {
                    Perfil = PerfilCarga.Medio,
                    DuracaoMaxima = TimeSpan.FromMinutes(1),
                    LimiteTemperaturaGpuCelsius = 85,
                },
                deveParar: () => false,
                progresso: null,
                cancelamento: CancellationToken.None);

            Principal.ResultadoGpu = resultado;
            TextoTeste = resultado.Estresse?.MotivoParadaTexto ?? resultado.CausaAusencia;
            Montar();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Erro = "Não foi possível concluir a prova de GPU. Detalhe: " + ex.Message;
        }
        finally
        {
            PodeTestar = true;
        }
    }
}

public sealed partial class BateriaViewModel : PaginaBase
{
    public BateriaViewModel(MainViewModel principal) : base(principal, "Bateria", "IconeBateria")
    {
    }

    public ObservableCollection<LinhaInfo> Linhas { get; } = new();

    [ObservableProperty]
    private string? _status;

    public void Recarregar() => _ = CarregarAsync(recarregar: true);

    protected override Task CarregarAsync() => CarregarAsync(recarregar: false);

    private async Task CarregarAsync(bool recarregar)
    {
        await ExecutarAsync(async () =>
        {
            Principal.Bateria = await Principal.Servicos.Bateria.LerAsync();
            Montar();
        }, "Não foi possível ler a bateria.");
    }

    private void Montar()
    {
        Linhas.Clear();
        if (Principal.Bateria is not { } bateria)
        {
            Status = "A bateria não foi lida.";
            return;
        }

        if (!bateria.Presente)
        {
            // Equipamento de mesa: ausência de bateria não é falha — o laudo registra "não se aplica".
            Status = "Este equipamento não tem bateria. A categoria fica \"Não se aplica\" no laudo.";
            return;
        }

        Linhas.Add(LinhaInfo.De("Nome", bateria.Nome));
        Linhas.Add(LinhaInfo.De("Fabricante", bateria.Fabricante));
        Linhas.Add(LinhaInfo.De("Número de série", bateria.NumeroSerie, s => Mascaramento.Mascarar(s)));
        Linhas.Add(LinhaInfo.De("Carga de projeto", bateria.CapacidadeProjetoMwh, v => v + " mWh"));
        Linhas.Add(LinhaInfo.De("Carga atual", bateria.CapacidadeAtualMwh, v => v + " mWh"));
        Linhas.Add(LinhaInfo.De("Saúde", bateria.SaudePercentual, v => $"{v:0} % da capacidade original"));
        Linhas.Add(LinhaInfo.De("Ciclos", bateria.Ciclos));
        Linhas.Add(LinhaInfo.De("Estado", bateria.Estado));

        foreach (var limitacao in bateria.Limitacoes)
        {
            Linhas.Add(new LinhaInfo("Limitação: " + limitacao.Item, limitacao.Motivo, "informação não coletada"));
        }

        Status = "Dados lidos do sistema.";
    }
}

public sealed partial class RedeViewModel : PaginaBase
{
    public RedeViewModel(MainViewModel principal) : base(principal, "Rede", "IconeRede")
    {
    }

    public ObservableCollection<LinhaInfo> Linhas { get; } = new();

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _testando;

    public void Recarregar() => _ = CarregarAsync(recarregar: true);

    protected override Task CarregarAsync() => CarregarAsync(recarregar: false);

    private async Task CarregarAsync(bool recarregar)
    {
        await ExecutarAsync(async () =>
        {
            var alvos = Principal.Configuracao.AlvosPing
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var dns = string.IsNullOrWhiteSpace(Principal.Configuracao.NomeDnsTeste)
                ? null
                : Principal.Configuracao.NomeDnsTeste;

            Principal.Rede = await Principal.Servicos.Rede.LerAsync(alvos, dns);
            Montar();
        }, "Não foi possível ler os dados de rede.");
    }

    private void Montar()
    {
        Linhas.Clear();
        if (Principal.Rede is not { } rede)
        {
            Status = "A rede não foi lida.";
            return;
        }

        foreach (var adaptador in rede.Adaptadores)
        {
            var rotulo = adaptador.Descricao.Disponivel && !string.IsNullOrWhiteSpace(adaptador.Descricao.Valor)
                ? adaptador.Descricao.Valor!
                : (adaptador.Nome.Valor ?? "Adaptador");
            Linhas.Add(new LinhaInfo(rotulo,
                adaptador.Ativo ? "ativo" : "inativo",
                adaptador.Descricao.Disponivel ? null : adaptador.Descricao.Causa));
            Linhas.Add(LinhaInfo.De("  Endereço IP", adaptador.EnderecoIp));
            Linhas.Add(LinhaInfo.De("  Velocidade do link", adaptador.VelocidadeBitsPorSegundo, FormatacaoBr.VelocidadeLink));
            Linhas.Add(LinhaInfo.De("  MAC", adaptador.Mac, m => Mascaramento.Mascarar(m)));
        }

        Linhas.Add(LinhaInfo.De("Gateway", rede.Gateway));
        Linhas.Add(LinhaInfo.De("Servidores DNS", rede.ServidoresDns));

        if (rede.LatenciaGateway.Disponivel && rede.LatenciaGateway.Valor is { } latencia)
        {
            Linhas.Add(new LinhaInfo("Latência até o gateway", $"{latencia.LatenciaMediaMs:0.0} ms (perda {latencia.PerdaPercentual:0} %)"));
        }
        else
        {
            Linhas.Add(LinhaInfo.De("Latência até o gateway", rede.LatenciaGateway));
        }

        if (rede.Wifi.QualidadePercentual.Disponivel)
        {
            Linhas.Add(new LinhaInfo("Wi-Fi", $"{rede.Wifi.QualidadePercentual.Valor} % de qualidade"));
        }

        foreach (var alvo in rede.Alvos)
        {
            Linhas.Add(new LinhaInfo("Alvo " + alvo.Alvo,
                alvo.Recebidos == 0
                    ? "sem resposta"
                    : $"{alvo.LatenciaMediaMs:0.0} ms, perda {alvo.PerdaPercentual.ToString("0", FormatacaoBr.Cultura) + " %"}"));
        }

        if (rede.Dns.Disponivel && rede.Dns.Valor is { } dns)
        {
            Linhas.Add(new LinhaInfo("Resolução DNS",
                dns.Resolvido ? $"{dns.LatenciaMs:0} ms — {string.Join(", ", dns.Enderecos)}" : dns.Erro ?? "não resolvido"));
        }

        Status = $"{rede.Adaptadores.Count} adaptador(es) encontrado(s).";
    }

    [RelayCommand]
    private async Task Atualizar()
    {
        if (Testando)
        {
            return;
        }

        Testando = true;
        try
        {
            await CarregarAsync();
        }
        finally
        {
            Testando = false;
        }
    }
}

