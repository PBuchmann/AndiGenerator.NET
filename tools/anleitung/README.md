# Anleitung als PDF erzeugen

Quelle ist `Doku/ANLEITUNG.md` mit den Bildern unter `Doku/Anleitung/Bilder`. Daraus entsteht `Doku/Anleitung.pdf`, die das Programm mitliefert und über **Anleitung** öffnet.

## Unter Windows

`anleitung.cmd` im Hauptordner erzeugt die PDF neu – ohne Installation, nur .NET SDK und Microsoft Edge müssen vorhanden sein. Dahinter steht `tools/anleitung/anleitung.ps1`: Es lädt beim ersten Lauf [pandoc](https://pandoc.org) als portables ZIP (Version und SHA-256-Prüfsumme stehen im Skript) nach `%LOCALAPPDATA%\AndiGenerator.NET\werkzeuge`, baut in `%TEMP%\andigen-anleitung` das HTML, prüft alle Verweise innerhalb der Anleitung und alle Bilder und druckt mit `drucken.cs` (Playwright für .NET mit dem installierten Edge, gleiche Einstellungen wie `pdf.js`) nach `Doku/Anleitung.pdf`. Beim ersten Lauf lädt `dotnet run` außerdem das NuGet-Paket Microsoft.Playwright. Protokoll: `anleitung-log.txt`. Nur das HTML (zum Prüfen im Browser): `powershell -File tools\anleitung\anleitung.ps1 -NurHtml`.

## Unter Linux (oder WSL)

Benötigt: [pandoc](https://pandoc.org), Node.js mit Playwright (Chromium) und die IBM-Plex-Schriften aus `src/AndiGenerator.UI/Assets/Fonts`.

```sh
# im Ordner Doku ausführen; die Shell muss UTF-8 sprechen, sonst wird der Gedankenstrich im Titel verstümmelt
export LANG=C.UTF-8
mkdir -p fonts && cp ../src/AndiGenerator.UI/Assets/Fonts/IBMPlex*.ttf fonts/
cp ../tools/anleitung/anleitung.css ../tools/anleitung/pdf.js .
pandoc -f gfm -t html5 -s --metadata pagetitle="AndiGenerator.NET – Anleitung" --css anleitung.css -o anleitung.html ANLEITUNG.md
node pdf.js            # schreibt Anleitung.pdf
rm -r fonts anleitung.css pdf.js anleitung.html
```

## Bildschirmfotos

`bildschirmfotos.cmd` erzeugt alle Bilder unter `Doku/Anleitung/Bilder` neu – automatisch, ohne Bildschirm und immer gleich: dieselbe Fenstergröße (1400 × 860), 150 % Auflösung und die anonymisierte Staffel `testdaten/referenz/eingabe/R2_4__Kreisklasse_Gruppe_A (2)`. Dahinter steht der Test `tests/AndiGenerator.UI.Tests/Bildschirmfotos.cs` mit der echten Oberfläche (Avalonia.Headless mit Skia); er läuft nur, wenn die Umgebungsvariable `ANDIGEN_BILDER` den Zielordner nennt, im normalen Testlauf tut er nichts. Von Hand gleichwertig:

```bat
set ANDIGEN_BILDER=%CD%\Doku\Anleitung\Bilder
dotnet test tests\AndiGenerator.UI.Tests -c Release --filter "FullyQualifiedName~Bildschirmfotos"
```

Der Ablauf: Startseite, Einrichtung (mit erfundener Mannschaftsmeldung und erfundenen Terminwünschen an echten Orten für die Prüfung der 60-km-Regel), Kostenoptimierung ab dem click-TT-Plan (8 s, dann Pause) mit allen Ansichten, Wechsel in den Automodus bis zur ersten Stufe (Ergebniskachel, Qualität), mit Leitstand (Kostenoptimierung und Automodus), Verlauf und der Rückfrage beim Sichern; danach Spielplandaten (spielfreie Tage, Setzliste, Wunschtermine, Wochenende mit Entfernungen), Gewichtung, Druckauswahl und Wunschtermin; zuletzt die Kompaktspieltage mit der erfundenen Klasse `testdaten/kompakt/Jugend_19_Testliga_(HR).xml` (neun Mannschaften, erzeugt mit `python3 tools/kompaktklasse/erzeugen.py`; Bericht „Terminwünsche“ an echten Orten und QTTR-Schnitte setzt der Test): Berechnung mit HiGHS, Pläne, Raster mit Karte, Spieltage, Bearbeiten und Druckauswahl im Kompaktmodus. Kommt ein Bild hinzu oder ändert sich die Oberfläche so, dass ein Schritt nicht mehr passt, wird der Test angepasst; danach die PDF wie oben neu erzeugen. Weil Kosten- und Automodus kurz echt rechnen, unterscheiden sich die Zahlen von Lauf zu Lauf leicht.

Einzelne Fenster lassen sich weiterhin von Hand aufnehmen: Mit `starten.cmd` gestartet, speichert **Strg+Umschalt+F12** das aktive Fenster (auch Dialoge) als PNG im Ordner `Bildschirmfotos` (nicht im Repository). Dafür nur anonymisierte Testdaten (`testdaten/`) verwenden und persönliche Pfade unkenntlich machen.
