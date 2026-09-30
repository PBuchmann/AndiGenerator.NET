# Testdaten – Inventar und Bewertung

> Stand: 27.09.2026 · Quelle: Ordner `Claude\Testdaten` (von Peter bereitgestellt) · Bezug: [MIGRATIONSPLAN](MIGRATIONSPLAN.md), Phase 0, Frage 13

## 1. Überblick
| Art | Anzahl | Bemerkung |
|---|---|---|
| click-TT-Exporte (`<TT>`, iso-8859-15, Schema `TTGenerator.xsd`) | 50 | davon 48 mit Mannschaften, 2 leer (`Damen_Kreisliga_(RR)*.xml`, 0 Mannschaften) |
| Leere AndiGenerator-Plandateien (`<andigenerator><plan/>`) | 2 | `3__Kreisklasse (9).xml`, `Verbandsoberliga.xml`: 80 Byte, gehören zur jeweiligen `.modifications` bzw. sind Rest-Dateien |
| `.modifications` (UTF-8, `<andigenerator><plan>`) | 21 | alle einem click-TT-Export zuordenbar |
| CSV-Exporte (click-TT-Importformat, von Delphi erzeugt) | 18 | **alle eindeutig einem click-TT-Export zuordenbar** (gleiche Mannschafts-IDs) → echte Eingabe/Ausgabe-Paare |
| ~~Fremddatei `sudoku.html`~~ | – | von Peter gelöscht (27.09.2026) |

Saison: 49 Dateien 2026/27 (17.08.2026–09.05.2027), 1 Datei 2025/26 (`Verbandsliga_Rheinland_Süd-West.xml`). Alle `seasonType="STANDARD"`. Region: Rheinhessen/Rheinland, Kreisklasse bis Verbandsoberliga, Damen und Herren. Viele Staffeln liegen in mehreren Exportständen vor (`(4)`, `(5)` …); das ist gut für Tests der `.modifications`-Zusammenführung über Versionen.

## 2. Abdeckung der Funktionen
| Merkmal | Vorkommen | Bewertung |
|---|---|---|
| Mannschaftszahl | 3, 6, 7, 8, 9, 10, 11, 12, 13 | gut; **ungerade Zahlen** (3, 7, 9, 11, 13) in 13 Dateien → Test für Befund #5 |
| Heimspieltermine (`homeGame`) | 9 641 | gut |
| Heim-Koppeltermine (`coupleGame`) | 5 Dateien (bis 61 Spiele) | ausreichend |
| Auswärtskoppel (`roadCouple`) | 4 Dateien (3–25) | ausreichend: `Verbandsliga_Rheinland_Süd-West`, `Verbandsoberliga (1)` |
| Nachbarmannschaften (`sisterTeams`) | alle Dateien | sehr gut (bis 120 Einträge) |
| Wochen ohne Spiel (`noWeekGame`) | 11 Dateien | gut |
| Pflichtspieltage (`mandatoryGameDays`) | 47 Dateien (je 7) | gut |
| Bestehender Spielplan (`existingSchedule`) | 4 Dateien (110–132 Spiele) | **wertvoll**: echte Pläne als feste Eingabe für Kosten-Parität |
| Vorgegebene Spiele (`predefinedGames`) | nur in 2 `.modifications` | knapp → ggf. synthetisch ergänzen |
| Spielfreie Tage (`noGameDays`) | **0** | **Lücke** → synthetisch ergänzen oder weitere Datei suchen |
| Setzliste (`ranking`) | nur 1 `.modifications` (`4__Kreisklasse_Gruppe_B (2)`); laut Peter keine weiteren Daten | **synthetisch erzeugen** |
| > 30 Mannschaften | 0 | erwartbar; Test für aufgehobenes Limit (E9) synthetisch |
| Corona-Rundenplanung (`rpCorona`) | 0; laut Peter keine Daten vorhanden | **synthetisch erzeugen** (E9: bleibt erhalten); Grundlage: ein Plan mit `existingSchedule` + Option `rpCorona` |
| Doppelrunde | 1 (Testplan des Autors) | knapp, aber vorhanden |

