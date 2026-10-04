# Anleitung als PDF erzeugen

Quelle ist `Doku/ANLEITUNG.md` mit den Bildern unter `Doku/Anleitung/Bilder`. Daraus entsteht `Doku/Anleitung.pdf`, die das Programm mitliefert und über **Anleitung** öffnet.

Benötigt: [pandoc](https://pandoc.org), Node.js mit Playwright (Chromium) und die IBM-Plex-Schriften aus `src/AndiGenerator.UI/Assets/Fonts`.

```sh
# im Ordner Doku ausführen
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

Der Ablauf: Startseite, Einrichtung, Kostenoptimierung ab dem click-TT-Plan (8 s, dann Pause) mit allen Ansichten, Wechsel in den Automodus bis zur ersten Stufe (Ergebniskachel, Qualität), danach Spielplandaten, Gewichtung, Druckauswahl und Wunschtermin. Kommt ein Bild hinzu oder ändert sich die Oberfläche so, dass ein Schritt nicht mehr passt, wird der Test angepasst; danach die PDF wie oben neu erzeugen. Weil Kosten- und Automodus kurz echt rechnen, unterscheiden sich die Zahlen von Lauf zu Lauf leicht.

Einzelne Fenster lassen sich weiterhin von Hand aufnehmen: Mit `starten.cmd` gestartet, speichert **Strg+Umschalt+F12** das aktive Fenster (auch Dialoge) als PNG im Ordner `Bildschirmfotos` (nicht im Repository). Dafür nur anonymisierte Testdaten (`testdaten/`) verwenden und persönliche Pfade unkenntlich machen.
