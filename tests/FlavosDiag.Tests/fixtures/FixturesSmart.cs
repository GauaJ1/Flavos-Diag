using System.Text;

namespace FlavosDiag.Tests.fixtures;

/// <summary>
/// Gera blocos SMART de 512 bytes no formato da especificação (ATA/ATAPI-8) para os testes.
/// Nada aqui vem de equipamento real: os valores são escolhidos para exercitar cada regra.
/// </summary>
public static class FixturesSmart
{
    /// <summary>Bloco padrão de um SSD saudável (sem setores problemáticos).</summary>
    public static byte[] SsdSaudavel()
    {
        var dados = new byte[512];
        dados[0] = 0x10;
        dados[1] = 0x00;

        EscreverAtributo(dados, 0, 0x05, valor: 100, pior: 100, limite: 10, bruto: 0);
        EscreverAtributo(dados, 1, 0x09, valor: 99, pior: 99, limite: 0, bruto: 1200);
        EscreverAtributo(dados, 2, 0xC2, valor: 68, pior: 68, limite: 0, bruto: 32);
        EscreverAtributo(dados, 3, 0xC5, valor: 100, pior: 100, limite: 0, bruto: 0);
        EscreverAtributo(dados, 4, 0xC6, valor: 100, pior: 100, limite: 0, bruto: 0);
        EscreverAtributo(dados, 5, 0x01, valor: 100, pior: 100, limite: 51, bruto: 0);
        return dados;
    }

    /// <summary>HDD com setores realocados (desgaste da mídia).</summary>
    public static byte[] HddComSetoresRealocados()
    {
        var dados = SsdSaudavel();
        EscreverAtributo(dados, 0, 0x05, valor: 90, pior: 90, limite: 10, bruto: 24);
        EscreverAtributo(dados, 2, 0xC2, valor: 55, pior: 45, limite: 0, bruto: 45);
        return dados;
    }

    /// <summary>Disco com setores pendentes e erros não corrigíveis (falha).</summary>
    public static byte[] DiscoComSetoresPendentes()
    {
        var dados = HddComSetoresRealocados();
        EscreverAtributo(dados, 3, 0xC5, valor: 80, pior: 80, limite: 0, bruto: 8);
        EscreverAtributo(dados, 4, 0xC6, valor: 90, pior: 90, limite: 0, bruto: 2);
        return dados;
    }

    /// <summary>Bloco vazio (nenhum atributo) — deve ser tratado como "sem dados".</summary>
    public static byte[] Vazio() => new byte[512];

    /// <summary>Log de saúde NVMe de 512 bytes com desgaste alto e erros de mídia.</summary>
    public static byte[] NvmeDesgastado()
    {
        var dados = new byte[512];
        dados[0] = 0x00;
        BitConverter.GetBytes((ushort)(45 + 273)).CopyTo(dados, 1);
        dados[3] = 10;
        dados[4] = 5;
        dados[5] = 92;
        BitConverter.GetBytes(1_000_000UL).CopyTo(dados, 32);
        BitConverter.GetBytes(2_000_000UL).CopyTo(dados, 48);
        BitConverter.GetBytes(500UL).CopyTo(dados, 112);
        BitConverter.GetBytes(8000UL).CopyTo(dados, 128);
        BitConverter.GetBytes(3UL).CopyTo(dados, 160);
        return dados;
    }

    /// <summary>Log de saúde NVMe saudável.</summary>
    public static byte[] NvmeSaudavel()
    {
        var dados = new byte[512];
        BitConverter.GetBytes((ushort)(38 + 273)).CopyTo(dados, 1);
        dados[3] = 100;
        dados[4] = 10;
        dados[5] = 3;
        BitConverter.GetBytes(500UL).CopyTo(dados, 112);
        BitConverter.GetBytes(600UL).CopyTo(dados, 128);
        return dados;
    }

    /// <summary>XML mínimo do relatório de bateria do Windows (formato do arquivo gerado pelo powercfg).</summary>
    public static string RelatorioBateriaSaudavel() => Relatorio(designMwh: 50000, fullMwh: 47000, ciclos: 120);

    public static string RelatorioBateriaDegradada() => Relatorio(designMwh: 50000, fullMwh: 22000, ciclos: 900);

    public static string RelatorioBateriaSemCiclos() => Relatorio(designMwh: 40000, fullMwh: 39000, ciclos: null);

    private static string Relatorio(int designMwh, int fullMwh, int? ciclos)
    {
        var ciclo = ciclos is null ? string.Empty : $"      <CycleCount>{ciclos}</CycleCount>{Environment.NewLine}";
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.AppendLine("<BatteryReport>");
        xml.AppendLine("  <Batteries>");
        xml.AppendLine("    <Battery>");
        xml.AppendLine("      <Id>BAT0</Id>");
        xml.AppendLine("      <Manufacturer>Fabricante de Teste</Manufacturer>");
        xml.AppendLine("      <SerialNumber>TESTE-0001</SerialNumber>");
        xml.AppendLine($"      <DesignCapacity>{designMwh}</DesignCapacity>");
        xml.AppendLine($"      <FullChargeCapacity>{fullMwh}</FullChargeCapacity>");
        xml.Append(ciclo);
        xml.AppendLine("    </Battery>");
        xml.AppendLine("  </Batteries>");
        xml.AppendLine("</BatteryReport>");
        return xml.ToString();
    }

    private static void EscreverAtributo(byte[] dados, int indice, int id, int valor, int pior, int limite, long bruto)
    {
        var baseOff = 2 + (indice * 12);
        dados[baseOff] = (byte)id;
        dados[baseOff + 1] = 0x00;
        dados[baseOff + 2] = (byte)limite;
        dados[baseOff + 3] = (byte)valor;
        dados[baseOff + 4] = (byte)pior;
        dados[baseOff + 5] = (byte)(bruto & 0xFF);
        dados[baseOff + 6] = (byte)((bruto >> 8) & 0xFF);
        dados[baseOff + 7] = (byte)((bruto >> 16) & 0xFF);
        dados[baseOff + 8] = 0x00;
        dados[baseOff + 9] = 0x00;
        dados[baseOff + 10] = (byte)((bruto >> 24) & 0xFF);
        dados[baseOff + 11] = (byte)((bruto >> 32) & 0xFF);
    }
}