## 2a. Testpläne des Autors (`AndiGenerator\Exe\Testplaene`)
Fünf click-TT-Exporte aus der Saison 2015/16, **älteres Exportformat** (ohne `seasonType`) – damit lässt sich die Abwärtskompatibilität des Imports prüfen.
| Datei | Mannschaften | Besonderheit |
|---|---|---|
| `Liga für Doppelrunde.xml` | 6 | **Doppelrunde** (einziger Fall) |
| `Liga mit 11 Mannschaften und 40km Regel.xml` | 11 | 60-km-Regel (`noWeekGame` 12), bestehender Plan (110) |
| `Liga mit 2 Mannschaften aus einem Verein.xml` | 10 | Vereinsmannschaften im selben Plan, bestehender Plan (90) |
| `Liga mit Koppelterminen.xml` | 10 | Heim-Koppeltermine (10), bestehender Plan (90) |
| `Liga mit idealen Meldungen.xml` | 10 | Idealfall (Kosten nahe 0 erwartet) |

Damit gibt es insgesamt **7 echte bestehende Spielpläne** (4 + 3) und 18 CSV-Pläne als feste Eingaben für die Kosten-Parität.

## 2c. Kopien aus `%LOCALAPPDATA%\Andi-Generator` (nur Kopien, Originale bleiben unverändert)

