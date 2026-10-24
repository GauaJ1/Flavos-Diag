using System.Runtime.InteropServices;
using FlavosDiag.Core;
using FlavosDiag.Core.Modelos;

namespace FlavosDiag.Providers.Nativo;

/// <summary>
/// Interpretação do bloco SMART de 512 bytes (ATA/ATAPI-8, seção de atributos SMART).
/// Função pura e testável: recebe os bytes, devolve os atributos interpretados. Nada aqui depende
/// de outro programa — os IDs e significados são os da especificação pública.
/// </summary>
public static class SmartAta
{
    /// <summary>IDs de atributo padronizados pela especificação que importam para o veredito.</summary>
    public const int IdTaxaErroLeitura = 0x01;

    public const int IdTemperatura = 0xC2;

    public const int IdSetoresRealocados = 0x05;

    public const int IdHorasLigado = 0x09;

    public const int IdCiclosLigarDesligar = 0x0C;

    public const int IdEventosRealocacao = 0xC4;

    public const int IdSetoresPendentes = 0xC5;

    public const int IdErrosNaoCorrigiveis = 0xC6;

    public const int IdTemperaturaAlternativa = 0xBE;

    /// <summary>
    /// Converte os 512 bytes do bloco SMART em atributos. Bloco inválido devolve lista vazia —
    /// o chamador trata "lista vazia" como leitura sem dados, nunca como disco saudável.
    /// </summary>
    public static IReadOnlyList<AtributoSmart> Interpretar(byte[] dados)
    {
        if (dados is null || dados.Length < 512)
        {
            return Array.Empty<AtributoSmart>();
        }

        var lista = new List<AtributoSmart>(24);
        for (var i = 0; i < 30; i++)
        {
            var baseOff = 2 + (i * 12);
            if (baseOff + 12 > 512)
            {
                break;
            }

            var id = dados[baseOff];
            if (id == 0)
            {
                continue;
            }

            var valor = dados[baseOff + 3];
            var pior = dados[baseOff + 4];
            var bruto = (long)dados[baseOff + 5]
                        | ((long)dados[baseOff + 6] << 8)
                        | ((long)dados[baseOff + 7] << 16)
                        | ((long)dados[baseOff + 10] << 24)
                        | ((long)dados[baseOff + 11] << 32);

            lista.Add(new AtributoSmart
            {
                Id = id,
                Nome = Nome(id),
                Valor = valor,
                Pior = pior,
                Limite = dados[baseOff + 2],
                Bruto = bruto,
                Interpretacao = InterpretarValor(id, bruto, valor),
                // Atributo de temperatura presente no bloco mas sem leitura aproveitável: continua listado
                // (o técnico vê que o sensor existe), porém marcado, para nenhum veredito usá-lo.
                LeituraIndisponivel = EhAtributoTemperatura(id) && !TemperaturaValida(id, bruto),
            });
        }

        return lista;
    }

    /// <summary>Nome em português do atributo; IDs desconhecidos recebem rótulo neutro.</summary>
    public static string Nome(int id) => id switch
    {
        IdTaxaErroLeitura => "Taxa de erro de leitura",
        IdSetoresRealocados => "Setores realocados",
        IdHorasLigado => "Horas de uso",
        IdCiclosLigarDesligar => "Ciclos de ligar/desligar",
        IdTemperatura => "Temperatura",
        IdTemperaturaAlternativa => "Temperatura (sensor alternativo)",
        IdEventosRealocacao => "Eventos de realocação",
        IdSetoresPendentes => "Setores pendentes",
        IdErrosNaoCorrigiveis => "Erros não corrigíveis",
        _ => "Atributo " + id,
    };

