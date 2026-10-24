param(
    [switch]$SemBuild,
    [string]$Configuracao = 'Debug'
)

# Executa a suíte automatizada do Flavos Diag.
# Fluxo 1 (padrão): `dotnet test`.
# Fluxo 2 (usado quando o host do vstest não consegue abrir o processo de teste — alguns ambientes
# corporativos bloqueiam isso): executa o runner próprio do assembly de testes, que gera TRX e
# sumário em artifacts/.

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$projeto = Join-Path $raiz 'tests\FlavosDiag.Tests\FlavosDiag.Tests.csproj'
$artefatos = Join-Path $raiz 'artifacts'
New-Item -ItemType Directory -Force $artefatos | Out-Null
$saida = Join-Path $artefatos 'testes.trx'
$resumo = Join-Path $artefatos 'testes.txt'

if (-not $SemBuild) {
    & dotnet build $projeto -c $Configuracao --nologo -v m
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'FALHA: a compilação do projeto de testes falhou.' -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

$dll = Join-Path $raiz "tests\FlavosDiag.Tests\bin\$Configuracao\net8.0-windows\FlavosDiag.Tests.dll"
if (-not (Test-Path $dll)) {
    Write-Host "FALHA: assembly de testes não encontrado em $dll" -ForegroundColor Red
    exit 1
}

$linhas = & dotnet exec $dll --runner --saida $saida 2>&1
$linhas | ForEach-Object { $_.ToString() } | Set-Content -Path $resumo -Encoding UTF8
$linhas | ForEach-Object { $_.ToString() }
$codigo = $LASTEXITCODE
if ($null -eq $codigo) { $codigo = 0 }
Write-Host ""
Write-Host "Sumário salvo em $resumo"
exit $codigo
