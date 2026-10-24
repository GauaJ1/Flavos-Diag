# Verificador de clean room do Flavos Diag (porta do script da skill, sem depender de Python).
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File build/cleanroom.ps1
# Retorna 1 se houver achados. Excecoes: .skills/flavos-diag-clean-room/scripts/cleanroom_allow.txt
param([switch]$Silencioso)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot

$proibidos = @(
    'aida\s?64', 'cinebench', 'furmark', 'cpu-?z', 'gpu-?z', 'hwinfo',
    'crystal\s?disk\s?info', '\bocct\b', 'prime\s?95', '3d\s?mark',
    'geekbench', 'passmark',
    'ghidra', '\bida\s?pro\b', '\bhopper\b', 'dnspy', '\bilspy\b',
    'x64dbg', 'rea-agents', 'morluto',
    'decompila', 'decompili', 'decompilo', 'decompilad',
    'engenharia\s+reversa\s+de\s+bin'
)
$padrao = [regex]::new(($proibidos -join '|'), 'IgnoreCase')

$pastas = @('src', 'tests', 'docs')
$arquivosRaiz = @('README.md')
$extTexto = @('.cs', '.xaml', '.resx', '.md', '.json', '.txt', '.csproj', '.props', '.yml', '.yaml', '.xml', '.ps1', '.py', '.sh', '.config')
$pularPastas = @('bin', 'obj', '.git', 'node_modules', '.vs', '.claude', 'TestResults', '.skills')
$extBin = @('.exe', '.dll', '.sys')

$permitidos = @{}
$listaPermitida = Join-Path $raiz '.skills\flavos-diag-clean-room\scripts\cleanroom_allow.txt'
if (Test-Path $listaPermitida) {
    Get-Content $listaPermitida -Encoding UTF8 | ForEach-Object {
        $l = $_.Trim()
        if ($l -and -not $l.StartsWith('#')) { $permitidos[$l.Replace('\', '/')] = $true }
    }
}

$achados = New-Object System.Collections.Generic.List[string]

function Obter-Arquivos([string]$rel) {
    $base = Join-Path $raiz $rel
    if (-not (Test-Path $base)) { return }
    Get-ChildItem $base -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
        $partes = $_.FullName.Substring($raiz.Length).Split([char[]]@('\', '/'))
        -not ($partes | Where-Object { $pularPastas -contains $_ })
    }
}

$candidatos = New-Object System.Collections.Generic.List[System.IO.FileInfo]
foreach ($d in $pastas) { Obter-Arquivos $d | ForEach-Object { $candidatos.Add($_) } }
foreach ($f in $arquivosRaiz) {
    $p = Join-Path $raiz $f
    if (Test-Path $p) { $candidatos.Add((Get-Item $p)) }
}

foreach ($arquivo in $candidatos) {
    $rel = $arquivo.FullName.Substring($raiz.Length + 1).Replace('\', '/')
    if ($permitidos.ContainsKey($rel)) { continue }
    if ($extTexto -notcontains $arquivo.Extension.ToLowerInvariant()) { continue }
    $n = 0
    foreach ($linha in [System.IO.File]::ReadAllLines($arquivo.FullName)) {
        $n++
        $m = $padrao.Match($linha)
        if ($m.Success) { $achados.Add(("{0}:{1}: termo proibido '{2}'" -f $rel, $n, $m.Value)) }
    }
}

Get-ChildItem $raiz -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $partes = $_.FullName.Substring($raiz.Length).Split([char[]]@('\', '/'))
    -not ($partes | Where-Object { $pularPastas -contains $_ })
} | Where-Object { $extBin -contains $_.Extension.ToLowerInvariant() } | ForEach-Object {
    $rel = $_.FullName.Substring($raiz.Length + 1).Replace('\', '/')
    if (-not $permitidos.ContainsKey($rel)) {
        $achados.Add(("{0}: binario versionado; registre em docs/LICENCAS.md e em cleanroom_allow.txt" -f $rel))
    }
}

if ($achados.Count -gt 0) {
    Write-Host 'CLEAN ROOM: achados que precisam de correcao:'
    $achados | ForEach-Object { Write-Host (" - " + $_) }
    exit 1
}

if (-not $Silencioso) { Write-Host 'CLEAN ROOM: nenhum achado.' }
exit 0