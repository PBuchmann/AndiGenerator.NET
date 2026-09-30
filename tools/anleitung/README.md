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

Mit `starten.cmd` gestartet, speichert **Strg+Umschalt+F12** das aktive Fenster (auch Dialoge) als PNG im Ordner `Bildschirmfotos` (nicht im Repository). Für die Bilder nur anonymisierte Testdaten (`testdaten/`) verwenden und persönliche Pfade unkenntlich machen.