- `Testdaten\Optionen (Stand 27.09.2026 19 Uhr)\<Staffel> <Jahr>.options` – alle 26 Optionsdateien (19 davon durch Befund #34 auf Standard zurückgesetzt). Die Kopie aus dem Ordner „ 1899“ heißt `_ohne Staffelname_ 1899.options`, weil OneDrive keine Dateinamen mit führendem Leerzeichen synchronisiert.
- `Testdaten\Gemerkte Plaene\1. Kreisklasse 2026\Testplan.xml` – mit „Plan merken“ im Original 26.7.1.0 erzeugt (110 Spiele, alle ohne Termin). Ein gemerkter Plan mit terminierten Spielen wäre als Ergänzung nützlich.

## 2b. Synthetisch zu erzeugende Testdaten
Setzliste (mehrere Varianten inkl. Lücken in `rankingindex`, Befund #17), Corona-Rundenplanung, spielfreie Tage, vorgegebene Spiele, > 30 Mannschaften (aufgehobenes Limit), Spiellokale > 5, Mannschaft ohne Heimtermine (Befund #2), Doppelrunde mit ungerader Mannschaftszahl. Erzeugt werden sie durch Abwandeln echter Dateien (z.B. Einträge in `.modifications` ergänzen) mit einem Generator in `AndiGenerator.Cli`. Die Referenzwerte dazu kommen ebenfalls aus der Delphi-Exe.

## 3. Erkenntnisse für die Formate (E8)
**CSV-Export (Delphi → click-TT):**
- Encoding **Windows-1252/Latin-1** (z.B. `ü` = `0xFC`), kein BOM, Zeilenende **CRLF**, Trennzeichen `;`. Das bestätigt E8.
- Kopfzeile mit **15** Feldern: `Nr.;Vor/Rück;Tag;;Datum;;Uhrzeit;HeimVereinNr;Heim-Mannschaft;GastVereinNr;Gast-Mannschaft;HeimMannschaftNr;GastMannschaftNr;Ergebnisse;Spiellokal`. Datenzeilen haben **16** Felder (abschließendes `;`). Für byte-genaue Parität muss das genau so nachgebaut werden.
- Datum `dd.mm.yyyy`, Uhrzeit `hh:mm`, Wochentag ausgeschrieben deutsch, `Vor/Rück` = `0`/`1`, Spiellokal leer oder `1`–`3`.
- Sortierung: erst Vorrunde, dann Rückrunde, innerhalb der Runde chronologisch.
- **Befund #8 bestätigt:** `Damen Kreisliga (VR).csv` beginnt mit Nr. `7` (Lücken in der Nummerierung).

**click-TT-Export:** Das Schema ist `http://www.tt-liga.de/includes/TTGenerator.xsd`. Es sollte besorgt und in die Tests aufgenommen werden (Frage 12). Umlaute stehen teils als Zeichenreferenz (`&#252;`). Mannschaftsnamen enthalten Eigenheiten wie **doppelte Leerzeichen** (`Rotamint  III`); diese dürfen beim Import nicht normalisiert werden.

**`.modifications`:** Elemente `team`, `homegameday`, `nogameday`, `sisterteam`, `sistergame`, `noweekgames`, `roadcouple`, `mandatorygames`, `predefinedgames`, `existingschedule`, `ranking`; Status-Attribut `state` (z.B. `dosModified`). ~~Drei Dateien verweisen auf verwaiste Mannschaften~~ **Korrektur 27.09.2026:** Die Mannschaften sind nicht verwaist. Im Export steht der Name mit **führendem Leerzeichen** (`" Aowi Trbk Bosu Agps IV"`), das Original trimmt beim Laden. CSV und `.modifications` verwenden den getrimmten Namen. Das ist ein Paritätstest für das Trimmen beim Import (Referenzfälle R10, R15, R17).

## 4. Datenschutz / Veröffentlichung
Die Dateien enthalten keine Personennamen, E-Mail-Adressen oder Telefonnummern, sondern nur Vereins-, Mannschafts- und Hallennamen sowie Termine (öffentlich in click-TT). Weil das Repository öffentlich wird (E11/E12), gilt trotzdem: **Testdaten nicht ungefragt ins öffentliche Repository**, sondern zunächst in einem privaten Ordner bzw. Repository. Veröffentlichung nur nach Rücksprache (Autor/Verband) oder als anonymisierte Kopie (Vereinsnamen durch Kürzel ersetzt; die Kosten bleiben dabei unverändert, solange Umbenennungen konsistent sind).

### Anonymisierte Kopie im Repository (29.09.2026)
Die Tests laufen standardmäßig gegen `testdaten/` im Repository: `testdaten/clicktt` (click-TT-Exporte, CSV-Pläne,
gemerkte Pläne, Optionsdateien) und `testdaten/referenz` (Referenzfälle `eingabe`, `werte`, `perf`, `datendialoge`).
Erzeugt wird sie mit `python3 tools/anonymisieren/anonymisieren.py` aus den privaten Originalen (`../Testdaten` und
`Referenz/`, beide nicht im Repository):
- Alle Mannschafts-, Vereins- und Hallennamen werden **wortweise** durch Kunstwörter aus vier Buchstaben ersetzt, überall
  gleich. Römische Mannschaftsnummern (I–XXX) bleiben, weil das Programm daran Mannschaften desselben Vereins erkennt.
- Die Kunstwörter sind in derselben ordinalen Reihenfolge vergeben wie die Wörter und gleich lang. Damit bleibt die
  Sortierung aller Namen und „Heim Gast“-Verkettungen erhalten; Kosten, Pläne und CSV-Exporte ändern sich nicht.
- Staffelnamen, Termine, IDs und Optionen bleiben unverändert (keine personenbezogenen Daten).
- Die private Zuordnung Klarname → Kunstname steht in `Referenz/zuordnung-anonym.json`.
- Neue Testdaten: in die privaten Ordner legen und das Skript erneut ausführen (die Kunstwörter können sich dabei ändern;
  in den Tests fest eingetragene Namen dann aus der Zuordnung übernehmen).

Gegen die Originale laufen die Tests mit den Umgebungsvariablen `ANDIGEN_TESTDATEN` (Ordner `Testdaten`) und
`ANDIGEN_REFERENZ` (Ordner `Referenz`); die fest eingetragenen Namen in den Tests passen dann allerdings nicht.

## 5. Vorschlag: Referenz-Set für Paritätstests (Phase 0/2)

> **Überholt durch den tatsächlichen Stand:** 25 abgelesene Referenzfälle R1–R26 (ohne R23), siehe [Referenz/README](../Referenz/README.md). Die Tabelle unten war der erste Vorschlag.
| # | Datei | Warum |
|---|---|---|
| R1 | `Damen_Kreisliga_(VR) (2).xml` + `.modifications` + CSV | kleinste Staffel (3 Mannschaften, nur Vorrunde), Nummerierungslücke |
| R2 | `4__Kreisklasse_Gruppe_A (2).xml` + `.modifications` + CSV | 7 Mannschaften (ungerade), klein |
| R3 | `Damen_Bezirksoberliga_Rheinhessen (1).xml` + CSV | 9 Mannschaften (ungerade), Standardfall |
| R4 | `Kreisoberliga (9).xml` + `.modifications` | 20 Heim-Koppeltermine |
| R5 | `Verbandsliga_Rheinland_Süd-West (2).xml` + `.modifications` | Auswärtskoppel (25), Koppel (44), noWeekGame (48): „alles drin“ |
| R6 | `Verbandsoberliga (1).xml` + `Verbandsoberliga.modifications` | 12 Mannschaften, bestehender Spielplan (132), Koppel + Auswärtskoppel |
| R7 | `4__Kreisklasse (6).xml` + `.modifications` + CSV | bestehender Spielplan in XML **und** in `.modifications` |
| R8 | `4__Kreisklasse_Gruppe_B (2).xml` + `.modifications` + CSV | vorgegebene Spiele + Setzliste |
| R9 | `3__Kreisklasse (8).xml` + `.modifications` | 13 Mannschaften (größte, ungerade) |
| R10 | `Kreisliga (10).xml` + `.modifications` | verwaiste Mannschaft in `.modifications` |

Alle übrigen Dateien dienen als **Breitentest** (Import und Round-Trip: laden → speichern → identisch).

**Feste Pläne für die Kosten-Parität** gibt es ohne Delphi-Lauf schon: die 18 CSV-Pläne und die 4 `existingSchedule`-Pläne. Für jeden davon braucht es aus Delphi die **Teilkosten je Kostenart und Mannschaft** (siehe offene Punkte).

## 6. Offene Punkte
1. **Delphi-Referenzwerte:** Lauffähige Exe 24.5.1.0 liegt vor (`AndiGenerator\Exe`, gleiche Version wie der analysierte Quellcode). Sie zeigt aber nur **gerundete Kosten** an (`MyFormatFloatShort`: „T“/„Mio“); exakt sind nur die **Anzahlen** (`Fehler /AllCount`). Für exakte Kostenwerte bräuchte es einen neu kompilierten Delphi-Build mit Kostenexport. → **Entschieden (E7, angepasst):** kein Delphi-Build; Parität = Anzahlen exakt, Kosten gleich nach Anzeige-Formatierung.
2. Zu welchen Eingabeständen (mit bzw. ohne `.modifications`, welche Gewichtungsoptionen) wurden die CSVs erzeugt? Ohne die dazugehörige `AndiGenerator.options` lassen sich die Kosten der CSV-Pläne nicht eindeutig reproduzieren.
3. `TTGenerator.xsd` besorgen.
4. ~~Welcher der beiden Pläne der 4. Kreisklasse ist der eingereichte?~~ → `4. Kreisklasse.csv`: identisch mit dem bestehenden Plan in `4__Kreisklasse (6).xml` (132/132 Spiele). `4. Kreisklasse A.csv` hat 14 Hallenkonflikte (Referenzfall R12).
