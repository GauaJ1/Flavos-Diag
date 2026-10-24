using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FlavosDiag.Core;
using FlavosDiag.Core.Abstractions;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Providers.Rede;

/// <summary>
/// Rede: adaptadores pelo próprio .NET (NetworkInterface), latência por ICMP, resolução DNS e,
/// quando o técnico configurar um servidor, teste de velocidade por download HTTP.
/// Sem alvo configurado não há teste — e isso é <b>Não se aplica</b>, não falha.
/// Nenhuma conexão é feita sem um endereço informado pelo usuário.
/// </summary>
public sealed class RedeWindowsProvider : IRedeProvider
{
    public string Nome => "Windows (adaptadores, ICMP e DNS)";

    public async Task<InfoRede> LerAsync(IReadOnlyList<string> alvos, string? nomeDns, CancellationToken cancelamento = default)
    {
        var limitacoes = new List<DadoAusente>();
        var adaptadores = ListarAdaptadores(limitacoes);

        // Preferimos o adaptador que realmente tem rota padrão (o resto é túnel/virtual ou sem link),
        // senão a latência do gateway sairia vazia mesmo com internet funcionando.
        var ativo = adaptadores.FirstOrDefault(a => a.Ativo && a.Gateway.Disponivel)
                    ?? adaptadores.FirstOrDefault(a => a.Ativo && a.EnderecoIp.Disponivel);
        var endereco = ativo?.EnderecoIp ?? Leitura<string>.NaoSuportado("Nenhum adaptador ativo com endereço IPv4.");
        var gateway = ativo?.Gateway ?? Leitura<string>.NaoSuportado("Roteador padrão não informado.");
        var dns = ativo?.ServidoresDns ?? Leitura<string>.NaoSuportado("Servidores DNS não informados.");

        if (!endereco.Disponivel)
        {
            limitacoes.Add(new DadoAusente("Endereço IP", endereco.Causa ?? "Sem adaptador ativo."));
        }

        var wifi = LerWifi(limitacoes);

        Leitura<ResultadoLatencia> latenciaGateway = string.IsNullOrWhiteSpace(gateway.Valor)
            ? Leitura<ResultadoLatencia>.NaoSuportado("Sem roteador padrão não há como medir a latência da rede local.")
            : await PingAsync(gateway.Valor!, 4, cancelamento).ConfigureAwait(false);

        var resultadosAlvos = new List<ResultadoLatencia>();
        foreach (var alvo in alvos.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).Take(4))
        {
            var latencia = await PingAsync(alvo.Trim(), 4, cancelamento).ConfigureAwait(false);
            if (latencia.Disponivel && latencia.Valor is { } valor)
            {
                resultadosAlvos.Add(valor);
            }
            else
            {
                limitacoes.Add(new DadoAusente("Latência para " + alvo, latencia.Causa ?? "Não respondeu."));
            }
        }

        Leitura<ResultadoDns> resultadoDns = string.IsNullOrWhiteSpace(nomeDns)
            ? Leitura<ResultadoDns>.NaoSuportado("Nenhum nome configurado para testar o DNS.")
            : await ResolverDnsAsync(nomeDns!, cancelamento).ConfigureAwait(false);

