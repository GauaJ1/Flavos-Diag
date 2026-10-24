using FlavosDiag.Providers.Abstractions;
using FlavosDiag.Providers.Discos;
using FlavosDiag.Providers.Nativo;

namespace FlavosDiag.Tests;

/// <summary>
/// Travas da lista de comandos permitidos e do caminho único de pass-through para o disco (T6/D78).
/// </summary>
/// <remarks>
/// Nenhum caso aqui envia comando a um disco real: os testes usam constantes, enumerações e leitura do
/// código-fonte. O que se prova:
/// <list type="bullet">
/// <item>a lista só contém comandos de leitura, com os opcodes conferidos na especificação;</item>
/// <item>o construtor de requisição não aceita byte de comando — a enumeração é fechada;</item>
/// <item>sem elevação ou sem aceite do usuário a leitura é recusada <b>antes</b> de abrir o disco;</item>
/// <item>nenhum outro arquivo de <c>src/</c> abre <c>\\.\PhysicalDrive</c> ou cita os IOCTL de pass-through.</item>
/// </list>
/// </remarks>
public static class ComandosDiscoPermitidosTests
{
    /// <summary>
    /// Os únicos arquivos autorizados a conhecer o nome do dispositivo e os IOCTL de pass-through.
    /// Qualquer outro arquivo em <c>src/</c> que apareça nessa varredura quebra o caminho único.
    /// </summary>
    private static readonly string[] ArquivosAutorizados =
    {
        Path.Combine("src", "FlavosDiag.Providers", "Nativo", "RequisicaoDisco.cs"),
        Path.Combine("src", "FlavosDiag.Providers", "Nativo", "ComandosDiscoPermitidos.cs"),
    };

    public static IEnumerable<(string Nome, Func<Task> Corpo)> Casos()
    {
        // ---- Lista de permitidos ----
        yield return ("Permitidos: só comandos de leitura", () => Task.Run(SomenteLeitura));
        yield return ("Permitidos: opcodes ATA batem com a especificação", () => Task.Run(OpcodesAtaDaEspecificacao));
        yield return ("Permitidos: opcodes NVMe batem com a especificação", () => Task.Run(OpcodesNvmeDaEspecificacao));
        yield return ("Permitidos: escrita de setores e SMART WRITE LOG ficam de fora", () => Task.Run(EscritasFicamDeFora));

        // ---- Recusa no construtor de requisição ----
        yield return ("Recusa: sem elevação não abre o disco", () => Task.Run(SemElevacaoRecusa));
        yield return ("Recusa: sem aceite do usuário não abre o disco", () => Task.Run(SemAceiteRecusa));
        yield return ("Recusa: elevação e aceite juntos liberam a pré-checagem", () => Task.Run(ElevadoComAceitePassa));
        yield return ("Recusa: disco negativo é inválido", () => Task.Run(DiscoNegativoRecusado));
        yield return ("Recusa: pedido ATA fora da lista é recusado", () => Task.Run(PedidoAtaForaDosLimitesRecusado));
        yield return ("Recusa: pedido NVMe fora da lista é recusado", () => Task.Run(PedidoNvmeForaDosLimitesRecusado));
        yield return ("Recusa: mensagem cita o byte e a lista", () => Task.Run(MensagemDeRecusaExplica));

        // ---- Sem API livre para byte de comando ----
        yield return ("Construtor: nenhuma sobrecarga aceita byte de comando", () => Task.Run(SemParametroDeComando));

        // ---- Arquitetura do código-fonte ----
        yield return ("Arquitetura: só o caminho único cita PhysicalDrive e pass-through", () => Task.Run(ArquiteturaCaminhoUnico));
        yield return ("Arquitetura: a abertura com escrita existe em um só lugar", () => Task.Run(AberturaComEscritaUnica));
        yield return ("Arquitetura: o aviso diz que o disco só é lido", () => Task.Run(AvisoDizSomenteLeitura));
        yield return ("Arquitetura: o aviso acontece antes de listar discos", () => Task.Run(AvisoAntesDaLeitura));
    }

    // ------------------------------------------------------------------ lista de permitidos