    /// <summary>Texto curto do que o valor significa, no vocabulário do técnico.</summary>
    public static string InterpretarValor(int id, long bruto, int valorNormalizado) => id switch
    {
        IdSetoresRealocados when bruto > 0 => $"{bruto} setor(es) realocado(s) — indica desgaste da mídia.",
        IdSetoresRealocados => "Nenhum setor realocado.",
        IdSetoresPendentes when bruto > 0 => $"{bruto} setor(es) pendente(s) de remapeamento — risco de perda de dados.",
        IdSetoresPendentes => "Nenhum setor pendente.",
        IdErrosNaoCorrigiveis when bruto > 0 => $"{bruto} erro(s) não corrigível(is) registrado(s).",
        IdErrosNaoCorrigiveis => "Nenhum erro não corrigível.",
        // Temperatura: o bruto pode vir 0, 255 ou fora de faixa. Escrever "0 °C" faria o técnico acreditar
        // numa medição, então a interpretação declara a ausência com a causa.
        IdTemperatura or IdTemperaturaAlternativa when TemperaturaValida(id, bruto) => $"{bruto} °C",
        IdTemperatura or IdTemperaturaAlternativa =>
            "leitura indisponível — " + Temperatura.CausaSentinelaOuForaDaFaixa + $"; valor bruto recebido: {bruto}",
        IdHorasLigado => $"{bruto} hora(s) ligado",
        IdCiclosLigarDesligar => $"{bruto} ciclo(s)",
        _ => $"Valor normalizado {valorNormalizado}, bruto {bruto}",
    };

    /// <summary>Indica se o atributo é um dos sensores de temperatura (0xC2 / 194 ou 0xBE / 190).</summary>
    public static bool EhAtributoTemperatura(int id) => id is IdTemperatura or IdTemperaturaAlternativa;

    /// <summary>
    /// Diz se o valor bruto de um atributo de temperatura é uma leitura real (1–120 °C) pela regra única
    /// de <see cref="Temperatura"/>. Recebe o id: atributos que não são de temperatura nunca são "válidos"
    /// como temperatura.
    /// </summary>
    public static bool TemperaturaValida(int id, long bruto) =>
        EhAtributoTemperatura(id) && Temperatura.TemperaturaValida((int)bruto);

    /// <summary>
    /// Interpretação do atributo de temperatura como <see cref="Leitura{T}"/>: valor real quando dentro da
    /// faixa, ausência com causa em pt-BR quando o bruto é sentinela (0/255) ou fora de 1–120 °C.
    /// </summary>
    /// <param name="id">ID do atributo (só 0xC2/0xBE são temperaturas).</param>
    /// <param name="bruto">Valor bruto de seis bytes lido do bloco SMART.</param>
    public static Leitura<int> LerTemperatura(int id, long bruto)
    {
        if (!EhAtributoTemperatura(id))
        {
            return Leitura<int>.NaoSuportado("O atributo " + id + " não é uma temperatura.");
        }

        return Temperatura.InterpretarTemperatura((int)bruto, id == IdTemperatura ? "SMART ATA 0xC2" : "SMART ATA 0xBE");
    }

    /// <summary>
    /// Temperatura do disco a partir dos atributos SMART: <c>0xC2</c> (principal) tem precedência sobre
    /// <c>0xBE</c> (alternativo). Quando aparece um atributo de temperatura sem leitura aproveitável, a
    /// ausência sai com a causa dele — nunca com <c>0</c>, que pareceria medição.
    /// </summary>
    public static Leitura<int> LerTemperatura(IReadOnlyList<AtributoSmart> atributos)
    {
        Leitura<int>? ausencia = null;
        Leitura<int>? alternativa = null;
        for (var i = 0; i < atributos.Count; i++)
        {
            var atributo = atributos[i];
            if (!EhAtributoTemperatura(atributo.Id))
            {
                continue;
            }

            var leitura = LerTemperatura(atributo.Id, atributo.Bruto);
            if (leitura.Disponivel)
            {
                // O sensor principal manda: só é substituído pelo alternativo se ele próprio faltar.
                if (atributo.Id == IdTemperatura)
                {
                    return leitura;
                }

                alternativa ??= leitura;
            }
            else
            {
                // Um sensor válido sempre prevalece sobre o outro que está com valor inválido.
                ausencia ??= leitura;
            }
        }

        return alternativa ?? ausencia
            ?? Leitura<int>.NaoSuportado("O disco não expõe um atributo de temperatura (0xC2/0xBE).", "SMART ATA");
    }