        return new InfoRede
        {
            Adaptadores = adaptadores,
            EnderecoIp = endereco,
            Gateway = gateway,
            ServidoresDns = dns,
            Wifi = wifi,
            LatenciaGateway = latenciaGateway,
            Alvos = resultadosAlvos,
            Dns = resultadoDns,
            ColetadoEm = DateTimeOffset.Now,
            Limitacoes = limitacoes,
        };
    }

    // --------------------------------------------------------------- adaptadores

    private static List<InfoAdaptadorRede> ListarAdaptadores(List<DadoAusente> limitacoes)
    {
        var lista = new List<InfoAdaptadorRede>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var props = nic.GetIPProperties();
                var ipv4 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
                var gateway = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
                var dns = props.DnsAddresses
                    .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                    .Select(d => d.ToString())
                    .ToList();
                var mac = nic.GetPhysicalAddress()?.ToString();
                var velocidade = nic.Speed;

                lista.Add(new InfoAdaptadorRede
                {
                    Nome = Leitura<string>.Ok(nic.Name, "NetworkInterface.Name"),
                    Descricao = Leitura<string>.Ok(nic.Description, "NetworkInterface.Description"),
                    Mac = string.IsNullOrWhiteSpace(mac)
                        ? Leitura<string>.NaoSuportado("Adaptador sem endereço físico.")
                        : Leitura<string>.Ok(mac, "NetworkInterface.GetPhysicalAddress"),
                    VelocidadeBitsPorSegundo = velocidade > 0
                        ? Leitura<long>.Ok(velocidade, "NetworkInterface.Speed")
                        : Leitura<long>.NaoSuportado("Velocidade do link não informada pelo driver."),
                    Tipo = Leitura<string>.Ok(nic.NetworkInterfaceType.ToString(), "NetworkInterface.NetworkInterfaceType"),
                    Ativo = nic.OperationalStatus == OperationalStatus.Up,
                    EnderecoIp = ipv4 is null
                        ? Leitura<string>.NaoSuportado("Adaptador sem endereço IPv4.")
                        : Leitura<string>.Ok(ipv4, "NetworkInterface.UnicastAddresses"),
                    Gateway = gateway is null
                        ? Leitura<string>.NaoSuportado("Adaptador sem roteador padrão.")
                        : Leitura<string>.Ok(gateway, "IPProperties.GatewayAddresses"),
                    ServidoresDns = dns.Count == 0
                        ? Leitura<string>.NaoSuportado("Adaptador sem servidores DNS.")
                        : Leitura<string>.Ok(string.Join(", ", dns), "IPProperties.DnsAddresses"),
                });
            }
        }
        catch (NetworkInformationException ex)
        {
            limitacoes.Add(new DadoAusente("Adaptadores de rede", "Falha ao listar as interfaces: " + ex.Message));
        }

        if (lista.Count == 0)
        {
            limitacoes.Add(new DadoAusente("Adaptadores de rede", "Nenhum adaptador físico encontrado."));
        }

        return lista;
    }

    // --------------------------------------------------------------- Wi-Fi

    private static StatusWifi LerWifi(List<DadoAusente> limitacoes)
    {
        var semSuporte = Leitura<string>.NaoSuportado("Esta máquina não tem adaptador Wi-Fi ativo.");
        try
        {
            // O cmdlet de Wi-Fi do próprio Windows é a fonte pública para SSID e intensidade do sinal.
            // powershell.exe do Windows PowerShell fica em System32\WindowsPowerShell\v1.0 (não em System32).
            var shell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(shell))
            {
                limitacoes.Add(new DadoAusente("Wi-Fi", "Windows PowerShell não encontrado nesta instalação."));
                return new StatusWifi
                {
                    Ssid = Leitura<string>.NaoSuportado("Consulta de Wi-Fi indisponível: Windows PowerShell ausente."),
                    QualidadePercentual = Leitura<int>.NaoSuportado("Consulta de Wi-Fi indisponível."),
                    ForcaDbm = Leitura<int>.NaoSuportado("Consulta de Wi-Fi indisponível."),
                };
            }

            var script = "Get-NetConnectionProfile -ErrorAction Stop | Select-Object -First 1 -ExpandProperty Name";
            var saida = Executar(shell, new[] { "-NoProfile", "-NonInteractive", "-Command", script }, TimeSpan.FromSeconds(15));
            var ssid = saida?.Trim();
            if (string.IsNullOrWhiteSpace(ssid))
            {
                return new StatusWifi
                {
                    Ssid = semSuporte,
                    QualidadePercentual = Leitura<int>.NaoSuportado("Sem conexão Wi-Fi ativa."),
                    ForcaDbm = Leitura<int>.NaoSuportado("Sem conexão Wi-Fi ativa."),
                };
            }

            return new StatusWifi
            {
                Ssid = Leitura<string>.Ok(ssid, "Get-NetConnectionProfile"),
                QualidadePercentual = Leitura<int>.NaoSuportado("Intensidade do sinal não consultada: exige o adaptador sem fio e permissão de rede."),
                ForcaDbm = Leitura<int>.NaoSuportado("Intensidade do sinal não consultada: exige o adaptador sem fio e permissão de rede."),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            limitacoes.Add(new DadoAusente("Wi-Fi", "Não foi possível consultar a rede sem fio: " + ex.Message));
            return new StatusWifi
            {
                Ssid = semSuporte,
                QualidadePercentual = Leitura<int>.NaoSuportado("Consulta de Wi-Fi indisponível."),
                ForcaDbm = Leitura<int>.NaoSuportado("Consulta de Wi-Fi indisponível."),
            };
        }
    }

    // --------------------------------------------------------------- ICMP

    /// <summary>Ping via ICMP usando a API do .NET (sem executar programa externo).</summary>
    public static async Task<Leitura<ResultadoLatencia>> PingAsync(string alvo, int tentativas, CancellationToken cancelamento)
    {
        try
        {
            using var ping = new Ping();
            var latencias = new List<double>(tentativas);
            var recebidos = 0;
            for (var i = 0; i < tentativas; i++)
            {
                cancelamento.ThrowIfCancellationRequested();
                try
                {
                    var resposta = await ping.SendPingAsync(alvo, 2000).ConfigureAwait(false);
                    if (resposta.Status == IPStatus.Success)
                    {
                        recebidos++;
                        latencias.Add(resposta.RoundtripTime);
                    }
                }
                catch (PingException)
                {
                    // tentativa perdida: entra na estatística de perda
                }
            }

            var perda = tentativas == 0 ? 100 : (tentativas - recebidos) * 100.0 / tentativas;
            if (recebidos == 0)
            {
                return Leitura<ResultadoLatencia>.NaoSuportado(
                    $"Nenhuma resposta de {alvo} (ICMP bloqueado por firewall ou destino inacessível).");
            }

            return Leitura<ResultadoLatencia>.Ok(new ResultadoLatencia
            {
                Alvo = alvo,
                IcMPRespondido = recebidos > 0,
                TcpRespondido = false,
                Enviados = tentativas,
                Recebidos = recebidos,
                PerdaPercentual = Math.Round(perda, 1),
                LatenciaMediaMs = Math.Round(latencias.Average(), 1),
                LatenciaMinimaMs = Math.Round(latencias.Min(), 1),
                LatenciaMaximaMs = Math.Round(latencias.Max(), 1),
            }, "ICMP");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is PingException or SocketException or ArgumentException)
        {
            return Leitura<ResultadoLatencia>.Falha("Falha ao medir latência de " + alvo + ": " + ex.Message);
        }
    }

    // --------------------------------------------------------------- DNS

    private static async Task<Leitura<ResultadoDns>> ResolverDnsAsync(string nome, CancellationToken cancelamento)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            var enderecos = await Dns.GetHostAddressesAsync(nome, cancelamento).ConfigureAwait(false);
            cronometro.Stop();
            if (enderecos.Length == 0)
            {
                return Leitura<ResultadoDns>.Falha("O DNS não devolveu endereços para " + nome + ".");
            }

            return Leitura<ResultadoDns>.Ok(new ResultadoDns
            {
                NomeConsultado = nome,
                Resolvido = true,
                Enderecos = enderecos.Select(e => e.ToString()).Distinct().Take(4).ToList(),
                LatenciaMs = Math.Round(cronometro.Elapsed.TotalMilliseconds, 1),
            }, "DNS do sistema");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException ex)
        {
            cronometro.Stop();
            return Leitura<ResultadoDns>.Ok(new ResultadoDns
            {
                NomeConsultado = nome,
                Resolvido = false,
                Enderecos = Array.Empty<string>(),
                LatenciaMs = Math.Round(cronometro.Elapsed.TotalMilliseconds, 1),
                Erro = ex.Message,
            }, "DNS do sistema");
        }
    }

    // --------------------------------------------------------------- velocidade

    /// <summary>
    /// Teste de velocidade contra um endereço informado pelo técnico: baixa até <paramref name="bytes"/>
    /// e mede a taxa. Sem endereço válido, recusa — nunca conecta a um servidor "embutido".
    /// </summary>
    public async Task<ResultadoVelocidadeRede> TestarVelocidadeAsync(string urlServidor, long bytes, CancellationToken cancelamento = default)
    {
        if (!Uri.TryCreate(urlServidor, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new ResultadoVelocidadeRede
            {
                Servidor = urlServidor,
                DownloadMbps = 0,
                BytesRecebidos = 0,
                Segundos = 0,
                Erro = "Informe um endereço http(s) válido do servidor de teste na aba Configurações.",
            };
        }

        using var cliente = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var cronometro = Stopwatch.StartNew();
        long recebidos = 0;
        var buffer = new byte[81920];
        try
        {
            using var resposta = await cliente
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancelamento)
                .ConfigureAwait(false);
            resposta.EnsureSuccessStatusCode();
            await using var fluxo = await resposta.Content.ReadAsStreamAsync(cancelamento).ConfigureAwait(false);
            int lidos;
            while (recebidos < bytes && (lidos = await fluxo.ReadAsync(buffer, cancelamento).ConfigureAwait(false)) > 0)
            {
                recebidos += lidos;
            }

            cronometro.Stop();
            var segundos = Math.Max(0.001, cronometro.Elapsed.TotalSeconds);
            return new ResultadoVelocidadeRede
            {
                Servidor = uri.ToString(),
                DownloadMbps = Math.Round(recebidos * 8 / 1_000_000.0 / segundos, 2),
                BytesRecebidos = recebidos,
                Segundos = Math.Round(segundos, 2),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new ResultadoVelocidadeRede
            {
                Servidor = uri.ToString(),
                DownloadMbps = 0,
                BytesRecebidos = recebidos,
                Segundos = Math.Round(cronometro.Elapsed.TotalSeconds, 2),
                Erro = "Falha ao baixar do servidor informado: " + ex.Message,
            };
        }
    }

    private static string? Executar(string caminhoExecutavel, IEnumerable<string> argumentos, TimeSpan limite)
    {
        var psi = new ProcessStartInfo(caminhoExecutavel)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argumento in argumentos)
        {
            psi.ArgumentList.Add(argumento);
        }

        using var processo = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar " + caminhoExecutavel);
        var saida = processo.StandardOutput.ReadToEndAsync();
        if (!processo.WaitForExit(limite))
        {
            processo.Kill(entireProcessTree: true);
            throw new InvalidOperationException(caminhoExecutavel + " excedeu o tempo limite.");
        }

        return saida.Result;
    }
}
