# Vergleich der Datendialoge mit dem Original (Phase 6, letzter Schritt)

Ziel: Für dieselbe Bearbeitung muss die neue Version dieselbe `.modifications`-Datei schreiben wie das Original.
Jede Datei `Dnn_….xml` ist eine unveränderte Kopie eines Referenzfalls (R2, bei D06 R26). Je Datei **genau eine**
Bearbeitung im Original, danach entsteht daneben `Dnn_….modifications`. Die Originale in `Testdaten` und `eingabe/`
bleiben unberührt.

## Ablauf je Datei
1. Im Original „Terminwünsche laden… → Datei öffnen“ → Datei aus diesem Ordner.
2. „Benutzereinstellungen…“: „Nein, die Standardeinstellungen verwenden“ (falls gefragt). Rundenfrage: „Nein“.
3. Erststart-Assistent: jeden Punkt mit **„Überspringen“**.
4. „Spielplandaten bearbeiten“ öffnen, die Seite wählen, die Bearbeitung unten ausführen, **OK**.
5. Wenn etwas nicht genau so geht oder du etwas anderes wählst: bitte in der Tabelle unter „Notiz“ festhalten.

## Bearbeitungen
| Datei | Seite | Bearbeitung | Notiz |
|---|---|---|---|
| D01_Allgemein | Allgemein | Name → `4. Kreisklasse Gruppe A Test`; Ende → `16.05.2027` | |
| D02_Mannschaft_aendern | Mannschaften | `Vluy Wwae` bearbeiten: Name → `Vluy Wwae Aoia`, Spiellokal → `2` | |
| D03_Mannschaft_neu | Mannschaften | Neue Mannschaft: Name `Trbk Tzqu`, alle anderen Felder leer lassen | ausgeführt mit ID `1234`, Vereins-ID `12345`, Nummer 1 |
| D04_Setzliste | Setzliste | „Setzliste aktivieren“ anhaken; `Vluy Wwae` auf den ersten Eintrag ziehen | |
| D05_Spiellokale | Spiellokale (Kompaktansicht) | `Tueq Qfga IV`: Standardspiellokal → `2`; Nachbarmannschaft „(Damen) Tueq Qfga“ → `3` | |
| D06_Heimkoppeln | Heimkoppel/Doppelspieltage (Kompakt) | `Euxw Grxg Bcbs`: beim **ersten** Termin einen anderen Eintrag wählen | ausgeführt: 10.10.2015 auf „möglich“ (`coupleprio` 0) |
| D07_Auswaertskoppeln | Auswärtskoppeltermine | `Ujuu Eaom IV`: neue Auswärtskoppel mit `Tueq Qfga IV` und `Vluy Wwae`, sonst Vorgaben lassen | ausgeführt: „am gleichen Tag“ (`sameday` 0) |
| D08_Wunschtermine | Wunschtermine | `Tueq Qfga IV`: Uhrzeit am `09.09.2026` → `20:00` | |
| D09_Nachbarmannschaften | Nachbarmannschaften | `Tueq Qfga IV`: neue Nachbarmannschaft `Tueq Tzqu` (Art wie vorgeschlagen), eine Begegnung: `05.10.2026 20:00`, Heimspiel gegen `Gegner X`, Spiellokal leer | ausgeführt: Art `Herren`, Nummer 1, Begegnung `15.10.2026 20:00` gegen `X` |
| D10_60km | 60km Regel | `Tueq Qfga IV`: Häkchen bei `Vluy Wwae` | |
| D11_Spielfreie_Tage | Spielfreie Tage | die ersten **zwei** Tage der Liste anhaken | ausgeführt: 17.08.2026 und 18.08.2026 |
| D12_Pflichtspieltage | Pflichtspieltage | vorhandenen Zeitraum (12.–18.04.2027) auf Anzahl `2`; neuen Zeitraum `01.03.2027`–`07.03.2027`, Anzahl `1` | |
| D13_Begegnungen | Manuell festgelegte Begegnungen | Neue Begegnung: `15.10.2026 20:00`, Heim `Vluy Wwae`, Gast `Ujuu Hpom IV`, Spiellokal leer | ausgeführt am `10.10.2026 20:00` |
| D14_Heimrecht | Heimrecht | `Vluy Wwae`: Anzahl „Vorrunde: 3 Rückrunde: 3“; bei `Ujuu Hpom IV` → `Vorrunde` | |

## Erwartete bewusste Abweichungen
- **D03:** Die neue Version setzt bei neuen Mannschaften `homerights="-1"` (Befund #21); das Original lässt das Attribut weg.
- **D02:** Die Umbenennung zieht in der neuen Version Verweise nach (Befund #16); in R2 gibt es solche Verweise nicht, deshalb sollte kein Unterschied entstehen.

## Stand
29.09.2026: alle 14 Bearbeitungen im Original ausgeführt. Die Spalte „Notiz“ gibt wieder, was laut `.modifications`
tatsächlich gewählt wurde. Die Tests `Datendialog_Dnn_…` in `PlansitzungTests` stellen genau das nach.
