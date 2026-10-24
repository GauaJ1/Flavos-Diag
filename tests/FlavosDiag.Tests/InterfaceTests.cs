using System.Windows;
using System.Windows.Input;
using FlavosDiag.App.Services;
using FlavosDiag.App.ViewModels;
using FlavosDiag.App.Views;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.Tests;

/// <summary>
/// Testes de interface que exercitam os comandos REAIS usados pela janela: o comando de cada item do
/// menu lateral, o atalho Ctrl+N, a troca de tema e o cancelamento do diagnóstico.
/// <para>
/// Testes que chamam <c>NavegarPara</c> diretamente NÃO cobrem o defeito encontrado no menu: o botão
/// do DataTemplate não passava <c>CommandParameter</c>, o comando genérico recebia <c>null</c> e
/// nenhum item navegava. Por isso aqui só se usa o que a janela realmente liga.
/// </para>
/// <para>
/// Nada aqui abre janela nem toca em hardware: os serviços são os de demonstração (dados fictícios) e
/// a coleta automática de página fica desligada via <see cref="MainViewModel.DemonstracaoDeTeste"/>.
/// </para>
/// </summary>
public static class InterfaceTests
{
    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos() => new (string, Func<Task>)[]
    {
        ("Interface: menu tem 10 itens, um por página de categoria", MenuTemDezItens),
        ("Interface: comando de cada item do menu navega para a sua página", CadaItemDoMenuNavega),
        ("Interface: comando do menu não recebe parâmetro e ainda assim navega", ComandoDoMenuNaoDependeDeParametro),
        ("Interface: item Configurações do rodapé navega e recebe destaque", ConfiguracoesNavega),
        ("Interface: só o item da página ativa fica selecionado", UmSelecionadoPorVez),
        ("Interface: ItensNav é criado uma vez e não recriado ao navegar", ItensNavNaoRecriado),
        ("Interface: Ctrl+N volta ao Resumo e limpa os resultados da sessão", AtalhoCtrlN),
        ("Interface: trocar de tema troca as chaves de cor e volta", TrocaDeTemaAtualizaCores),
        ("Interface: trocar de tema não deixa estilo de tema no dicionário escuro", TemaEscuroSoTemCores),
        ("Interface: todo item do menu tem ícone (não fica sem desenho)", TodoItemTemIcone),
        ("Interface: nenhuma cor fixa nas telas (tudo passa pelo tema)", NenhumaCorFixa),
        ("Interface: <Run> não faz binding TwoWay em propriedade somente leitura", RunSemBindingTwoWay),
        ("Interface: todas as páginas renderizam sem exceção de binding", TodasAsPaginasRenderizam),
        ("Interface: cancelar o diagnóstico interrompe e não vira Falha", CancelarDiagnostico),
    };

    /// <summary>
    /// Cria o estado da bancada em modo demonstração. A coleta automática de página fica desligada:
    /// os testes não precisam (nem devem) tocar em WMI, disco ou rede.
    /// </summary>
    private static (MainViewModel Vm, Servicos Servicos) Criar()
    {
        var servicos = Servicos.Criar(demonstracao: true);
        var configuracao = new Configuracao();
        var vm = new MainViewModel(servicos, configuracao, demonstracao: true)
        {
            DemonstracaoDeTeste = true,
        };
        return (vm, servicos);
    }

    private static void Verificar(bool condicao, string mensagem)
    {
        if (!condicao)
        {
            throw new InvalidOperationException(mensagem);
        }
    }

    private static void Iguais<T>(T esperado, T obtido, string mensagem)
    {
        if (!EqualityComparer<T>.Default.Equals(esperado, obtido))
        {
            throw new InvalidOperationException($"{mensagem} (esperado: {esperado}; obtido: {obtido})");
        }
    }