    private static void SomenteLeitura()
    {
        // A lista em vigor é explicitamente curta. Se alguém acrescentar um comando, este teste
        // obriga a olhar a lista de novo e justificar a inclusão.
        Assert.Empty(ComandosDiscoPermitidos.OpcodesAtaEmVigor.Except(new byte[]
        {
            ComandosDiscoPermitidos.AtaIdentifyDevice,
            ComandosDiscoPermitidos.AtaSmartReadData,
        }));
        Assert.Empty(ComandosDiscoPermitidos.SubcomandosSmartEmVigor.Except(new byte[]
        {
            ComandosDiscoPermitidos.AtaSmartReadData,
            ComandosDiscoPermitidos.AtaSmartReadThresholds,
        }));
        Assert.Empty(ComandosDiscoPermitidos.OpcodesNvmeEmVigor.Except(new[]
        {
            ComandosDiscoPermitidos.NvmeGetLogPage,
        }));

        // Todo opcode aceito tem descrição legível (usada na mensagem de recusa).
        foreach (var comando in ComandosDiscoPermitidos.OpcodesAtaEmVigor)
        {
            Assert.True(ComandosDiscoPermitidos.DescreverAta(comando) is not null, $"opcode ATA 0x{comando:X2} sem descrição");
        }

        foreach (var feature in ComandosDiscoPermitidos.SubcomandosSmartEmVigor)
        {
            Assert.True(ComandosDiscoPermitidos.DescreverAtaSmart(feature) is not null, $"feature SMART 0x{feature:X2} sem descrição");
        }

        foreach (var opcode in ComandosDiscoPermitidos.OpcodesNvmeEmVigor)
        {
            Assert.True(ComandosDiscoPermitidos.DescreverNvme(opcode) is not null, $"opcode NVMe 0x{opcode:X2} sem descrição");
        }
    }

    private static void OpcodesAtaDaEspecificacao()
    {
        // Valores públicos (ATA/ATAPI-8; conferidos em include/uapi/linux/hdreg.h).
        Assert.Equal(0xEC, ComandosDiscoPermitidos.AtaIdentifyDevice);
        Assert.Equal(0xB0, ComandosDiscoPermitidos.AtaSmart);
        Assert.Equal(0xD0, ComandosDiscoPermitidos.AtaSmartReadData);
        Assert.Equal(0xD1, ComandosDiscoPermitidos.AtaSmartReadThresholds);
        Assert.Equal(0x2F, ComandosDiscoPermitidos.AtaReadLogExt);

        // As formas canônicas respondem pela lista; a dupla 0xB0 + feature é aceita.
        Assert.True(ComandosDiscoPermitidos.DescreverAta(0xEC) is not null, "IDENTIFY DEVICE é leitura e entra");
        Assert.True(ComandosDiscoPermitidos.DescreverAtaSmart(0xD0) is not null, "SMART READ DATA é leitura e entra");
        Assert.True(ComandosDiscoPermitidos.DescreverAtaSmart(0xD1) is not null, "SMART READ THRESHOLDS é leitura e entra");
    }

    private static void OpcodesNvmeDaEspecificacao()
    {
        // Valores públicos (NVMe Base Specification, Admin Command Set; conferidos em include/linux/nvme.h).
        Assert.Equal(0x06, ComandosDiscoPermitidos.NvmeIdentify);
        Assert.Equal(0x02, ComandosDiscoPermitidos.NvmeGetLogPage);
        Assert.Equal(0x0A, ComandosDiscoPermitidos.NvmeGetFeatures);
        Assert.Equal(0x02, ComandosDiscoPermitidos.NvmeLogPageSmartHealth);

        Assert.True(ComandosDiscoPermitidos.DescreverNvme(0x02) is not null, "Get Log Page é leitura e entra");
        Assert.False(ComandosDiscoPermitidos.DescreverNvme(0x01) is not null, "NVMe Write (0x01) não pode entrar");
    }

    private static void EscritasFicamDeFora()
    {
        // 0x30 WRITE SECTORS, 0x3F e 0xC4/0xC5 (leitura/escrita múltipla) não são leitura.
        foreach (var comando in new byte[] { 0x30, 0x3F, 0xC5, 0xCA, 0xE8 })
        {
            Assert.False(
                ComandosDiscoPermitidos.DescreverAta(comando) is not null,
                $"comando ATA 0x{comando:X2} não pode estar na lista de permitidos");
        }

        // Subcomandos SMART de escrita: 0xD6 WRITE LOG, 0xD7 WRITE THRESHOLDS, 0xD2/D2 autosave.
        foreach (var feature in new byte[] { 0xD6, 0xD7, 0xD2, 0xD3 })
        {
            Assert.False(
                ComandosDiscoPermitidos.DescreverAtaSmart(feature) is not null,
                $"subcomando SMART 0x{feature:X2} é escrita e não pode estar na lista");
        }

        // NVMe: 0x01 Write, 0x09 Set Features (escrita), 0x80 Format NVM, 0x84 Sanitize.
        foreach (var opcode in new byte[] { 0x01, 0x04, 0x09, 0x80, 0x84 })
        {
            Assert.False(
                ComandosDiscoPermitidos.DescreverNvme(opcode) is not null,
                $"opcode NVMe 0x{opcode:X2} não é leitura e não pode estar na lista");
        }
    }

