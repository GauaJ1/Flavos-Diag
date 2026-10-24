#Requires -Version 5.1
<#
.SYNOPSIS
    Gera o pacote portátil do Flavos Diag em dist/FlavosDiag-<versao>-win-x64/.

.DESCRIPTION
    Publica o aplicativo autocontido (não exige .NET instalado na máquina do técnico) e monta a pasta de
    distribuição com:
      - FlavosDiag.App.exe (e as dependências do lado do app)
      - licenses/  (texto das bibliotecas de terceiros efetivamente distribuídas)
      - LEIA-ME.txt (instruções e limitações honestas)

    A lista de licenças é conferida contra os pacotes realmente resolvidos: se um pacote novo aparecer no
    build e não estiver mapeado aqui, o script FALHA em vez de entregar o pacote sem o aviso legal.
#>
[CmdletBinding()]
param(
    [string] $Versao = "",
    [string] $Saida = "",
    [switch] $PularBuild
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

function Falhar([string] $mensagem) {
    Write-Host "[ERRO] $mensagem" -ForegroundColor Red
    exit 1
}

function Resolver-Dotnet {
    $noPath = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($noPath) { return $noPath.Source }

    $candidatos = @(
        (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
        (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe')
    )
    foreach ($c in $candidatos) {
        if (Test-Path $c) { return $c }
    }

    Falhar 'O SDK do .NET não foi encontrado. Instale o .NET 8 SDK e tente de novo.'
}

$dotnet = Resolver-Dotnet
Write-Host "dotnet: $dotnet"

if ([string]::IsNullOrWhiteSpace($Versao)) {
    $props = Join-Path $raiz 'Directory.Build.props'
    if (Test-Path $props) {
        $texto = Get-Content $props -Raw -Encoding UTF8
        if ($texto -match '<FlavosVersion>([^<]+)</FlavosVersion>') {
            $Versao = $Matches[1].Trim()
        }
    }
}

if ([string]::IsNullOrWhiteSpace($Versao)) {
    Falhar 'Não foi possível determinar a versão. Informe -Versao x.y.z.'
}

$pastaDist = if ($Saida) { $Saida } else { Join-Path $raiz ("dist\FlavosDiag-$Versao-win-x64") }
Write-Host "versão: $Versao"
Write-Host "destino: $pastaDist"

$projetoApp = Join-Path $raiz 'src\FlavosDiag.App\FlavosDiag.App.csproj'
if (-not (Test-Path $projetoApp)) { Falhar "Projeto do app não encontrado: $projetoApp" }

# ---- 1. Publicação ----
if (-not $PularBuild) {
    Write-Host 'Publicando (autocontido, win-x64)...'
    & $dotnet publish $projetoApp -c Release -r win-x64 --self-contained true `
        -p:Version=$Versao -p:AssemblyVersion="$Versao.0" -p:FileVersion="$Versao.0" `
        -o $pastaDist --nologo -v m
    if ($LASTEXITCODE -ne 0) { Falhar "A publicação falhou (código $LASTEXITCODE)." }
}

$exe = Join-Path $pastaDist 'FlavosDiag.App.exe'
if (-not (Test-Path $exe)) { Falhar "O executável não foi gerado em $pastaDist" }

# ---- 2. Pasta de licenças ----
$pastaLicencas = Join-Path $pastaDist 'licenses'
New-Item -ItemType Directory -Force -Path $pastaLicencas | Out-Null

# Mapa pacote -> (arquivo de licença no cache do NuGet, licença, aviso).
# Se um pacote distribuído não estiver mapeado, o script falha: é melhor parar do que entregar sem aviso.
$mapaLicencas = @{
    'communitytoolkit.mvvm'          = @{ Licenca = 'MIT'; Aviso = 'Copyright (c) .NET Foundation and Contributors' }
    'wpf-ui'                         = @{ Licenca = 'MIT'; Aviso = 'WPF-UI (MIT)' }
    'questpdf'                       = @{ Licenca = 'QuestPDF Community License'; Aviso = 'QuestPDF Community License v3.0 — ver THIRD-PARTY-NOTICES.txt' }
    'librehardwaremonitorlib'        = @{ Licenca = 'MPL-2.0'; Aviso = 'LibreHardwareMonitor (MPL-2.0) — fonte em https://github.com/LibreHardwareMonitor/LibreHardwareMonitor' }
    'diskinfotoolkit'                = @{ Licenca = 'MPL-2.0'; Aviso = 'DiskInfoToolkit (MPL-2.0)' }
    'ramspdtoolkit-ndd'            = @{ Licenca = 'MPL-2.0'; Aviso = 'RAMSPDToolkit (MPL-2.0)' }
    'hidsharp'                       = @{ Licenca = 'Apache-2.0'; Aviso = 'HidSharp (Apache-2.0)' }
    'system.management'              = @{ Licenca = 'MIT'; Aviso = 'System.Management (MIT, Microsoft)' }
    'vortice.direct3d11'             = @{ Licenca = 'MIT'; Aviso = 'Vortice.Direct3D11 (MIT, Amer Koleci)' }
    'vortice.dxgi'                   = @{ Licenca = 'MIT'; Aviso = 'Vortice.DXGI (MIT, Amer Koleci)' }
    'vortice.d3dcompiler'            = @{ Licenca = 'MIT'; Aviso = 'Vortice.D3DCompiler (MIT, Amer Koleci)' }
    'vortice.directx'                = @{ Licenca = 'MIT'; Aviso = 'Vortice.DirectX (MIT, Amer Koleci)' }
    'sharpgen.runtime'               = @{ Licenca = 'MIT'; Aviso = 'SharpGen.Runtime (MIT, Alexandre Mutel)' }
    'microsoft.windows.sdk.net.refs' = @{ Licenca = 'MIT'; Aviso = 'Windows SDK projections (MIT, Microsoft)' }
    'wpf-ui.abstractions'            = @{ Licenca = 'MIT'; Aviso = 'WPF-UI Abstractions (MIT)' }
    'vortice.mathematics'            = @{ Licenca = 'MIT'; Aviso = 'Vortice.Mathematics (MIT, Amer Koleci)' }
    'sharpgen.runtime.com'           = @{ Licenca = 'MIT'; Aviso = 'SharpGen.Runtime COM (MIT, Alexandre Mutel)' }
    'blacksharp.core'                = @{ Licenca = 'MIT'; Aviso = 'BlackSharp.Core (MIT)' }
    'mono.posix.netstandard'         = @{ Licenca = 'MIT'; Aviso = 'Mono.Posix.NETStandard (MIT)' }
    'system.codedom'                 = @{ Licenca = 'MIT'; Aviso = 'System.CodeDom (MIT, Microsoft)' }
    'system.io.pipelines'            = @{ Licenca = 'MIT'; Aviso = 'System.IO.Pipelines (MIT, Microsoft)' }
    'system.io.ports'                = @{ Licenca = 'MIT'; Aviso = 'System.IO.Ports (MIT, Microsoft)' }
    'system.text.encodings.web'      = @{ Licenca = 'MIT'; Aviso = 'System.Text.Encodings.Web (MIT, Microsoft)' }
    'system.text.json'               = @{ Licenca = 'MIT'; Aviso = 'System.Text.Json (MIT, Microsoft)' }
    'system.threading.accesscontrol' = @{ Licenca = 'MIT'; Aviso = 'System.Threading.AccessControl (MIT, Microsoft)' }
}

Write-Host 'Coletando licenças dos pacotes resolvidos...'
$projetoProviders = Join-Path $raiz 'src\FlavosDiag.Providers\FlavosDiag.Providers.csproj'
$projetoReports = Join-Path $raiz 'src\FlavosDiag.Reports\FlavosDiag.Reports.csproj'

$pacotes = @()
foreach ($proj in @($projetoApp, $projetoProviders, $projetoReports)) {
    if (-not (Test-Path $proj)) { continue }
    $json = & $dotnet list $proj package --include-transitive --format json 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $json) { continue }
    $dados = $json | ConvertFrom-Json
    foreach ($framework in $dados.projects[0].frameworks) {
        foreach ($p in $framework.topLevelPackages) { if ($p.id) { $pacotes += $p.id.ToLowerInvariant() } }
        foreach ($p in $framework.transitivePackages) { if ($p.id) { $pacotes += $p.id.ToLowerInvariant() } }
    }
}

$pacotes = $pacotes | Sort-Object -Unique
Write-Host ("pacotes resolvidos: " + ($pacotes -join ', '))

$naoMapeados = @()
foreach ($p in $pacotes) {
    $chave = $p
    if (-not $mapaLicencas.ContainsKey($chave)) { $naoMapeados += $chave }
}

# Pacotes que são parte do próprio .NET (não são redistribuição separada) não exigem entrada.
$doFramework = @('microsoft.netcore.app.host.win-x64', 'microsoft.windowsdesktop.app.win-x64', 'microsoft.netcore.app.runtime.win-x64')
# Pacotes de runtime nativo por plataforma do .NET (não são redistribuídos por nós nesta compilação win-x64).
$natiosDotnet = $pacotes | Where-Object { $_ -like 'runtime*' -and $_ -like '*system.io.ports*' }
$doFramework = $doFramework + $natiosDotnet
$naoMapeados = $naoMapeados | Where-Object { $doFramework -notcontains $_ }

if ($naoMapeados.Count -gt 0) {
    Falhar ("Pacotes sem aviso de licença mapeado: " + ($naoMapeados -join ', ') + ". Atualize `$mapaLicencas em build/publish.ps1.")
}

$notices = New-Object System.Collections.Generic.List[string]
$notices.Add("Flavos Diag $Versao — avisos de licenças de terceiros")
$notices.Add("")
$notices.Add("Este programa distribui as seguintes bibliotecas de código aberto (ou de licença comunitária).")
$notices.Add("Cada entrada indica o pacote, a licença e o aviso de copyright correspondente.")
$notices.Add("")

foreach ($p in $pacotes) {
    if ($doFramework -contains $p) { continue }
    $info = $mapaLicencas[$p]
    if (-not $info) { continue }
    $notices.Add("- $p")
    $notices.Add("    Licença: " + $info.Licenca)
    $notices.Add("    " + $info.Aviso)

    # Copia o arquivo de licença real do pacote quando existir no cache.
    $pastaPacote = Join-Path $env:USERPROFILE ".nuget\packages\$p"
    if (Test-Path $pastaPacote) {
        $versaoPkg = Get-ChildItem $pastaPacote -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($versaoPkg) {
            $arquivos = Get-ChildItem $versaoPkg.FullName -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^(LICEN[CS]E|COPYING)' }
            foreach ($a in $arquivos) {
                $destino = Join-Path $pastaLicencas ("$p-" + $a.Name)
                Copy-Item $a.FullName $destino -Force
            }
        }
    }
    $notices.Add("")
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $pastaLicencas 'THIRD-PARTY-NOTICES.txt'), ($notices -join "`r`n"), $utf8)

# Licença do próprio projeto, quando existir.
$licencaProjeto = Join-Path $raiz 'LICENSE'
if (Test-Path $licencaProjeto) {
    Copy-Item $licencaProjeto (Join-Path $pastaLicencas 'FlavosDiag-LICENSE.txt') -Force
}

# ---- 3. Leia-me da distribuição ----
$leiaMe = @"
Flavos Diag $Versao — diagnóstico de bancada para Windows
==========================================================

COMO USAR
1. Copie esta pasta inteira para o computador ou para um pendrive.
2. Abra FlavosDiag.App.exe.
3. Para ler mais dados (temperatura de sensores e alguns dados de disco), clique com o botão direito e
   escolha "Executar como administrador". Sem isso, o programa funciona em modo reduzido e mostra
   "Inconclusivo" nos itens que não conseguiram ser lidos.

Não é preciso instalar nada: esta pasta já traz tudo o que o programa precisa.

O QUE O PROGRAMA FAZ
- Lê a configuração do computador e apresenta um resumo por categoria.
- Executa testes próprios de disco, memória, processador e placa de vídeo.
- Gera um laudo em PDF e em JSON, com os números medidos e as limitações encontradas.

O QUE O PROGRAMA NÃO FAZ
- Não altera o sistema: nada de registro, serviços, drivers, otimizações ou reparos.
- Não envia dados pela internet. Não há telemetria.
- Não substitui backup. Nenhum resultado autoriza deixar de fazer cópia de segurança.

QUANDO APARECE "INCONCLUSIVO"
Significa que aquele item não pôde ser medido naquele computador. Pode ser falta de permissão, sensor
inexistente ou recurso não suportado. O programa mostra sempre o motivo, e nunca trata dado ausente como
"tudo certo".

ARQUIVOS GERADOS
- Configurações e laudos ficam em: %LOCALAPPDATA%\FlavosDiag
- Os números de série aparecem mascarados no laudo por padrão (configurável).

AVISO
Este programa não é assinado digitalmente. Por isso o Windows SmartScreen pode exibir um aviso na primeira
execução. Confirme apenas se obteve o arquivo de fonte confiável.

Licenças das bibliotecas usadas: veja a pasta licenses/.
"@
[System.IO.File]::WriteAllText((Join-Path $pastaDist 'LEIA-ME.txt'), $leiaMe, $utf8)

# ---- 4. Resumo ----
$tamanhoMb = [math]::Round(((Get-ChildItem $pastaDist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
$totalArquivos = (Get-ChildItem $pastaDist -Recurse -File).Count

Write-Host ''
Write-Host 'PACOTE GERADO' -ForegroundColor Green
Write-Host "  pasta:    $pastaDist"
Write-Host "  versão:   $Versao"
Write-Host "  arquivos: $totalArquivos"
Write-Host "  tamanho:  $tamanhoMb MB"
Write-Host "  licenças: $((Get-ChildItem $pastaLicencas -File).Count) arquivo(s) em licenses/"
Write-Host ''
Write-Host 'Antes de entregar, confira:'
Write-Host '  - o executável abre e mostra a tela inicial;'
Write-Host '  - a pasta licenses/ contém o aviso de terceiros;'
Write-Host '  - as limitações conhecidas estão em docs/ROTEIRO-TESTE-MANUAL.md.'
