namespace FlavosDiag.Workloads;

/// <summary>Resultado de um bloco de trabalho com verificação de cálculo.</summary>
public readonly record struct ResultadoBloco(long Unidades, ulong Checksum, bool Verificado);

/// <summary>
/// Carga de CPU original e determinística. Cada execução produz um checksum; o mesmo trabalho
/// reexecutado deve produzir o MESMO checksum — diferença = erro de cálculo.
/// </summary>
public interface ICargaCpu
{
    string Id { get; }

    string Nome { get; }

    /// <summary>Prepara buffers (custo não contabilizado na medição).</summary>
    void Preparar(int tamanhoTrabalho, ulong semente);

    /// <summary>Executa um bloco de trabalho e devolve unidades concluídas e checksum.</summary>
    ResultadoBloco ExecutarBloco(int tamanhoTrabalho);
}
