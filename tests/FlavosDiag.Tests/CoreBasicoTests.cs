using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Core.Vereditos;

namespace FlavosDiag.Tests;

public class FormatacaoTests
{
    [Fact]
    public void Data_usa_dd_mm_aaaa()
    {
        Assert.Equal("07/03/2025", FormatacaoBr.Data(new DateTime(2025, 3, 7)));
    }

    [Fact]
    public void DataHora_inclui_segundos_em_pt_br()
    {
        Assert.Equal("07/03/2025 14:05:09", FormatacaoBr.DataHora(new DateTime(2025, 3, 7, 14, 5, 9)));
    }

    [Fact]
    public void Iso_usa_8601_com_offset()
    {
        var iso = FormatacaoBr.Iso(new DateTimeOffset(2025, 3, 7, 14, 5, 9, TimeSpan.FromHours(-3)));
        Assert.StartsWith("2025-03-07T14:05:09", iso);
        Assert.Matches(@"^-?\d{2}:\d{2}$", iso[^6..]);
    }

    [Fact]
    public void Bytes_formata_unidades()
    {
        Assert.Equal("512 B", FormatacaoBr.Bytes(512));
        Assert.Equal("1 KB", FormatacaoBr.Bytes(1024));
        Assert.Equal("2 MB", FormatacaoBr.Bytes(1536 * 1024));
    }

    [Fact]
    public void Bytes_usa_virgula_decimal_no_gb()
    {
        var texto = FormatacaoBr.Bytes(500L * 1024 * 1024 * 1024);
        Assert.Contains(",", texto);
        Assert.EndsWith("GB", texto);
    }

    [Fact]
    public void NaoDisponivel_mostra_o_motivo_quando_existe()
    {
        Assert.Equal("Não disponível", FormatacaoBr.NaoDisponivel(null));
        Assert.Equal("Não disponível — sensor ausente", FormatacaoBr.NaoDisponivel("sensor ausente"));
    }

    [Fact]
    public void Duracao_escolhe_a_melhor_unidade()
    {
        Assert.Equal("2,5 s", FormatacaoBr.Duracao(TimeSpan.FromSeconds(2.5)));
        Assert.Equal("3 min 05 s", FormatacaoBr.Duracao(TimeSpan.FromSeconds(185)));
        Assert.Equal("1 h 02 min 03 s", FormatacaoBr.Duracao(new TimeSpan(1, 2, 3)));
    }

    [Fact]
    public void VelocidadeLink_usa_gb_ou_mb()
    {
        Assert.Equal("1,0 Gb/s", FormatacaoBr.VelocidadeLink(1_000_000_000));
        Assert.Equal("100 Mb/s", FormatacaoBr.VelocidadeLink(100_000_000));
    }
}

public class MascaramentoTests
{
    [Fact]
    public void Mascara_mantem_pontas_de_seriais_longos()
    {
        Assert.Equal("ABC1****WXYZ", Mascaramento.Mascarar("ABC123456789WXYZ"));
    }

    [Fact]
    public void Serial_curto_nao_vaza_valor()
    {
        Assert.Equal("****", Mascaramento.Mascarar("SN123"));
        Assert.Equal(string.Empty, Mascaramento.Mascarar(null));
        Assert.Equal(string.Empty, Mascaramento.Mascarar("   "));
    }

    [Fact]
    public void Hash_e_estavel_e_nao_revela_valor()
    {
        var a = Mascaramento.HashChave("Fabricante", "Modelo", "SERIAL");
        var b = Mascaramento.HashChave("Fabricante", "Modelo", "SERIAL");
        var c = Mascaramento.HashChave("Fabricante", "Modelo", "OUTRO");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(16, a.Length);
        Assert.DoesNotContain("SERIAL", a);
    }
}

