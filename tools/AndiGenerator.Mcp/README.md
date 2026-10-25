# andigen-mcp – AndiGenerator.NET für KI-Anwendungen (MCP)

`andigen-mcp` ist ein Server für das **Model Context Protocol** (MCP). Eine KI-Anwendung wie Claude Desktop startet ihn als
Unterprozess und kann damit eine Staffel planen – mit **derselben Ablauflogik wie die Oberfläche**: Einrichtung,
Kostenoptimierung, Automodus mit HiGHS (Schranke, B-Varianten, Teilneuplanung), Kompaktspieltage, Merken und Export. Der
Server erzeugt dafür das Hauptfenster-Modell ohne Fenster; Optimiert wird vom Programm, die KI entscheidet und erklärt.

- **Lokal:** Verbindung über stdio, kein Netzwerkport, keine API-Schlüssel im AndiGenerator.
- **Datenschutz:** Was die KI-Anwendung von den Antworten (Mannschaften, Termine, Hallen) an ihren Dienst schickt, entscheidet
  ihr Anwender mit seiner KI-Anwendung. Das Programm selbst bleibt ohne KI vollständig und offline.
- **Ablage:** Optionen und gemerkte Pläne liegen wie beim Programm unter `%LOCALAPPDATA%\AndiGenerator.NET`; in der
  Oberfläche erscheinen sie beim nächsten Öffnen der Staffel. Dieselbe Staffel nicht gleichzeitig in der Oberfläche und über
  MCP bearbeiten.

## Einrichten (Claude Desktop)

1. `andigen-mcp` liegt dem installierten Programm bei (ab 0.9.10), im Programmordner neben `AndiGenerator.NET`:

   | System | Pfad |
   |---|---|
   | Windows (Setup) | `%LOCALAPPDATA%\AndiGeneratorNET\current\andigen-mcp.exe` |
   | Windows (Portable) | `andigen-mcp.exe` im entpackten Ordner |
   | macOS | `/Applications/AndiGenerator.NET.app/Contents/MacOS/andigen-mcp` |
   | Linux (.deb) | `/opt/andigenerator-net/andigen-mcp` |

   Selbst gebaut (`bauen.cmd`): `%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Mcp\release\andigen-mcp.exe`.
2. In Claude Desktop: *Einstellungen → Entwickler → Konfiguration bearbeiten* öffnet `claude_desktop_config.json`. Eintragen
   (Benutzername anpassen):

   ```json
   {
     "mcpServers": {
       "andigenerator": {
         "command": "C:\\Users\\peter\\AppData\\Local\\AndiGeneratorNET\\current\\andigen-mcp.exe"
       }
     }
   }
   ```

3. Claude Desktop neu starten. Unter den Werkzeugen erscheint „andigenerator“.

Vor einem Update des Programms die KI-Anwendung beenden: Solange sie `andigen-mcp` gestartet hat, kann das Update die Datei
nicht ersetzen.

Beispiel: „Öffne C:\Spielplaene\4. Kreisklasse Gruppe A.xml, plane sie im Automodus, bis Stufe B optimal ist, und erkläre mir
die verbleibenden Sperrtermin-Verstöße.“

## Werkzeuge

