using FlavosDiag.Core;

namespace FlavosDiag.Tests;

public class LeituraTests
{
    [Fact]
    public void Ok_marca_disponivel_e_sem_causa()
    {
        var l = Leitura<int>.Ok(42, "WMI");
        Assert.True(l.Disponivel);
        Assert.False(l.Ausente);
        Assert.Equal(42, l.Valor);
        Assert.Null(l.Causa);
        Assert.Equal("WMI", l.Fonte);
    }

    [Fact]
    public void Estados_de_ausencia_sempre_tem_causa_e_nunca_sao_ok()
    {
        var casos = new[]
        {
            Leitura<int>.NaoSuportado("sem sensor"),
            Leitura<int>.SemPermissao("exige administrador"),
            Leitura<int>.Falha("exceção"),
            Leitura<int>.TempoEsgotado("passou de 10 s"),
            Leitura<int>.NaoLido(),
        };

        foreach (var l in casos)
        {
            Assert.False(l.Disponivel);
            Assert.True(l.Ausente);
            Assert.False(string.IsNullOrWhiteSpace(l.Causa));
            Assert.Equal(0, l.ValorOu(0));
        }
    }

    [Fact]
    public void Map_preserva_estado_de_ausencia()
    {
        var l = Leitura<string>.SemPermissao("precisa de administrador").Map(s => s.Length);
        Assert.Equal(EstadoLeitura.SemPermissao, l.Estado);
        Assert.False(l.Disponivel);
        Assert.Equal("precisa de administrador", l.Causa);
    }

    [Fact]
    public void Conversao_implicita_cria_leitura_disponivel()
    {
        Leitura<int> l = 7;
        Assert.True(l.Disponivel);
        Assert.Equal(7, l.Valor);
    }

    [Fact]
    public void Limitacoes_registram_somente_ausentes()
    {
        var lim = new Limitacoes();
        lim.AdicionarSeAusente("Temperatura CPU", Leitura<double>.SemPermissao("administrador necessário"));
        lim.AdicionarSeAusente("Clock", Leitura<double>.Ok(3200));

        var item = Assert.Single(lim.Itens);
        Assert.Equal("Temperatura CPU", item.Item);
        Assert.Contains("administrador", item.Motivo);
    }
}
