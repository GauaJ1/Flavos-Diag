using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Nativo;

namespace FlavosDiag.Providers.Discos;

/// <summary>
/// Avaliação de saúde do disco a partir das leituras disponíveis (SMART ATA, log NVMe, contadores do Windows).
/// Regras em docs/REGRAS-VEREDITO.md; cada item devolve evidência com o valor medido e o motivo.
/// Função pura: não toca em hardware, por isso é testável com fixtures.
/// </summary>
public static class AvaliadorSaudeDisco
{
    public sealed record Achado(string Titulo, string Valor, string? Fonte, bool Critico, bool Aviso);

    /// <summary>
    /// Devolve os achados que importam para o veredito. Lista vazia com <paramref name="temLeitura"/>
    /// falso significa "nada foi lido" (Inconclusivo), nunca "disco saudável".
    /// </summary>
    public static (List<Achado> Achados, bool TemLeitura) Avaliar(SaudeDisco saude)
    {
        var achados = new List<Achado>();
        var temLeitura = false;

        foreach (var atributo in saude.AtributosAta)
        {
            switch (atributo.Id)
            {
                case SmartAta.IdSetoresPendentes when atributo.Bruto > 0:
                    temLeitura = true;
                    achados.Add(new Achado("Setores pendentes", atributo.Interpretacao, "SMART", Critico: true, Aviso: false));
                    break;
                case SmartAta.IdSetoresPendentes:
                    temLeitura = true;
                    break;
                case SmartAta.IdErrosNaoCorrigiveis when atributo.Bruto > 0:
                    temLeitura = true;
                    achados.Add(new Achado("Erros não corrigíveis", atributo.Interpretacao, "SMART", Critico: true, Aviso: false));
                    break;
                case SmartAta.IdSetoresRealocados when atributo.Bruto > 0:
                    temLeitura = true;
                    achados.Add(new Achado("Setores realocados", atributo.Interpretacao, "SMART", Critico: false, Aviso: true));
                    break;
                case SmartAta.IdSetoresRealocados:
                    temLeitura = true;
                    break;
                case SmartAta.IdTaxaErroLeitura when atributo.Valor < 50:
                    temLeitura = true;
                    achados.Add(new Achado("Taxa de erro de leitura", atributo.Interpretacao, "SMART", Critico: false, Aviso: true));
                    break;
                default:
                    temLeitura = true;
                    break;
            }
        }

        if (saude.Nvme is { } nvme)
        {
            temLeitura = true;
            if (nvme.AvisoCritico.Disponivel && (nvme.AvisoCritico.Valor & 0x01) != 0)
            {
                achados.Add(new Achado("Aviso crítico NVMe", "O controlador prevê falha.", "NVMe", Critico: true, Aviso: false));
            }

            if (nvme.ErrosIntegridadeMidia.Disponivel && nvme.ErrosIntegridadeMidia.Valor > 0)
            {
                achados.Add(new Achado(
                    "Erros de integridade",
                    nvme.ErrosIntegridadeMidia.Valor + " erro(s) de mídia registrado(s).",
                    "NVMe",
                    Critico: true,
                    Aviso: false));
            }

            if (nvme.PercentualVidaUsada.Disponivel)
            {
                var usado = nvme.PercentualVidaUsada.Valor;
                if (usado >= 90)
                {
                    achados.Add(new Achado("Vida útil consumida", FormatacaoBr.Percentual(usado, 0), "NVMe", Critico: true, Aviso: false));
                }
                else if (usado >= 70)
                {
                    achados.Add(new Achado("Vida útil consumida", FormatacaoBr.Percentual(usado, 0), "NVMe", Critico: false, Aviso: true));
                }
            }
        }

        if (saude.PrevisaoFalhaWindows is { } preveFalha)
        {
            temLeitura = true;
            if (preveFalha)
            {
                achados.Add(new Achado(
                    "Previsão de falha (Windows)",
                    "O Windows indica que este disco deve falhar. Faça backup imediatamente.",
                    "Windows",
                    Critico: true,
                    Aviso: false));
            }
        }

        if (saude.SaudavelSegundoWindows is { } saudavelWindows)
        {
            temLeitura = true;
            if (!saudavelWindows)
            {
                achados.Add(new Achado(
                    "Estado de saúde (Windows)",
                    "A API de armazenamento do Windows não considera este disco saudável.",
                    "Windows",
                    Critico: false,
                    Aviso: true));
            }
        }

        if (saude.Confiabilidade is { } confiabilidade)
        {
            if (confiabilidade.ErrosLeituraNaoCorrigidos.Disponivel && confiabilidade.ErrosLeituraNaoCorrigidos.Valor > 0)
            {
                temLeitura = true;
                achados.Add(new Achado(
                    "Erros de leitura (Windows)",
                    confiabilidade.ErrosLeituraNaoCorrigidos.Valor + " erro(s) não corrigido(s).",
                    "Windows",
                    Critico: true,
                    Aviso: false));
            }

            if (confiabilidade.DesgastePercentual.Disponivel && confiabilidade.DesgastePercentual.Valor >= 90)
            {
                temLeitura = true;
                achados.Add(new Achado("Desgaste (Windows)", FormatacaoBr.Percentual(confiabilidade.DesgastePercentual.Valor, 0), "Windows", Critico: false, Aviso: true));
            }
        }

        return (achados, temLeitura);
    }
}
