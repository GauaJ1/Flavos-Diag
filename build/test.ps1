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

# O SDK pode estar numa instalação por usuário que não está no PATH do processo que chama este script.
# Resolvemos o dotnet explicitamente para o script funcionar de qualquer shell.
$dotnet = $null
$candidatos = @(
    (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe')
) | Where-Object { $_ -and (Test-Path $_) }
foreach ($candidato in $candidatos) {
    $versoes = & $candidato --list-sdks 2>$null
    if ($versoes) { $dotnet = $candidato; break }
}
if (-not $dotnet) {
    Write-Host 'FALHA: nenhum SDK .NET encontrado (procurei no PATH, em %LOCALAPPDATA%\Microsoft\dotnet e em %ProgramFiles%\dotnet).' -ForegroundColor Red
    exit 1
}
Write-Host "dotnet: $dotnet"

if (-not $SemBuild) {
    # Cadeia de suprimentos: restaura travado no packages.lock.json versionado.
    # Se alguma versão de pacote mudar sem atualizar o lock, a restauração falha aqui.
    & $dotnet restore $projeto --locked-mode --nologo -v m
    $codigoRestore = $LASTEXITCODE
    if ($codigoRestore -ne 0) {
        Write-Host 'FALHA: a restauração travada (--locked-mode) falhou. Rode "dotnet restore" sem --locked-mode e revise o packages.lock.json.' -ForegroundColor Red
        exit $codigoRestore
    }

    & $dotnet build $projeto -c $Configuracao --nologo -v m --no-restore
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

$linhas = & $dotnet exec $dll --runner --saida $saida 2>&1
$linhas | ForEach-Object { $_.ToString() } | Set-Content -Path $resumo -Encoding UTF8
$linhas | ForEach-Object { $_.ToString() }
$codigo = $LASTEXITCODE
if ($null -eq $codigo) { $codigo = 0 }
Write-Host ""
Write-Host "Sumário salvo em $resumo"
exit $codigo
