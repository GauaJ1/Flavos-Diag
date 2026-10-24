using FlavosDiag.Core.Abstractions;
using System.ComponentModel;
using System.Diagnostics;

namespace FlavosDiag.Providers.Abstractions;

/// <summary>
/// Elevação on-demand: reabre o próprio executável com "runas" (UAC). Não instala drivers nem altera sistema.
/// </summary>
public sealed class ElevacaoService : IElevacaoService
{
    private readonly IAmbiente _ambiente;

    public ElevacaoService(IAmbiente ambiente)
    {
        _ambiente = ambiente;
    }

    public bool Elevado => _ambiente.EhAdministrador;

    public string ExplicacaoElevacao =>
        "Alguns dados só podem ser lidos com permissão de administrador: sensores internos de temperatura e log de saúde de discos NVMe. " +
        "O Flavos Diag reabre com elevação (janela do Windows) apenas para ler esses dados. Nada é instalado e nenhuma configuração é alterada. " +
        "Sem elevação, essas categorias ficam \"Inconclusivo\" com a causa registrada.";

    public Task<bool> SolicitarAsync(CancellationToken cancelamento = default)
    {
        if (Elevado)
        {
            return Task.FromResult(true);
        }

        try
        {
            var caminho = Environment.ProcessPath;
            if (string.IsNullOrEmpty(caminho))
            {
                return Task.FromResult(false);
            }

            var info = new ProcessStartInfo
            {
                FileName = caminho,
                Arguments = "--elevado",
                UseShellExecute = true,
                Verb = "runas",
            };
            Process.Start(info);
            return Task.FromResult(true);
        }
        catch (Win32Exception)
        {
            // Usuário recusou o UAC — segue em modo reduzido.
            return Task.FromResult(false);
        }
    }
}
