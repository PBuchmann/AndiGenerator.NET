# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
#
# Fasst die Cobertura-Berichte aller Testprojekte zu einer Zeilenabdeckung je Schicht zusammen: Eine Zeile gilt als
# abgedeckt, wenn irgendein Testprojekt sie ausführt. Ausgabe als Markdown-Tabelle (für die Zusammenfassung im CI).
param(
    [Parameter(Mandatory = $true)][string] $Ordner,
    [string] $Ausgabe
)

$zeilen = @{}
foreach ($datei in Get-ChildItem -Path $Ordner -Recurse -Filter *.cobertura.xml) {
    [xml] $bericht = Get-Content -Raw -LiteralPath $datei.FullName
    foreach ($paket in $bericht.coverage.packages.package) {
        if (-not $paket.name.StartsWith('AndiGenerator.') -or $paket.name.EndsWith('.Tests')) { continue }
        foreach ($klasse in $paket.classes.class) {
            foreach ($zeile in $klasse.lines.line) {
                $schluessel = "$($paket.name)|$($klasse.filename)|$($zeile.number)"
                $abgedeckt = [int] $zeile.hits -gt 0
                if (-not $zeilen.ContainsKey($schluessel)) { $zeilen[$schluessel] = $abgedeckt }
                elseif ($abgedeckt) { $zeilen[$schluessel] = $true }
            }
        }
    }
}

$text = @('## Testabdeckung (Zeilen)', '', '| Schicht | abgedeckt | Zeilen | Anteil |', '|---|---:|---:|---:|')
$gesamt = 0; $gesamtAbgedeckt = 0
$zeilen.GetEnumerator() | Group-Object { $_.Key.Split('|')[0] } | Sort-Object Name | ForEach-Object {
    $anzahl = $_.Count
    $abgedeckt = @($_.Group | Where-Object { $_.Value }).Count
    $script:gesamt += $anzahl; $script:gesamtAbgedeckt += $abgedeckt
    $text += "| $($_.Name) | $abgedeckt | $anzahl | $([math]::Round(100.0 * $abgedeckt / [math]::Max($anzahl, 1), 1)) % |"
}
$text += "| **Gesamt** | **$gesamtAbgedeckt** | **$gesamt** | **$([math]::Round(100.0 * $gesamtAbgedeckt / [math]::Max($gesamt, 1), 1)) %** |"

$text | Write-Output
if ($Ausgabe) { $text | Out-File -FilePath $Ausgabe -Append -Encoding utf8 }
