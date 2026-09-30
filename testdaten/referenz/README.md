# Referenzwerte aus dem Original (Andi-Generator 24.5.1.0)

Grundlage für die Paritätstests (MIGRATIONSPLAN E7, angepasst): Anzahlen exakt, Kosten gleich nach Anzeige-Formatierung.

## Aufbau
- `eingabe/` – Kopie des click-TT-Exports mit dem **zu bewertenden Plan als `<existingSchedule>`** (aus der CSV bzw. einem gegebenen Plan), dazu ggf. eine Kopie der `.modifications`. Die Originale in `Testdaten` bleiben unberührt.
- `werte/Rn.json` – abgelesene Werte: Plan-Kostenarten, Tabelle Mannschaft × Kostenart (Zellen wörtlich, z.B. `5 /178 (35.738)`), Summenzeile, Detailmeldungen, Herleitung/Plausibilität.

## Ablauf je Fall (erprobt am 27.09.2026 mit R1–R3)
1. Außer Andi-Generator und Claude **alle Fenster minimieren**. Sonst verdecken sie Dialoge in den Bildschirmaufnahmen.
2. „Terminwünsche laden… → Datei öffnen“ → Datei aus `eingabe/`.
3. Dialog „Benutzereinstellungen…“: **„Nein, die Standardeinstellungen verwenden“**. Die dort angezeigten hinterlegten Einstellungen in der JSON unter `hinterlegte_benutzereinstellungen_nicht_verwendet` notieren.
4. Ohne `.modifications` erscheint der Erststart-Assistent: jeden Schritt mit **„Überspringen“** beenden.
5. „Angezeigter Plan: in Click-TT vorhandener Plan“ (Standard) → Tab **Kosten** → mit Zoom 100 % die Tabelle ablesen, mit 50–75 % die Meldungen.
6. Im Tab **Terminplan** kontrollieren, ob alle Spiele übernommen wurden; „ungültiger Termin“ notieren.
7. Plausibilität: Summe der Mannschafts-Gesamtwerte + Plan-Kostenarten ≈ Gesamtkosten; Anzahlen in den Meldungen = Anzahlen in der Tabelle.

