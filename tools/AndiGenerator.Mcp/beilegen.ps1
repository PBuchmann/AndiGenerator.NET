# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
#
# Legt den MCP-Server (andigen-mcp) dem veröffentlichten Programm bei (Release-Workflow, alle Plattformen):
# eigenständig für dieselbe Plattform veröffentlichen und die Dateien in den Programmordner kopieren. Beide teilen sich die
# .NET-Laufzeit und die gemeinsamen Bibliotheken; neu kommen nur andigen-mcp und seine Pakete (gut 3 MB) dazu.
# Gibt es eine Datei schon mit anderem Inhalt (z. B. eine andere Version derselben Bibliothek), bricht das Skript ab,
# statt das Programm zu verändern.
#
# Aufruf: pwsh tools/AndiGenerator.Mcp/beilegen.ps1 <rid> <version> <programmordner>

param(
    [Parameter(Mandatory)][string] $Rid,
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][string] $Programmordner
)

$ErrorActionPreference = 'Stop'
$wurzel = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$ziel = Resolve-Path $Programmordner
$zwischen = Join-Path ([IO.Path]::GetTempPath()) ("andigen-mcp-" + [Guid]::NewGuid().ToString('N'))

dotnet publish (Join-Path $wurzel 'tools' 'AndiGenerator.Mcp') -c Release -r $Rid --self-contained -nologo -o $zwischen "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw "Veröffentlichen von andigen-mcp fehlgeschlagen." }

$neu = 0
$gleich = 0
$abweichend = @()
foreach ($datei in Get-ChildItem $zwischen -Recurse -File) {
    $relativ = [IO.Path]::GetRelativePath($zwischen, $datei.FullName)
    $dort = Join-Path $ziel $relativ
    if (-not (Test-Path $dort)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $dort) | Out-Null
        Copy-Item $datei.FullName $dort
        $neu++
    } elseif ((Get-FileHash $datei.FullName).Hash -eq (Get-FileHash $dort).Hash) {
        $gleich++
    } else {
        $abweichend += $relativ
    }
}

Remove-Item $zwischen -Recurse -Force
if ($abweichend.Count -gt 0) {
    throw "andigen-mcp braucht andere Fassungen dieser Dateien als das Programm: $($abweichend -join ', ')"
}

Write-Host "andigen-mcp beigelegt: $neu Dateien neu, $gleich schon vorhanden."