| Werkzeug | Wirkung |
|---|---|
| `status` | Staffel, Einrichtung, Verfahren, ob gerechnet wird, Kacheln, nächster Schritt, Meldungen |
| `staffel_oeffnen`, `staffel_schliessen` | XML-Export aus click-TT öffnen bzw. schließen |
| `beispiel_oeffnen` | Eine der beiliegenden Beispielstaffeln öffnen (Kopie im Datenordner); ohne Namen die Liste |
| `einrichtung`, `einrichtung_waehlen`, `einrichtung_abschliessen` | Punkte der Einrichtung ansehen, entscheiden, abschließen |
| `verfahren_waehlen`, `startplan_waehlen` | Automodus oder Kostenoptimierung; Ausgangsplan |
| `starten`, `anhalten`, `fortsetzen`, `stoppen`, `warten` | Generierung bzw. Berechnung steuern und verfolgen |
| `qualitaet`, `verstoesse` | Qualitätsansicht: Stufen, Verstöße, bestmöglich (HiGHS); Verstöße je Mannschaft |
| `einstufen` | Kriterium in A, B oder C einstufen (Automodus) |
| `b_varianten`, `b_variante_festlegen`, `festlegung_aufheben` | B-Varianten aus HiGHS vergleichen und festlegen |
| `plan_merken`, `csv_exportieren`, `drucken` | Plan merken; CSV für den Import in click-TT; Ausdruck als PDF (Plan, Abschnitte, Format wählbar) |
| `gewichtungen`, `gewichtung_setzen` | Gewichtungen der Kostenoptimierung lesen und ändern (Kostenart, Mannschaft oder beides) |
| `spielplandaten`, `mannschaft` | Spielplandaten im Überblick bzw. einer Mannschaft (Termine, Sperren, 60-km-Regel, Bemerkung) |
| `termin_setzen`, `spielfreie_tage`, `setzliste_setzen`, `nur_wochenende`, `begegnung_festlegen`, `begegnung_loeschen`, `pflichtspielzeit`, `kompakt_meldung` | Spielplandaten ändern; gilt sofort, auch für die laufende Generierung |
| `mannschaft_speichern`, `mannschaft_loeschen` | Mannschaft anlegen, ändern (Name, Nummer, IDs aus click-TT, Spiellokal) oder löschen |
| `spiellokal_setzen` | Spiellokal der Mannschaft, eines Heimtermins oder einer Nachbarmannschaft |
| `heimkoppel_setzen`, `auswaertskoppel_setzen`, `auswaertskoppel_loeschen` | Heimkoppeln (zwei Heimspiele am Tag, Doppelspieltag) und Auswärtskoppeln |
| `nachbarmannschaft_speichern`, `nachbarmannschaft_loeschen`, `nachbarspiel_setzen` | Nachbarmannschaften (dieselbe Halle) mit ihren Spielen |
| `heimrecht_setzen` | Heimspiele in der Vorrunde, Heimspiel gegen einen Gegner in Vor- oder Rückrunde |
| `terminplan` | Terminplan Woche für Woche mit Hinweisen, auch nur einer Mannschaft und für jeden Plan |
| `diagramm` | Ein Diagramm als Bild (PNG) mit Beschreibung und Verstößen je Mannschaft |
| `kompakt`, `kompakt_plan_waehlen` | Kompaktspieltage: Pläne, Erläuterung, Spieltage, Hinweise; Plan wählen |
| `schritt_fuer_schritt` | Die Führung „Schritt für Schritt“ der Oberfläche für die aktuelle Lage |

Fragen, die die Oberfläche stellen würde, beantwortet der Server zurückhaltend: Der gesicherte Plan bleibt erhalten, ein Export
mit Hinweisen geschieht nur mit `trotzHinweisen`, und alle Meldungen gehen mit der nächsten Antwort an die KI. Die Anleitung
für die KI (Ablauf, vor Änderungen den Anwender fragen) wird beim Verbinden übermittelt.

Ausdruck und Diagramme entstehen wie in der Oberfläche (dieselben Dateien `PdfAusdruck` und `SkiaZeichenflaeche`, Schriften
eingebettet), nur ohne Avalonia.

## Auslieferung

Der Release-Workflow veröffentlicht `andigen-mcp` für jede Plattform eigenständig und legt ihn mit
`tools/AndiGenerator.Mcp/beilegen.ps1` in den Programmordner (gemeinsame .NET-Laufzeit, gut 3 MB zusätzlich). Bräuchte der
Server eine Bibliothek in einer anderen Fassung als das Programm, bricht der Workflow ab, statt das Programm zu verändern.

## Noch nicht enthalten

Eine laufende Oberfläche mitsteuern (die KI arbeitet mit einem eigenen, unsichtbaren Hauptfenster-Modell).