## Erkenntnisse
- **Drucken nach PDF geht nicht:** Beim Wechsel auf „Microsoft Print to PDF“ im Druckdialog meldet das Original „Ausgewählter Drucker ist ungültig“ bzw. „Operation auf ausgewähltem Drucker nicht verfügbar“ (neuer Befund #31). Das Ablesen am Bildschirm ist ohnehin genauer: Kleine Werte stehen vollständig da (`35.738`, `3,33`), gerundet wird erst ab 100 000 („100 T“).
- Beim Öffnen und Bewerten schreibt das Original **nichts** in den `eingabe`-Ordner zurück.
- Das Original merkt sich Benutzereinstellungen **je Staffel** (unter `%LOCALAPPDATA%`), unabhängig vom Dateinamen. Die CSV-Pläne wurden vermutlich mit diesen Einstellungen erzeugt. Für die Parität spielt das keine Rolle, weil beide Seiten mit denselben (Standard-)Optionen rechnen.

## Stand (27.09.2026): 25 Referenzfälle
Alle Werte mit `tools/speichern.py` geprüft: Die Zeilensummen ergeben jeweils „Gesamt“, die Spaltensummen die „Summe“-Zeile, und die Summe plus Plan-Kostenarten die Gesamtkosten, jeweils innerhalb der Anzeigerundung. R1–R18 sind CSV-Pläne, R19–R22 bestehende click-TT-Pläne, R24–R26 Testpläne des Autors. **R23** (Doppelrunde) und **R27** (ideale Meldungen) enthalten keinen Plan und bleiben für die Optimierertests (Phase 3).

| Fall | Plan | Mannsch. | Gesamtkosten | ungültige Spiele | besondere Kostenarten |
|---|---|---|---|---|---|
| R1 | Damen Kreisliga | 3 | 60.000 Mio | 60.000 Mio | – |
| R2 | 4. Kreisklasse Gruppe A.csv | 7 | 104 T | – | Ausweichtermine |
| R3 | Damen Bezirksoberliga Rheinhessen.csv | 9 | 190 T | – | – |
| R04 | 1. Kreisklasse Gruppe A.csv | 10 | 85.305 | – | – |
| R05 | 1. Kreisklasse Gruppe B.csv | 10 | 385 T | – | – |
| R06 | 1. Kreisklasse.csv | 11 | 1,8 Mio | – | 60km Regel |
| R07 | 2. Kreisklasse Gruppe A.csv | 10 | 193 T | – | Ausweichtermine |
| R08 | 2. Kreisklasse Gruppe B.csv | 10 | 545 T | – | – |
| R09 | 2. Kreisklasse.csv | 10 | 180 Mio | – | – |
| R10 | 3. Kreisklasse.csv | 10 | 809 T | – | Ausweichtermine |
| R11 | 4. Kreisklasse.csv | 12 | 1,6 Mio | – | Vereinsinterne Spiele am Anfang |
| R12 | 4. Kreisklasse A.csv | 12 | 982 Mio | – | Vereinsinterne Spiele am Anfang |
| R13 | 4. Kreisklasse Gruppe B.csv | 8 | 290 T | – | – |
| R14 | Bezirksliga Rheinhessen Nord.csv | 10 | 458 T | – | Ausweichtermine |
| R15 | Bezirksliga Rheinhessen Süd.csv | 11 | 449 T | – | – |
| R16 | Bezirksoberliga Rheinhessen Nord.csv | 10 | 95.372 | – | 60km Regel, Heimkoppel |
| R17 | Bezirksoberliga Rheinhessen Süd.csv | 10 | 192 T | – | Ausweichtermine |
| R18 | Damen Bezirksliga Rheinhessen.csv | 6 | 5.067 | – | – |
| R19 | in Click-TT vorhandener Plan | 11 | 30.084 Mio | 30.000 Mio | 60km Regel |
| R20 | in Click-TT vorhandener Plan | 12 | 1,6 Mio | – | Vereinsinterne Spiele am Anfang |
| R21 | in Click-TT vorhandener Plan | 12 | 14 Mio | – | Ausweichtermine, 60km Regel, Heimkoppel, Auswärtskoppel |
| R22 | in Click-TT vorhandener Plan | 11 | 1.041.589 Mio | 1.040.000 Mio | Heimkoppel, Auswärtskoppel |
| R24 | in Click-TT vorhandener Plan | 11 | 60.012 Mio | 60.000 Mio | 60km Regel |
| R25 | in Click-TT vorhandener Plan | 10 | 60.094 Mio | 60.000 Mio | Vereinsinterne Spiele am Anfang |
| R26 | in Click-TT vorhandener Plan | 10 | 50.011 Mio | 50.000 Mio | Heimkoppel |

## Weitere Erkenntnisse aus dem Ablesen
- **Namen werden getrimmt:** Im click-TT-Export stehen manche Mannschaftsnamen mit führendem Leerzeichen (`" Aowi Trbk Bosu Agps IV"`). Das Original zeigt sie ohne Leerzeichen an und ordnet CSV-Pläne und `.modifications` darüber korrekt zu (R10, R15, R17). Der C#-Import muss genauso trimmen.
- **click-TT akzeptiert die Delphi-CSV:** Der bestehende Plan in `4__Kreisklasse (6).xml` ist Spiel für Spiel identisch mit `4. Kreisklasse.csv` (132/132). R20 = R11. Die Windows-1252-CSV ohne BOM wurde also erfolgreich importiert (Frage 12).
- **Kosten hängen nicht nur von der Anzahl ab:** Gleiche Anzahl ergibt unterschiedliche Kosten (z.B. Wechsel H/A 5 → 83 bzw. 4.083; Sperrtermine 5/178 → 35.738 bzw. 31.001). Das sind gute Paritätsfälle für die Staffelungs- und Abstandslogik.
- Das Original merkt sich die Wahl „Standardeinstellungen“ je Staffel. Beim erneuten Laden derselben Staffel kommt keine Abfrage mehr (R12, R19, R20).
- Der Erststart-Assistent hat je nach Datei 2 bis 4 Seiten (Spiellokale, Koppeltermine, Auswärtskoppelwünsche, Setzliste, Pflichtspieltage).
- Die Meldungen unter der Tabelle wurden nur teilweise erfasst (`meldungen`/`meldungen_teilweise`). Verbindlich sind Tabelle und Plan-Kostenarten.
