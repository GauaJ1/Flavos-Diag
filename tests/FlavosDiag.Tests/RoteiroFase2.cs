using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;
using FlavosDiag.Providers.Bateria;
using FlavosDiag.Providers.Discos;
using FlavosDiag.Providers.Rede;
using FlavosDiag.Providers.Wmi;

namespace FlavosDiag.Tests;

/// <summary>
/// Verificação manual/assistida da Fase 2 com hardware real: discos (identificação + saúde),
/// bateria e rede. Não roda na suíte padrão.
/// Uso: <c>dotnet exec FlavosDiag.Tests.dll --roteiro-fase2</c>.
/// </summary>
public static class RoteiroFase2
{
    public static async Task<int> ExecutarAsync(string pastaSaida)
    {
        Directory.CreateDirectory(pastaSaida);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var linhas = new List<string>();
        void Log(string texto)
        {
            Console.WriteLine(texto);
            linhas.Add(texto);
        }

        Log("=== DISCOS (identificação + SMART/NVMe/confiabilidade) ===");
        var discos = new DiscosWindowsProvider();
        try
        {
            var lista = await discos.ListarAsync(cts.Token);
            Log($"Discos encontrados: {lista.Count}");
            foreach (var disco in lista)
            {
                Log($"  [{disco.Numero}] {Texto(disco.Modelo)} | FW {Texto(disco.Firmware)} | {Texto(disco.Barramento)} | {Texto(disco.Midia)} | {FormatacaoBr.Bytes(disco.TamanhoBytes.Valor)}");
                Log($"      série: {Mascaramento.Mascarar(Texto(disco.NumeroSerie))}");
                Log($"      fontes de saúde: {(disco.Saude.Fontes.Count == 0 ? "NENHUMA" : string.Join(", ", disco.Saude.Fontes))}");
                foreach (var atributo in disco.Saude.AtributosAta)
                {
                    Log($"      SMART {atributo.Id:X2} {atributo.Nome}: valor {atributo.Valor}, pior {atributo.Pior}, limite {atributo.Limite}, bruto {atributo.Bruto} — {atributo.Interpretacao}");
                }

                if (disco.Saude.Nvme is { } nvme)
                {
                    Log($"      NVMe: {(nvme.TemperaturaCelsius.Disponivel ? nvme.TemperaturaCelsius.Valor + " °C" : "sem temperatura — " + nvme.TemperaturaCelsius.Causa)} | vida usada {nvme.PercentualVidaUsada.Valor}% | escrito {nvme.DadosEscritosGb.Valor} GB | horas {nvme.HorasLigado.Valor} | erros de mídia {nvme.ErrosIntegridadeMidia.Valor}");
                }

                if (disco.Saude.Confiabilidade is { } conf)
                {
                    Log($"      Windows: desgaste {conf.DesgastePercentual.Valor}% | temperatura {conf.TemperaturaCelsius.Valor} °C | horas {conf.HorasLigado.Valor}");
                }

                foreach (var limitacao in disco.Saude.Limitacoes)
                {
                    Log($"      LIMITAÇÃO — {limitacao.Item}: {limitacao.Motivo}");
                }

                var (achados, temLeitura) = AvaliadorSaudeDisco.Avaliar(disco.Saude);
                Log($"      avaliação: {(temLeitura ? "leitura presente" : "SEM LEITURA → Inconclusivo")}, {achados.Count} achado(s)");
                foreach (var achado in achados)
                {
                    Log($"        * {(achado.Critico ? "CRÍTICO" : "aviso")} {achado.Titulo}: {achado.Valor}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"FALHA ao listar discos: {ex.Message}");
        }

        Log(string.Empty);
        Log("=== BATERIA ===");
        var bateria = new BateriaWindowsProvider(new WmiFonte());
        try
        {
            var info = await bateria.LerAsync(cts.Token);
            Log($"  presente: {(info.Presente ? "sim" : "não")}");
            Log($"  nome: {Texto(info.Nome)}");
            Log($"  projeto: {info.CapacidadeProjetoMwh.Valor} mWh | atual: {info.CapacidadeAtualMwh.Valor} mWh | saúde: {info.SaudePercentual.Valor}%");
            Log($"  ciclos: {info.Ciclos.Valor} | estado: {Texto(info.Estado)}");
            foreach (var limitacao in info.Limitacoes)
            {
                Log($"  LIMITAÇÃO — {limitacao.Item}: {limitacao.Motivo}");
            }
        }
        catch (Exception ex)
        {
            Log($"FALHA ao ler bateria: {ex.Message}");
        }

        Log(string.Empty);
        Log("=== REDE ===");
        var rede = new RedeWindowsProvider();
        try
        {
            var info = await rede.LerAsync(new[] { "8.8.8.8" }, "www.microsoft.com", cts.Token);
            Log($"  adaptadores: {info.Adaptadores.Count}");
            foreach (var adaptador in info.Adaptadores)
            {
                Log($"    - {Texto(adaptador.Descricao)} | {adaptador.Tipo.Valor} | ativo {adaptador.Ativo} | IP {Texto(adaptador.EnderecoIp)} | GW {Texto(adaptador.Gateway)} | {FormatacaoBr.VelocidadeLink(adaptador.VelocidadeBitsPorSegundo.Valor)}");
            }

            Log($"  gateway: {info.Gateway.Valor} | latência {info.LatenciaGateway.Valor?.LatenciaMediaMs} ms");
            foreach (var alvo in info.Alvos)
            {
                Log($"  alvo {alvo.Alvo}: {alvo.LatenciaMediaMs} ms (perda {alvo.PerdadePercentualUnica()}%)");
            }

            Log($"  DNS {info.Dns.Valor?.NomeConsultado}: resolvido {info.Dns.Valor?.Resolvido} em {info.Dns.Valor?.LatenciaMs} ms");
            Log($"  Wi-Fi: {Texto(info.Wifi.Ssid)}");
            foreach (var limitacao in info.Limitacoes)
            {
                Log($"  LIMITAÇÃO — {limitacao.Item}: {limitacao.Motivo}");
            }
        }
        catch (Exception ex)
        {
            Log($"FALHA ao ler rede: {ex.Message}");
        }

        var arquivo = Path.Combine(pastaSaida, "roteiro-fase2.txt");
        await File.WriteAllTextAsync(arquivo, string.Join(Environment.NewLine, linhas), cts.Token);
        Console.WriteLine();
        Console.WriteLine($"Saída gravada em: {arquivo}");
        return 0;
    }

    private static string Texto<T>(Leitura<T> leitura) => leitura.Disponivel
        ? leitura.Valor?.ToString() ?? "(vazio)"
        : $"AUSENTE ({leitura.Estado}): {leitura.Causa}";
}

internal static class ExtensaoLatencia
{
    public static double PerdadePercentualUnica(this ResultadoLatencia latencia) => latencia.PerdaPercentual;
}