    /// <summary>
    /// Lê o bloco SMART (comando ATA SMART READ DATA) por <see cref="RequisicaoDisco"/>, o único caminho
    /// que abre o disco com acesso de escrita e monta o task file a partir da lista de permitidos.
    /// A falha devolve o motivo legível em pt-BR, como o resto dos providers.
    /// </summary>
    /// <remarks>
    /// O handle precisa ser aberto com <c>GENERIC_WRITE</c> mesmo para um comando de leitura: o driver de
    /// armazenamento do Windows só repassa o pass-through ATA a handles com acesso de escrita (D31).
    /// Isso <b>não</b> escreve nada — o comando enviado é SMART READ DATA (0xD0) — mas, como o handle tem
    /// permissão de escrita, o comando passou a vir de uma lista fechada (D78): esta função não aceita
    /// byte de comando, só indica qual leitura quer.
    /// </remarks>
    /// <param name="numeroDisco">Índice do disco físico.</param>
    /// <param name="permissoes">Elevação e consentimento já conferidos pelo chamador.</param>
    public static LeituraSmartBruta LerSmartAta(int numeroDisco, RequisicaoDisco.Permissoes permissoes)
    {
        var resultado = RequisicaoDisco.Executar(numeroDisco, RequisicaoDisco.PedidoAta.LerAtributosSmart, permissoes);
        return new LeituraSmartBruta(resultado.Sucesso, resultado.Dados, resultado.Erro);
    }

    /// <summary>
    /// SMART predito pelo Windows (IOCTL_STORAGE_PREDICT_FAILURE). Funciona em muitos dispositivos
    /// USB e RAID onde o IOCTL ATA não passa; devolve apenas "prevê falha sim/não".
    /// </summary>
    /// <remarks>
    /// Não é pass-through: não envia comando ATA ao disco, só pergunta ao driver o que ele já sabe. Por
    /// isso abre o disco <b>somente leitura</b> e não depende de consentimento para leitura do disco.
    /// </remarks>
    public static LeituraSmartBruta LerPredicaoWindows(int numeroDisco)
    {
        using var dispositivo = RequisicaoDisco.AbrirSomenteLeitura(numeroDisco, out var erroAbertura);
        if (dispositivo is null || dispositivo.IsInvalid)
        {
            return new LeituraSmartBruta(false, null, erroAbertura);
        }

        var tamanhoSaida = Marshal.SizeOf<STORAGE_PREDICT_FAILURE_SAIDA>();
        var pontSaida = Marshal.AllocHGlobal(tamanhoSaida);
        try
        {
            var saida = STORAGE_PREDICT_FAILURE_SAIDA.Criar();
            Marshal.StructureToPtr(saida, pontSaida, false);

            // IOCTL_STORAGE_PREDICT_FAILURE não recebe buffer de entrada.
            var ok = ApiNativas.DeviceIoControl(
                dispositivo,
                ApiNativas.IOCTL_STORAGE_PREDICT_FAILURE,
                IntPtr.Zero,
                0,
                pontSaida,
                tamanhoSaida,
                out var retornados,
                IntPtr.Zero);

            if (!ok || retornados == 0)
            {
                return new LeituraSmartBruta(false, null, RequisicaoDisco.DescricaoErro(Marshal.GetLastWin32Error()));
            }

            var lido = Marshal.PtrToStructure<STORAGE_PREDICT_FAILURE_SAIDA>(pontSaida);
            var preveFalha = lido.PredictFailure == ApiNativas.PredictFailureIsFailurePredicted;
            var bloco = new byte[512];
            bloco[0] = preveFalha ? (byte)1 : (byte)0;
            return new LeituraSmartBruta(true, bloco, null);
        }
        finally
        {
            Marshal.FreeHGlobal(pontSaida);
        }
    }
}
