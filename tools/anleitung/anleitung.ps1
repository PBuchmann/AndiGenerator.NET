# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
#
# Erzeugt Doku\Anleitung.pdf aus Doku\ANLEITUNG.md unter Windows, ohne Installation: pandoc wird beim ersten Lauf als
# portables ZIP geladen (Prüfsumme fest) und unter %LOCALAPPDATA%\AndiGenerator.NET\werkzeuge abgelegt; gedruckt wird
# mit drucken.cs (Playwright für .NET) und dem installierten Edge. Gebaut wird in einem Ordner unter %TEMP%, damit weder
# im Repository Zwischendateien entstehen noch dessen Directory.Build.props für das Skript gilt.
# Aufruf über anleitung.cmd im Hauptordner. Hinweis: Windows PowerShell 5.1 liest diese Datei als ANSI, deshalb stehen
# in Zeichenketten keine typografischen Zeichen (Gedankenstrich über [char]0x2013).
param([switch] $NurHtml)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$pandocVersion = '3.6.4'
$pandocPruefsumme = 'A9E5FEB3D56D2FB0E3E765D1C33B8EE6B72E6963D7DE31504EDEEC8CD1BE34B1'
$werkzeuge = Join-Path $env:LOCALAPPDATA 'AndiGenerator.NET\werkzeuge'
$pandoc = Join-Path $werkzeuge "pandoc-$pandocVersion\pandoc.exe"
$bau = Join-Path $env:TEMP 'andigen-anleitung'

# pandoc holen, falls noch nicht da
if (-not (Test-Path $pandoc)) {
    Write-Host "Lade pandoc $pandocVersion ..."
    New-Item -ItemType Directory -Force $werkzeuge | Out-Null
    $zip = Join-Path $werkzeuge "pandoc-$pandocVersion.zip"
    Invoke-WebRequest "https://github.com/jgm/pandoc/releases/download/$pandocVersion/pandoc-$pandocVersion-windows-x86_64.zip" -OutFile $zip -UseBasicParsing
    $ist = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($ist -ne $pandocPruefsumme) {
        Remove-Item $zip
        throw "Pruefsumme von pandoc stimmt nicht ($ist)."
    }
    Expand-Archive $zip -DestinationPath $werkzeuge -Force
    Remove-Item $zip
}

$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ }
if (-not $edge) { throw 'Microsoft Edge wurde nicht gefunden; er wird zum Drucken der PDF gebraucht.' }

# Bauordner wie in README.md: Quelle, Bilder, Schriften, Stil und Druckskript nebeneinander
if (Test-Path $bau) { Remove-Item -Recurse -Force $bau }
New-Item -ItemType Directory -Force (Join-Path $bau 'fonts'), (Join-Path $bau 'Anleitung') | Out-Null
Copy-Item (Join-Path $repo 'Doku\ANLEITUNG.md') $bau
Copy-Item -Recurse (Join-Path $repo 'Doku\Anleitung\Bilder') (Join-Path $bau 'Anleitung\Bilder')
Copy-Item (Join-Path $repo 'src\AndiGenerator.UI\Assets\Fonts\IBMPlex*.ttf') (Join-Path $bau 'fonts')
Copy-Item (Join-Path $PSScriptRoot 'anleitung.css'), (Join-Path $PSScriptRoot 'drucken.cs') $bau

Push-Location $bau
try {
    Write-Host 'Erzeuge HTML ...'
    $titel = "AndiGenerator.NET $([char]0x2013) Anleitung"
    & $pandoc -f gfm -t html5 -s --metadata "pagetitle=$titel" --css anleitung.css -o anleitung.html ANLEITUNG.md
    if ($LASTEXITCODE -ne 0) { throw "pandoc ist fehlgeschlagen ($LASTEXITCODE)." }

    # Jeder Verweis innerhalb der Anleitung (#...) muss auf eine Überschrift zeigen, jedes Bild vorhanden sein.
    $html = [IO.File]::ReadAllText((Join-Path $bau 'anleitung.html'), [Text.Encoding]::UTF8)
    $ziele = [regex]::Matches($html, ' id="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $verweise = [regex]::Matches($html, 'href="#([^"]+)"') | ForEach-Object { [uri]::UnescapeDataString($_.Groups[1].Value) } | Sort-Object -Unique
    $bilder = [regex]::Matches($html, '<img src="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $fehler = @($verweise | Where-Object { $ziele -notcontains $_ } | ForEach-Object { "Verweis ohne Ziel: #$_" })
    $fehler += @($bilder | Where-Object { -not (Test-Path (Join-Path $bau $_)) } | ForEach-Object { "Bild fehlt: $_" })
    Write-Host "Verweise: $(@($verweise).Count), Bilder: $(@($bilder).Count)"
    if ($fehler.Count -gt 0) { $fehler | ForEach-Object { Write-Host $_ }; throw 'Die Anleitung hat kaputte Verweise oder Bilder.' }
    if ($NurHtml) { Write-Host "HTML unter $bau"; return }

    Write-Host 'Drucke PDF mit Edge (beim ersten Lauf werden die NuGet-Pakete geladen) ...'
    dotnet run drucken.cs
    if ($LASTEXITCODE -ne 0) { throw "Drucken ist fehlgeschlagen ($LASTEXITCODE)." }
    Copy-Item (Join-Path $bau 'Anleitung.pdf') (Join-Path $repo 'Doku\Anleitung.pdf') -Force
    Write-Host "Doku\Anleitung.pdf geschrieben ($([math]::Round((Get-Item (Join-Path $repo 'Doku\Anleitung.pdf')).Length / 1MB, 1)) MB)."
}
finally {
    Pop-Location
}