public class VereditoTests
{
    [Fact]
    public void Agregacao_retorna_a_categoria_quando_ha_uma_so()
    {
        foreach (var v in new[] { Veredito.Ok, Veredito.Atencao, Veredito.Inconclusivo, Veredito.Falha })
        {
            var res = VereditoConsolidador.Agregar(new[] { new ResultadoCategoria { Categoria = "CPU", Veredito = v, Resumo = "teste" } });
            Assert.Equal(v, res.Geral);
        }
    }

    [Fact]
    public void Falha_domina_todas_as_outras()
    {
        var res = VereditoConsolidador.Agregar(new[]
        {
            new ResultadoCategoria { Categoria = "CPU", Veredito = Veredito.Ok, Resumo = "ok" },
            new ResultadoCategoria { Categoria = "Disco", Veredito = Veredito.Inconclusivo, Resumo = "sem smart" },
            new ResultadoCategoria { Categoria = "Sensores", Veredito = Veredito.Atencao, Resumo = "quente" },
            new ResultadoCategoria { Categoria = "Rede", Veredito = Veredito.Falha, Resumo = "sem gateway" },
        });

        Assert.Equal(Veredito.Falha, res.Geral);
        Assert.Contains("Rede", res.Resumo);
    }

    [Fact]
    public void Inconclusivo_domina_atencao_e_ok()
    {
        var res = VereditoConsolidador.Agregar(new[]
        {
            new ResultadoCategoria { Categoria = "CPU", Veredito = Veredito.Ok, Resumo = "ok" },
            new ResultadoCategoria { Categoria = "Disco", Veredito = Veredito.Inconclusivo, Resumo = "sem smart" },
            new ResultadoCategoria { Categoria = "Sensores", Veredito = Veredito.Atencao, Resumo = "quente" },
        });

        Assert.Equal(Veredito.Inconclusivo, res.Geral);
    }

    [Fact]
    public void Lista_vazia_e_inconclusiva_nunca_ok()
    {
        Assert.Equal(Veredito.Inconclusivo, VereditoConsolidador.Agregar(Array.Empty<ResultadoCategoria>()).Geral);
        Assert.Equal(Veredito.Inconclusivo, VereditoConsolidador.Agregar(null).Geral);
    }

    [Fact]
    public void NaoSeAplica_e_ignorado_na_agregacao()
    {
        var res = VereditoConsolidador.Agregar(new[]
        {
            new ResultadoCategoria { Categoria = "Bateria", Veredito = Veredito.NaoSeAplica, Resumo = "desktop" },
            new ResultadoCategoria { Categoria = "CPU", Veredito = Veredito.Ok, Resumo = "ok" },
        });

        Assert.Equal(Veredito.Ok, res.Geral);

        var soDesktop = VereditoConsolidador.Agregar(new[]
        {
            new ResultadoCategoria { Categoria = "Bateria", Veredito = Veredito.NaoSeAplica, Resumo = "desktop" },
        });
        Assert.Equal(Veredito.Inconclusivo, soDesktop.Geral);
    }

    [Fact]
    public void MaisGrave_respeita_a_ordem_documentada()
    {
        Assert.Equal(Veredito.Falha, VereditoTexto.MaisGrave(Veredito.Ok, Veredito.Falha));
        Assert.Equal(Veredito.Inconclusivo, VereditoTexto.MaisGrave(Veredito.Atencao, Veredito.Inconclusivo));
        Assert.Equal(Veredito.Atencao, VereditoTexto.MaisGrave(Veredito.Ok, Veredito.Atencao));
        Assert.Equal(Veredito.Ok, VereditoTexto.MaisGrave(Veredito.Ok, Veredito.NaoSeAplica));
    }

    [Fact]
    public void Rotulos_estao_em_pt_br()
    {
        Assert.Equal("OK", Veredito.Ok.Rotulo());
        Assert.Equal("Atenção", Veredito.Atencao.Rotulo());
        Assert.Equal("Inconclusivo", Veredito.Inconclusivo.Rotulo());
        Assert.Equal("Falha", Veredito.Falha.Rotulo());
        Assert.Equal("Não se aplica", Veredito.NaoSeAplica.Rotulo());
    }
}
