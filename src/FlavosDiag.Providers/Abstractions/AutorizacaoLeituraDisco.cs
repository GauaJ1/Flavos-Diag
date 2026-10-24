namespace FlavosDiag.Providers.Abstractions;

/// <summary>
/// Autorização do usuário para ler o disco por pass-through. Enquanto não autorizado, nenhum disco é
/// aberto com acesso de escrita (<c>docs/DECISOES.md</c> D78).
/// </summary>
/// <remarks>
/// O contrato é "falha fechada": sem autorização registrada, a leitura do disco não acontece. A
/// interface deixa o provider independente de WPF; quem fala com o usuário é a camada de aplicação.
/// </remarks>
public interface IAutorizacaoLeituraDisco
{
    /// <summary>Verdadeiro somente depois de o usuário aceitar o aviso de leitura de disco.</summary>
    bool Autorizada { get; }

    /// <summary>Registra a resposta do usuário (verdadeiro = aceitou a leitura).</summary>
    void Registrar(bool aceitou);
}

/// <summary>
/// Implementação em memória: começa <b>não autorizada</b> e só passa a autorizar com um aceite explícito.
/// </summary>
public sealed class AutorizacaoLeituraDisco : IAutorizacaoLeituraDisco
{
    private int _autorizada;

    public bool Autorizada => Volatile.Read(ref _autorizada) == 1;

    public void Registrar(bool aceitou) => Volatile.Write(ref _autorizada, aceitou ? 1 : 0);
}