    private static async Task MenuTemDezItens()
    {
        var (vm, servicos) = Criar();
        using var _ = servicos;

        Iguais(10, vm.ItensNav.Count, "o menu deve ter um item para cada categoria");

        // Cada item aponta para uma página distinta das demais.
        var paginas = vm.ItensNav.Select(i => i.Pagina).ToList();
        Iguais(paginas.Count, paginas.Distinct().Count(), "não pode haver item repetido no menu");

        // E as páginas do menu são exatamente as de categoria (Configurações fica no rodapé).
        Verificar(!paginas.Contains(vm.ConfigVm), "Configurações não deve estar na lista de categorias");

        await Task.CompletedTask;
    }

    /// <summary>
    /// O teste central do defeito: executa o comando que o botão do menu executa e confere a página.
    /// </summary>
    private static async Task CadaItemDoMenuNavega()
    {
        var (vm, servicos) = Criar();
        using var _ = servicos;

        foreach (var item in vm.ItensNav.ToList())
        {
            var esperada = item.Pagina;

            // Volta ao Resumo antes de cada item, para a navegação ser sempre uma mudança real.
            vm.NavegarPara(vm.Resumo);
            Verificar(ReferenceEquals(vm.PaginaAtual, vm.Resumo), "a preparação deveria ter voltado ao Resumo");

            item.Comando.Execute(null);

            Verificar(
                ReferenceEquals(esperada, vm.PaginaAtual),
                $"o comando do item '{item.Rotulo}' deveria abrir a página '{esperada.Titulo}', mas abriu '{vm.PaginaAtual.Titulo}'");
            Iguais(esperada.Titulo, vm.TituloPagina, $"o título da janela deveria acompanhar '{item.Rotulo}'");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Garante que o comando funciona sem parâmetro — era exatamente o que faltava no XAML (o
    /// <c>RelayCommand&lt;PaginaBase&gt;</c> recebia <c>null</c>). Executar com um parâmetro qualquer
    /// também não deve quebrar nem navegar para o lugar errado.
    /// </summary>
    private static async Task ComandoDoMenuNaoDependeDeParametro()
    {
        var (vm, servicos) = Criar();
        using var _ = servicos;

        var item = vm.ItensNav.First(i => ReferenceEquals(i.Pagina, vm.DiscoVm));

        vm.NavegarPara(vm.Resumo);
        item.Comando.Execute(null);
        Verificar(ReferenceEquals(vm.DiscoVm, vm.PaginaAtual), "sem parâmetro o item Disco deve navegar");

        // Com um parâmetro estranho, continua indo para a própria página (o comando a captura).
        vm.NavegarPara(vm.Resumo);
        item.Comando.Execute(vm.CpuVm);
        Verificar(
            ReferenceEquals(vm.DiscoVm, vm.PaginaAtual),
            "o comando do item deve ignorar parâmetros e usar a página que capturou");

        // E o comando está sempre disponível: sem CanExecute o botão fica cinza e não clica.
        Verificar(item.Comando.CanExecute(null), "o comando do menu deve estar sempre habilitado");

        await Task.CompletedTask;
    }

    private static async Task ConfiguracoesNavega()
    {
        var (vm, servicos) = Criar();
        using var _ = servicos;

        Verificar(vm.ItemConfiguracoes is not null, "o item de Configurações deve existir no rodapé");
        var itemConfig = vm.ItemConfiguracoes!;
        Verificar(
            ReferenceEquals(vm.ConfigVm, itemConfig.Pagina),
            "o item de Configurações deve apontar para a página de configurações");

        itemConfig.Comando.Execute(null);

        Verificar(ReferenceEquals(vm.ConfigVm, vm.PaginaAtual), "o comando de Configurações deve abrir a página");
        Verificar(itemConfig.Selecionado, "Configurações ativa deve aparecer destacada no menu");
        Iguais("Configurações", vm.TituloPagina, "o título deve acompanhar a página de configurações");

        // O atalho F1 (AbrirConfiguracoesCommand) precisa levar ao mesmo lugar.
        vm.NavegarPara(vm.Resumo);
        vm.AbrirConfiguracoesCommand.Execute(null);
        Verificar(ReferenceEquals(vm.ConfigVm, vm.PaginaAtual), "F1 deve abrir Configurações");

        await Task.CompletedTask;
    }

    private static async Task UmSelecionadoPorVez()
    {
        var (vm, servicos) = Criar();
        using var _ = servicos;

        foreach (var item in vm.ItensNav.ToList())
        {
            item.Comando.Execute(null);

            var marcados = vm.ItensNav.Where(i => i.Selecionado).ToList();
            Iguais(1, marcados.Count, $"após abrir '{item.Rotulo}' deveria haver exatamente um item destacado");
            Verificar(ReferenceEquals(marcados[0], item), $"o destaque deveria estar em '{item.Rotulo}'");

            // Ao abrir uma categoria, o rodapé de Configurações sai do destaque.
            Verificar(!vm.ItemConfiguracoes.Selecionado, "Configurações não deveria estar destacada");
        }

        // E ao voltar para Configurações, todos os itens de categoria perdem o destaque.
        vm.ItemConfiguracoes.Comando.Execute(null);
        Verificar(vm.ItensNav.All(i => !i.Selecionado), "nenhum item de categoria deveria estar destacado");
        Verificar(vm.ItemConfiguracoes.Selecionado, "Configurações deveria estar destacada");

        await Task.CompletedTask;
    }

    /// <summary>
    /// O menu é montado no construtor. Recriar a coleção a cada navegação reconstruiria os botões e
    /// faria o menu perder o foco do teclado; a substituição é detectada aqui.
    /// </summary>
    private static async Task ItensNavNaoRecriado()
    {
        var (vm, servicos) = Criar();
        using var _ = servicos;

        var mesmosItens = vm.ItensNav.ToList();
        var mesmoItemConfig = vm.ItemConfiguracoes;

        foreach (var item in mesmosItens)
        {
            item.Comando.Execute(null);
        }

        vm.ItemConfiguracoes.Comando.Execute(null);

        Iguais(mesmosItens.Count, vm.ItensNav.Count, "a coleção do menu não pode ser recriada");
        for (var i = 0; i < mesmosItens.Count; i++)
        {
            Verificar(
                ReferenceEquals(mesmosItens[i], vm.ItensNav[i]),
                $"o item {i} do menu foi substituído durante a navegação (a coleção foi recriada)");
        }

        Verificar(
            ReferenceEquals(mesmoItemConfig, vm.ItemConfiguracoes),
            "o item de Configurações foi substituído durante a navegação");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Ctrl+N: começa atendimento novo. Precisa voltar ao Resumo e limpar os resultados da sessão,
    /// sem apagar nada em disco.
    /// </summary>
    private static async Task AtalhoCtrlN()
    {
        var (vm, servicos) = Criar();
        using var _ = servicos;

        // Simula uma sessão já com resultados na tela.
        vm.Inventario = FlavosDiag.Providers.Fakes.Fixtures.Inventario();
        var inventarioAnterior = vm.Inventario;
        vm.Resumo.Categorias.Add(new ResultadoCategoria
        {
            Categoria = Regras.CategoriaMemoria,
            Veredito = Veredito.Inconclusivo,
            Resumo = "resultado anterior",
        });
        vm.Resumo.Etapas.Add("Inventário");
        vm.Resumo.Progresso = 1;
        vm.Resumo.ResumoGeral = "resumo antigo";
        vm.ItensNav.First(i => ReferenceEquals(i.Pagina, vm.LaudoVm)).Comando.Execute(null);
        Verificar(ReferenceEquals(vm.LaudoVm, vm.PaginaAtual), "preparação: deveria estar em Laudo");

        // 1) A limpeza em si, verificada de forma determinística (sem concorrer com o diagnóstico
        //    assíncrono que o Ctrl+N dispara logo depois).
        var resumo = vm.Resumo;
        resumo.LimparResultados();

        Iguais(0, resumo.Categorias.Count, "a limpeza deve descartar as categorias da sessão");
        Iguais(0, resumo.Etapas.Count, "a limpeza deve descartar as etapas da sessão");
        Iguais(Veredito.Inconclusivo, resumo.VereditoGeral, "sem resultado o veredito volta a Inconclusivo");
        Iguais("Nenhum diagnóstico executado nesta sessão.", resumo.ResumoGeral, "o resumo volta ao estado inicial");
        Verificar(!resumo.Executando, "após limpar, a tela não pode ficar no estado 'executando'");

        // 2) O Ctrl+N como a janela o dispara: precisa recolocar o técnico no Resumo e recomeçar.
        //    O comando dispara o diagnóstico assíncrono; espera-se a execução terminar para não
        //    deixar trabalho pendente ao fim do caso.
        var resumoVm = vm.Resumo;
        vm.IniciarNovoDiagnosticoCommand.Execute(null);

        var limite = DateTime.UtcNow.AddSeconds(30);
        while (resumoVm.Executando && DateTime.UtcNow < limite)
        {
            await Task.Delay(50);
        }

        Verificar(ReferenceEquals(vm.Resumo, vm.PaginaAtual), "Ctrl+N deve voltar ao Resumo");
        Verificar(
            resumo.ResumoGeral != "resumo antigo",
            "Ctrl+N deve descartar o resumo do diagnóstico anterior");
        Verificar(
            !ReferenceEquals(vm.Inventario, inventarioAnterior),
            "Ctrl+N deve refazer o inventário em vez de reaproveitar o da sessão anterior");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Executa a ação numa thread STA com uma <see cref="Application"/> real e os dicionários de tema
    /// mesclados, como no aplicativo. WPF exige STA e um <c>Application</c> para
    /// <c>TryFindResource</c> enxergar os recursos — sem isso, o teste de tema testaria outra coisa.
    /// </summary>
    private static void EmThreadSta(Action acao)
    {
        Exception? erro = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current is null)
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    foreach (var origem in new[] { "Theme/Tema.xaml", "Converters/Icone.xaml" })
                    {
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary
                        {
                            Source = new Uri("pack://application:,,,/FlavosDiag.App;component/" + origem, UriKind.Absolute),
                        });
                    }
                }

                acao();
            }
            catch (Exception ex)
            {
                erro = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("a operação de interface não terminou em 30 s");
        }

        if (erro is not null)
        {
            throw new InvalidOperationException("falha na thread de interface: " + erro.Message, erro);
        }
    }

    /// <summary>
    /// Troca de tema em tempo de execução: as chaves de cor de tema precisam mudar de valor.
    /// Se algum estilo usasse StaticResource, essa parte da interface ficaria congelada no tema antigo.
    /// </summary>
    private static Task TrocaDeTemaAtualizaCores()
    {
        EmThreadSta(() =>
        {
            // Estado inicial claro: garante que o dicionário escuro não está mesclado.
            FlavosDiag.App.App.AplicarTema(false);

            var claro = CorDe("FlavosTexto");
            var claroFundo = CorDe("FlavosSuperficie");
            var claroOk = CorDe("FlavosOk");

            // Aplicar o escuro, como o comando AlternarTema do aplicativo faz.
            FlavosDiag.App.App.AplicarTema(true);

            var textoEscuro = CorDe("FlavosTexto");
            var fundoEscuro = CorDe("FlavosSuperficie");
            var okEscuro = CorDe("FlavosOk");

            Verificar(textoEscuro != claro, $"a cor de texto deveria mudar no tema escuro (continuou {claro})");
            Verificar(fundoEscuro != claroFundo, $"a superfície deveria mudar no tema escuro (continuou {claroFundo})");
            Verificar(okEscuro != claroOk, $"a cor de OK deveria mudar no tema escuro (continuou {claroOk})");

            // Voltar ao claro precisa restaurar exatamente os valores originais.
            FlavosDiag.App.App.AplicarTema(false);

            Verificar(CorDe("FlavosTexto") == claro, "voltar ao tema claro deveria restaurar a cor de texto");
            Verificar(CorDe("FlavosSuperficie") == claroFundo, "voltar ao tema claro deveria restaurar a superfície");
            Verificar(CorDe("FlavosOk") == claroOk, "voltar ao tema claro deveria restaurar a cor de OK");
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// O dicionário do tema escuro é uma sobreposição de cores: se ele trouxer um <c>Style</c>, aquele
    /// estilo congela as cores do tema claro no momento em que é carregado e a troca deixa de valer.
    /// </summary>
    private static Task TemaEscuroSoTemCores()
    {
        EmThreadSta(() =>
        {
            var dicionario = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/FlavosDiag.App;component/Theme/TemaEscuro.xaml", UriKind.Absolute),
            };

            var estilos = dicionario.Keys.Cast<object>()
                .Where(k => dicionario[k] is Style)
                .ToList();

            Iguais(
                0,
                estilos.Count,
                "TemaEscuro.xaml deve conter apenas cores; estilos encontrados: " + string.Join(", ", estilos));

            // E precisa cobrir as mesmas chaves de cor do tema claro, senão um valor fica do tema anterior.
            var claroTema = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/FlavosDiag.App;component/Theme/Tema.xaml", UriKind.Absolute),
            };

            var chavesClaras = claroTema.Keys.Cast<object>()
                .Where(k => claroTema[k] is System.Windows.Media.SolidColorBrush)
                .ToList();

            Verificar(chavesClaras.Count > 0, "o tema claro deveria definir cores");

            foreach (var chave in chavesClaras)
            {
                Verificar(
                    dicionario.Contains(chave),
                    $"a chave de cor '{chave}' existe no tema claro mas não no escuro (ficaria presa no valor claro)");
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Todo item do menu precisa de ícone desenhado. O ícone vem de uma chave de recurso do
    /// dicionário de ícones, então este teste também confere que a chave de cada página existe —
    /// uma chave errada deixaria o menu com o espaço do ícone vazio, sem aviso nenhum.
    /// </summary>
    private static Task TodoItemTemIcone()
    {
        EmThreadSta(() =>
        {
            var (vm, servicos) = Criar();
            using var _ = servicos;

            foreach (var item in vm.ItensNav)
            {
                Verificar(
                    !ReferenceEquals(item.GeometriaIcone, System.Windows.Media.Geometry.Empty),
                    $"o item '{item.Rotulo}' ficou sem ícone (a chave de geometria não foi encontrada)");
                Verificar(!string.IsNullOrWhiteSpace(item.Rotulo), "todo item do menu precisa de rótulo");
            }

            Verificar(
                !ReferenceEquals(vm.ItemConfiguracoes.GeometriaIcone, System.Windows.Media.Geometry.Empty),
                "o item de Configurações ficou sem ícone");

            // A chave declarada por cada página precisa existir de fato no dicionário de ícones.
            foreach (var pagina in vm.Paginas.Append(vm.ConfigVm))
            {
                Verificar(
                    Application.Current?.TryFindResource(pagina.Icone) is System.Windows.Media.Geometry,
                    $"a página '{pagina.Titulo}' aponta para a chave de ícone '{pagina.Icone}', que não existe");
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cancelar precisa interromper depressa e não pode virar "Falha": cancelar é escolha do técnico,
    /// não defeito do equipamento.
    /// </summary>
    private static async Task CancelarDiagnostico()
    {
        var (vm, servicos) = Criar();
        using var _ = servicos;

        var resumo = vm.Resumo;
        Verificar(
            resumo.DiagnosticoRapidoCancelCommand is not null,
            "o comando de cancelar deveria existir (RelayCommand IncludeCancelCommand)");

        // Inicia pelo comando real do botão e cancela logo em seguida.
        var execucao = resumo.DiagnosticoRapidoCommand.ExecuteAsync(null);

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        resumo.DiagnosticoRapidoCancelCommand!.Execute(null);

        var concluiu = execucao.Wait(TimeSpan.FromSeconds(2));
        relogio.Stop();

        Verificar(
            concluiu,
            $"o cancelamento deveria interromper em menos de 2 s (ainda não terminou após {relogio.ElapsedMilliseconds} ms)");

        Verificar(!resumo.Executando, "após cancelar, a tela não pode continuar no estado 'executando'");
        Verificar(
            resumo.VereditoGeral != Veredito.Falha,
            "cancelar não pode produzir veredito Falha (é escolha do técnico, não defeito do equipamento)");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Nenhuma View pode usar cor literal (<c>White</c>, <c>Black</c>, <c>#RRGGBB</c>) em propriedade de
    /// cor. Uma cor fixa não acompanha a troca de tema: no tema escuro o mesmo branco pode ficar ilegível
    /// sobre um fundo que clareou. Três ocorrências foram encontradas assim (ícones sobre o verde-petróleo)
    /// depois de a troca de tema já estar funcionando — por isso esta verificação existe.
    /// </summary>
    private static Task NenhumaCorFixa()
    {
        // Raiz do repositório a partir da pasta de saída do teste (bin/Debug/net8.0-windows).
        var raiz = AppContext.BaseDirectory;
        for (var i = 0; i < 5 && raiz is not null; i++)
        {
            var candidato = Path.Combine(raiz, "src", "FlavosDiag.App");
            if (Directory.Exists(candidato))
            {
                raiz = candidato;
                break;
            }

            raiz = Path.GetDirectoryName(raiz.TrimEnd(Path.DirectorySeparatorChar));
        }

        Verificar(raiz is not null && Directory.Exists(raiz), "não foi possível localizar a pasta src/FlavosDiag.App a partir do teste");

        var propriedades = "Stroke|Fill|Foreground|Background|BorderBrush|CaretBrush";
        var padrao = new System.Text.RegularExpressions.Regex(
            $@"\b({propriedades})\s*=\s*""(White|Black|#[0-9A-Fa-f]{{3,8}})""");

        var achados = new List<string>();
        foreach (var arquivo in Directory.EnumerateFiles(raiz!, "*.xaml", SearchOption.AllDirectories))
        {
            var nome = Path.GetFileName(arquivo);

            // Os dicionários de tema são justamente onde os valores literais devem morar.
            if (nome.Equals("Tema.xaml", StringComparison.OrdinalIgnoreCase) ||
                nome.Equals("TemaEscuro.xaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var linhas = File.ReadAllLines(arquivo);
            for (var n = 0; n < linhas.Length; n++)
            {
                var m = padrao.Match(linhas[n]);
                if (m.Success)
                {
                    achados.Add($"{nome}:{n + 1}: {m.Value}");
                }
            }
        }

        Verificar(
            achados.Count == 0,
            "cores fixas encontradas nas View (devem usar DynamicResource do tema): " + string.Join("; ", achados));

        return Task.CompletedTask;
    }
    /// <summary>
    /// <c>Run.Text</c> tem <c>Mode=TwoWay</c> por padrão. Vincular um <c>Run</c> a uma propriedade
    /// somente leitura (<c>Leitura&lt;T&gt;.Valor</c>, membros de <c>record</c>) derruba o aplicativo com
    /// <c>XamlParseException</c> no momento em que a página é desenhada — e só nesse momento, por isso
    /// escapa de testes que apenas montam ViewModels. Foi exatamente o defeito que derrubou o pacote
    /// publicado ao abrir a página Sistema.
    /// </summary>
    private static Task RunSemBindingTwoWay()
    {
        var raiz = PastaDasViews();
        var padrao = new System.Text.RegularExpressions.Regex(
            @"<Run\b[^>]*?Text=""\{Binding\s+(?<corpo>[^}]*)\}""");

        var achados = new List<string>();
        foreach (var arquivo in Directory.EnumerateFiles(raiz, "*.xaml", SearchOption.AllDirectories))
        {
            var linhas = File.ReadAllLines(arquivo);
            for (var n = 0; n < linhas.Length; n++)
            {
                foreach (System.Text.RegularExpressions.Match m in padrao.Matches(linhas[n]))
                {
                    var corpo = m.Groups["corpo"].Value;
                    if (!corpo.Contains("Mode=OneWay", StringComparison.OrdinalIgnoreCase))
                    {
                        achados.Add($"{Path.GetFileName(arquivo)}:{n + 1}: {corpo.Trim()}");
                    }
                }
            }
        }

        Verificar(
            achados.Count == 0,
            "todo <Run> precisa de Mode=OneWay (Run.Text é TwoWay por padrão e quebra em propriedade somente leitura): "
            + string.Join("; ", achados));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Desenha cada página de verdade, numa janela real, e reprova se qualquer exceção for lançada.
    /// É o teste que faltava: todo o resto trabalhava com ViewModels e nunca montava o XAML, por isso um
    /// binding inválido só apareceu ao abrir o pacote publicado.
    /// </summary>
    private static Task TodasAsPaginasRenderizam()
    {
        EmThreadSta(() =>
        {
            var (vm, servicos) = Criar();
            using var _ = servicos;

            // Mesma composição de recursos do App.xaml: WPF-UI, tema, ícones e conversores.
            // (Carregar o App.xaml diretamente não serve: ele tenta construir a classe Application,
            // e só pode existir uma por processo — a do teste.)
            foreach (var origem in new[]
                     {
                         "/Wpf.Ui;component/Resources/Wpf.Ui.xaml",
                         "/FlavosDiag.App;component/Theme/Tema.xaml",
                         "/FlavosDiag.App;component/Converters/Icone.xaml",
                     })
            {
                Application.Current!.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,," + origem, UriKind.Absolute),
                });
            }

            var conversores = new FlavosDiag.App.Converters.VereditoParaPincel();
            Application.Current!.Resources["VereditoPincel"] = conversores;
            Application.Current.Resources["VereditoFundo"] = new FlavosDiag.App.Converters.VereditoParaPincel { Fundo = true };
            Application.Current.Resources["VereditoTexto"] = new FlavosDiag.App.Converters.VereditoParaTexto();
            Application.Current.Resources["BoolVisivel"] = new FlavosDiag.App.Converters.BoolParaVisibilidade();
            Application.Current.Resources["TextoVisivel"] = new FlavosDiag.App.Converters.TextoParaVisibilidade();
            Application.Current.Resources["ColecaoVazia"] = new FlavosDiag.App.Converters.ColecaoVaziaParaVisibilidade();
            Application.Current.Resources["Bytes"] = new FlavosDiag.App.Converters.BytesParaTexto();
            Application.Current.Resources["FundoDestaqueMenu"] = new FlavosDiag.App.Converters.SelecaoParaFundo();

            var falhas = new List<string>();
            var errosBinding = new List<string>();
            var janela = new Window { Width = 1024, Height = 700, DataContext = vm };
            var rastreio = new ListaRastreio(errosBinding);
            try
            {
                // Preenche os dados de sessão com a fixture: sem isso os DataTemplate de listas
                // (módulos de memória, discos, redes) nunca são instanciados e um binding inválido
                // dentro deles passaria despercebido — foi assim que o defeito do <Run> escapou.
                vm.Inventario = FlavosDiag.Providers.Fakes.Fixtures.Inventario();
                vm.Discos = FlavosDiag.Providers.Fakes.Fixtures.Discos();
                vm.Bateria = FlavosDiag.Providers.Fakes.Fixtures.Bateria();
                vm.Rede = FlavosDiag.Providers.Fakes.Fixtures.Rede();

                // As listas das páginas só são preenchidas no carregamento assíncrono, que não corre
                // aqui. Preencher as coleções observáveis diretamente garante que os DataTemplate das
                // listas sejam materializados — é dentro deles que os bindings inválidos se escondem.
                foreach (var modulo in vm.Inventario.Memoria.Modulos)
                {
                    vm.SistemaVm.Modulos.Add(modulo);
                }

                vm.SistemaVm.CamposMemoria.Add(new FlavosDiag.App.ViewModels.LinhaCampo("Total", "32 GB"));

                janela.Content = new FlavosDiag.App.Views.ConteudoPagina();
                janela.Show();
                Desenhar(janela);

                System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level =
                    System.Diagnostics.SourceLevels.Error;
                System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(rastreio);

                foreach (var pagina in vm.Paginas.Append(vm.ConfigVm))
                {
                    try
                    {
                        vm.NavegarPara(pagina);
                        Desenhar(janela);
                        Desenhar(janela);
                    }
                    catch (Exception ex)
                    {
                        var real = ex is System.Windows.Markup.XamlParseException { InnerException: { } interna }
                            ? interna
                            : ex;
                        falhas.Add($"{pagina.Titulo}: {real.GetType().Name}: {real.Message}");
                    }
                }
            }
            finally
            {
                janela.Close();
            }

            System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Remove(rastreio);
            Verificar(
                falhas.Count == 0,
                "páginas que falharam ao desenhar: " + string.Join(" | ", falhas));
            Verificar(
                errosBinding.Count == 0,
                "erros de binding durante o desenho: " + string.Join(" | ", errosBinding.Distinct().Take(5)));
        });

        return Task.CompletedTask;
    }

    /// <summary>Coleta os erros de binding que o WPF registra em vez de lançar.</summary>
    private sealed class ListaRastreio : System.Diagnostics.TraceListener
    {
        private readonly List<string> _destino;

        public ListaRastreio(List<string> destino) => _destino = destino;

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _destino.Add(message);
            }
        }
    }

    /// <summary>Força o ciclo de layout e de renderização, que é quando os bindings são avaliados.</summary>
    private static void Desenhar(Window janela)
    {
        janela.UpdateLayout();
        var fila = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        fila.Invoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => { }));
        janela.UpdateLayout();
    }

    /// <summary>Pasta src/FlavosDiag.App localizada a partir da pasta de saída do teste.</summary>
    private static string PastaDasViews()
    {
        var raiz = AppContext.BaseDirectory;
        for (var i = 0; i < 6 && !string.IsNullOrEmpty(raiz); i++)
        {
            var candidato = Path.Combine(raiz, "src", "FlavosDiag.App");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            raiz = Path.GetDirectoryName(raiz.TrimEnd(Path.DirectorySeparatorChar))!;
        }

        throw new InvalidOperationException("não foi possível localizar src/FlavosDiag.App a partir do teste");
    }
    private static string CorDe(string chave)
    {
        if (Application.Current?.TryFindResource(chave) is System.Windows.Media.SolidColorBrush pincel)
        {
            return pincel.Color.ToString();
        }

        // Sem Application (alguns contextos de teste): lê direto dos dicionários de tema.
        foreach (var dicionario in Dicionarios())
        {
            if (dicionario[chave] is System.Windows.Media.SolidColorBrush b)
            {
                return b.Color.ToString();
            }
        }

        throw new InvalidOperationException($"a chave de cor '{chave}' não foi encontrada no tema");
    }

    private static IEnumerable<ResourceDictionary> Dicionarios()
    {
        var claroTema = new ResourceDictionary { Source = new Uri("pack://application:,,,/FlavosDiag.App;component/Theme/Tema.xaml", UriKind.Absolute) };
        var escuroTema = new ResourceDictionary { Source = new Uri("pack://application:,,,/FlavosDiag.App;component/Theme/TemaEscuro.xaml", UriKind.Absolute) };

        // Ordem de precedência: o escuro, se mesclado depois, vence.
        yield return escuroTema;
        yield return claroTema;
    }
}