    // ------------------------------------------------------------------ recusa no construtor

    private static void SemElevacaoRecusa()
    {
        // Elevação falsa: a leitura é recusada. Não há disco aberto aqui — se houvesse, o teste
        // dependeria da máquina. O que se prova é a decisão.
        var permissoes = new RequisicaoDisco.Permissoes(ProcessoElevado: false, UsuarioAutorizouLeitura: true);
        Assert.False(permissoes.Permitido, "sem elevação o pass-through precisa ficar recusado");

        var resultado = RequisicaoDisco.Executar(numeroDisco: -1, RequisicaoDisco.PedidoAta.LerAtributosSmart, permissoes);
        Assert.False(resultado.Sucesso, "não pode haver leitura sem elevação");
        Assert.True(resultado.Dados is null, "recusa não devolve dados");
        Assert.True(
            resultado.Erro is not null && resultado.Erro.Contains("não está elevado"),
            "a recusa precisa nomear a falta de elevação");
    }

    private static void SemAceiteRecusa()
    {
        var permissoes = new RequisicaoDisco.Permissoes(ProcessoElevado: true, UsuarioAutorizouLeitura: false);
        Assert.False(permissoes.Permitido, "sem aceite do usuário o pass-through precisa ficar recusado");

        var resultado = RequisicaoDisco.Executar(-1, RequisicaoDisco.PedidoAta.LerAtributosSmart, permissoes);
        Assert.False(resultado.Sucesso, "não pode haver leitura sem aceite");
        Assert.True(
            resultado.Erro is not null && resultado.Erro.Contains("não autorizou"),
            "a recusa precisa nomear a falta de autorização");
    }

    private static void ElevadoComAceitePassa()
    {
        var permissoes = new RequisicaoDisco.Permissoes(ProcessoElevado: true, UsuarioAutorizouLeitura: true);
        Assert.True(permissoes.Permitido, "elevado e autorizado precisa liberar a pré-checagem");

        // A recusa só pode vir do dispositivo (número inválido), nunca das pré-condições.
        var resultado = RequisicaoDisco.Executar(-1, RequisicaoDisco.PedidoAta.LerAtributosSmart, permissoes);
        Assert.False(resultado.Sucesso, "disco -1 não existe");
        Assert.False(
            resultado.Erro is not null && (resultado.Erro.Contains("não está elevado") || resultado.Erro.Contains("não autorizou")),
            "com elevação e aceite a recusa não pode ser de pré-condição");
    }

    private static void DiscoNegativoRecusado()
    {
        var permissoes = new RequisicaoDisco.Permissoes(true, true);
        var resultado = RequisicaoDisco.Executar(-5, RequisicaoDisco.PedidoAta.Identificar, permissoes);
        Assert.False(resultado.Sucesso, "número negativo nunca é um disco");
        Assert.True(
            resultado.Erro is not null && resultado.Erro.Contains("inválido"),
            "a recusa precisa explicar que o número é inválido");
    }

    private static void PedidoAtaForaDosLimitesRecusado()
    {
        // Um valor numérico fora da enumeração não pode virar comando. O construtor não recebe byte;
        // forçar um número inválido precisa cair na recusa da lista, nunca abrir o disco.
        // Disco 0 (válido) para que a recusa venha da lista, não da checagem de número.
        var permissoes = new RequisicaoDisco.Permissoes(true, true);
        var invalido = (RequisicaoDisco.PedidoAta)0x7F;
        var resultado = RequisicaoDisco.Executar(0, invalido, permissoes);
        Assert.False(resultado.Sucesso, "pedido fora da enumeração não pode ser executado");
        Assert.True(
            resultado.Erro is not null && resultado.Erro.Contains("recusado"),
            "pedido fora da enumeração precisa ser recusado pela lista (erro: " + resultado.Erro + ")");
    }

    private static void PedidoNvmeForaDosLimitesRecusado()
    {
        var permissoes = new RequisicaoDisco.Permissoes(true, true);
        var invalido = (RequisicaoDisco.PedidoNvme)0x7F;
        var resultado = RequisicaoDisco.Executar(0, invalido, permissoes);
        Assert.False(resultado.Sucesso, "pedido NVMe fora da enumeração não pode ser executado");
        Assert.True(
            resultado.Erro is not null && resultado.Erro.Contains("recusado"),
            "pedido NVMe fora da enumeração precisa ser recusado pela lista (erro: " + resultado.Erro + ")");
    }

    private static void MensagemDeRecusaExplica()
    {
        // A mensagem de recusa da lista é montada a partir da própria lista: precisa citar o byte
        // recusado e os comandos aceitos, para o técnico não ficar sem saída.
        Assert.True(
            ComandosDiscoPermitidos.DescricaoDaLista.Count > 0,
            "a lista precisa ter descrição legível");
        Assert.Contains("leitura", string.Join(" ", ComandosDiscoPermitidos.DescricaoDaLista));

        // Nenhuma descrição pode anunciar escrita.
        Assert.False(
            string.Join(" ", ComandosDiscoPermitidos.DescricaoDaLista).Contains("escrita", StringComparison.OrdinalIgnoreCase),
            "nenhum comando da lista pode ser de escrita");
    }

    private static void SemParametroDeComando()
    {
        // Prova estrutural: nenhuma sobrecarga pública de Executar recebe byte/ushort como primeiro
        // parâmetro além do número do disco, e nenhum parâmetro se chama "comando"/"opcode"/"feature".
        var metodos = typeof(RequisicaoDisco)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == "Executar")
            .ToList();

        Assert.True(metodos.Count > 0, "precisa existir o caminho único de execução");

        foreach (var metodo in metodos)
        {
            foreach (var parametro in metodo.GetParameters())
            {
                Assert.False(
                    parametro.ParameterType == typeof(byte) || parametro.ParameterType == typeof(ushort),
                    $"sobrecarga {metodo} expõe byte de comando no parâmetro {parametro.Name}");

                var nome = parametro.Name?.ToLowerInvariant() ?? string.Empty;
                Assert.False(
                    nome is "comando" or "opcode" or "feature" or "bytes" or "taskfile",
                    $"sobrecarga {metodo} expõe comando arbitrário no parâmetro {parametro.Name}");
            }

            // O segundo parâmetro é sempre a enumeração fechada, nunca um número.
            var segundo = metodo.GetParameters().ElementAtOrDefault(1);
            Assert.True(segundo is not null && segundo.ParameterType.IsEnum, $"a sobrecarga {metodo} precisa receber um pedido enumerado");
        }
    }

    // ------------------------------------------------------------------ arquitetura do fonte

    private static void ArquiteturaCaminhoUnico()
    {
        var raiz = EncontrarRaiz();
        var arquivos = Directory.GetFiles(Path.Combine(raiz, "src"), "*.cs", SearchOption.AllDirectories);

        // Marcas de que o arquivo sabe falar com o dispositivo por pass-through.
        var marcas = new[]
        {
            "PhysicalDrive",
            "IOCTL_ATA_PASS_THROUGH",
            "IoCtlAtaPassThroughDirect",
            "IOCTL_SCSI_MINIPORT",
            "IoCtlScsiMiniport",
            "CREATE_IO_QUEUE",
        };

        var infratores = new List<string>();
        foreach (var arquivo in arquivos)
        {
            var relativo = Path.GetRelativePath(raiz, arquivo);
            if (ArquivosAutorizados.Any(a => string.Equals(a, relativo, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var texto = SemComentarios(File.ReadAllText(arquivo));
            foreach (var marca in marcas)
            {
                if (texto.Contains(marca, StringComparison.Ordinal))
                {
                    infratores.Add($"{relativo} cita '{marca}'");
                }
            }
        }

        Assert.Empty(infratores);
    }

    private static void AberturaComEscritaUnica()
    {
        var raiz = EncontrarRaiz();
        var arquivos = Directory.GetFiles(Path.Combine(raiz, "src"), "*.cs", SearchOption.AllDirectories);

        // O direito de escrita em disco só existe dentro do caminho único. Em qualquer outro arquivo é
        // sinal de que alguém abriu o disco com permissão de escrita por fora da lista de permitidos.
        // Comentários são ignorados: citar o assunto em documentação é legítimo; usá-lo em código, não.
        var comEscrita = arquivos
            .Where(a => SemComentarios(File.ReadAllText(a)).Contains("GENERIC_WRITE", StringComparison.Ordinal)
                        || SemComentarios(File.ReadAllText(a)).Contains("GenericWrite", StringComparison.Ordinal))
            .Select(a => Path.GetRelativePath(raiz, a))
            .ToList();

        Assert.True(
            comEscrita.Count == 1,
            "acesso de escrita em disco deve existir em exatamente um arquivo (o caminho único); encontrados: " +
            string.Join(", ", comEscrita));

        Assert.True(
            string.Equals(comEscrita[0], ArquivosAutorizados[0], StringComparison.OrdinalIgnoreCase),
            $"o único arquivo com acesso de escrita precisa ser {ArquivosAutorizados[0]}, e não {comEscrita[0]}");

        // E a combinação do acesso acontece só na abertura privada.
        var requisicao = SemComentarios(File.ReadAllText(Path.Combine(raiz, ArquivosAutorizados[0])));
        Assert.True(
            requisicao.Contains("acessoDeEscrita ? GenericWrite : 0", StringComparison.Ordinal),
            "o caminho único precisa ser o único a combinar a permissão de escrita no CreateFile");
    }

    private static void AvisoDizSomenteLeitura()
    {
        var aviso = DiscosWindowsProvider.AvisoLeituraDisco;

        Assert.True(
            aviso.Contains("SOMENTE LEITURA", StringComparison.Ordinal),
            "o aviso precisa declarar em maiúsculas que a consulta é somente leitura");
        Assert.True(
            aviso.Contains("não grava nada", StringComparison.Ordinal),
            "o aviso precisa dizer que nada é gravado no disco");
        Assert.True(
            aviso.Contains("SMART", StringComparison.Ordinal),
            "o aviso precisa nomear a consulta SMART");
        Assert.True(
            aviso.Contains("administrador", StringComparison.Ordinal),
            "o aviso precisa explicar por que exige administrador");
    }

    private static void AvisoAntesDaLeitura()
    {
        // Prova estrutural no fonte: a chamada ao aviso precede a listagem de discos no ViewModel.
        // Sem isto, alguém poderia mover a leitura para antes do aviso e reabrir o buraco.
        var raiz = EncontrarRaiz();
        var caminho = Path.Combine(raiz, "src", "FlavosDiag.App", "ViewModels", "DiscoViewModel.cs");
        Assert.True(File.Exists(caminho), $"arquivo não encontrado: {caminho}");

        var texto = File.ReadAllText(caminho);
        var posicaoAviso = texto.IndexOf("AutorizarLeituraDisco()", StringComparison.Ordinal);
        var posicaoLeitura = texto.IndexOf("Discos.ListarAsync()", StringComparison.Ordinal);

        Assert.True(posicaoAviso >= 0, "o ViewModel precisa pedir o aviso de leitura do disco");
        Assert.True(posicaoLeitura >= 0, "o ViewModel precisa listar os discos");
        Assert.True(
            posicaoAviso < posicaoLeitura,
            "o aviso precisa vir ANTES da leitura dos discos");
    }

    /// <summary>
    /// Remove comentários de linha (<c>//</c>) e de bloco (<c>/* */</c>) do fonte, para que a varredura
    /// arquitetural olhe código e não prosa. Sem isto, explicar a regra num comentário passaria a
    /// "violá-la", e o teste viraria ruído em vez de trava.
    /// </summary>
    private static string SemComentarios(string fonte)
    {
        var semBloco = System.Text.RegularExpressions.Regex.Replace(fonte, @"/\*.*?\*/", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        return System.Text.RegularExpressions.Regex.Replace(semBloco, @"//[^""]*$", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    /// <summary>Sobe do diretório de saída até achar a raiz do repositório (a que contém src/).</summary>
    private static string EncontrarRaiz()
    {
        var diretorio = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && diretorio is not null; i++)
        {
            if (Directory.Exists(Path.Combine(diretorio, "src", "FlavosDiag.Providers")))
            {
                return diretorio;
            }

            diretorio = Path.GetDirectoryName(diretorio.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new InvalidOperationException(
            $"não foi possível localizar a raiz do repositório a partir de {AppContext.BaseDirectory}");
    }
}
