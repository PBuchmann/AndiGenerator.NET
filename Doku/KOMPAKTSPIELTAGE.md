# Kompaktspieltage: Meldungen in click-TT erfassen

Anleitung für die Staffelleitung einer Staffel, die in **Kompaktspieltagen** spielt (bisher „Turniersystem“, z. B. Jugend 19 Verbandsoberliga): 3er-Spieltage (drei Mannschaften, drei Spiele) und 4er-Spieltage (vier Mannschaften in zwei Runden, zwei Spiele parallel). Statt einer eigenen Umfrage stehen die Meldungen der Vereine in den **Terminwünschen** von click-TT. Von dort kommen sie über die XML-Datei und den Bericht „Terminwünsche“ (PDF) in den AndiGenerator.

> Stand 12.10.2026. Die Planung von Kompaktspieltagen im Programm ist in Arbeit; diese Anleitung beschreibt nur die Erfassung, damit die Daten schon richtig in click-TT stehen.

## Auf einen Blick

Je Mannschaft und Sonntag in der Spalte **So** der Terminwünsche:

| Meldung des Vereins | Eintrag in click-TT | Bedeutung für die Planung |
|---|---|---|
| Heimspieltag möglich | Uhrzeit, z. B. `11:00` | Die Mannschaft kann an diesem Tag ausrichten; die Uhrzeit ist der **früheste Beginn**. Auswärts geht auch. |
| Heimspieltag nur notfalls | Uhrzeit mit Stern, z. B. `11:00*` | Ausweichtermin: wird nur genutzt, wenn es anders nicht geht. Höchstens ein Stern je Spieltag (Woche). |
| nur Heimspiel, nicht auswärts | Uhrzeit **und** Zeile `Nur Heim:` in den Bemerkungen (siehe unten) | Die Mannschaft richtet aus oder spielt an diesem Tag gar nicht. |
| Auswärtsspieltag möglich | leer lassen | Die Mannschaft kann an diesem Tag als Gast zu einem Kompaktspieltag fahren. |
| kann nicht | `frei` | Sperrtermin: an diesem Tag kein Spiel. |
| zwei Spiele parallel möglich (4er-Spieltag) | **max. Heimspiele** `2` beim Heimtermin (Schritt 4.1) | Ohne die 2 kann die Mannschaft nur einen 3er-Spieltag ausrichten. |
| Halle | Spiellokal (Schritt 1.2, je Termin in 4.1) | Für den Spielplan und die Entfernungen. |

Die übrigen Felder der Maske (Koppelspiele, gesperrte Auswärtsspiele unter der Woche, Doppelspieltage) bleiben leer. An anderen Tagen als Sonntag wird nichts eingetragen.

## Schritt für Schritt

Für jede Mannschaft der Staffel die Terminwünsche öffnen und die Schritte der Maske durchgehen.

**1. Terminwünsche Vorrunde** (bzw. **2. Rückrunde**)

- Jede Zeile ist ein Spieltag (eine Woche). Eingetragen wird nur in der Spalte **So**.
- Format der Uhrzeit: `hh:mm`, für einen Ausweichtermin direkt dahinter ein `*` (z. B. `10:30*`).
- `frei` für Sonntage, an denen die Mannschaft nicht kann.
- Felder mit „-“ sind vom Verband gesperrt (Ranglisten, Meisterschaften, Feiertage) und lassen sich nicht füllen.
- Unter **1.2 Spiellokal** das übliche Spiellokal wählen.
- Achtung: Was ein Verein vorher selbst eingetragen hat, bleibt stehen. Alte Einträge an Werktagen (z. B. „Mo 17:45“) löschen, sonst gelten sie als Heimtermine.

**3. Koppelspiele** – nichts eintragen.

**4. Spiellokal und Zusatzinformationen**

- **4.1 / 4.2:** Für jeden Heimtermin das Spiellokal prüfen und bei **max. Heimspiele** `2` eintragen, wenn in der Halle zwei Spiele gleichzeitig gehen (dann ist ein 4er-Spieltag möglich). Sonst leer lassen.
- **4.3 Gesperrte Auswärtsspiele unter der Woche:** nichts ankreuzen.
- **4.4 Doppelspieltage:** nicht ankreuzen.
- **4.4 Bemerkungen:** besondere Wünsche des Vereins als Text, dazu bei Bedarf die Zeile `Nur Heim:` (nächster Abschnitt).

**5. Kontrolle** – Heimtermine, Sperrtermine und Bemerkungen prüfen, dann **Speichern**.

**Danach:** Wenn alle Mannschaften erfasst sind, wie gewohnt die Terminmeldung (XML) und den Bericht **Terminmeldungen (pdf)** herunterladen und im AndiGenerator öffnen bzw. in der Einrichtung unter *Daten aus click-TT ergänzen* laden.

## „Nur Heim“ in den Bemerkungen

Die Maske kennt keinen Eintrag für „an diesem Tag nur Heimspiel, nicht auswärts“. Dafür gibt es eine feste Zeile in den **Bemerkungen** (Schritt 4.4), die der AndiGenerator aus dem PDF liest:

```
Nur Heim: 18.10., 01.11., 07.03.
```

- Die Zeile beginnt mit `Nur Heim:` (Groß- und Kleinschreibung egal, auch `Nur Heimspiel:` geht).
- Danach die Sonntage, durch Komma getrennt, als `TT.MM.` oder `TT.MM.JJJJ`. Ohne Jahr gilt das Jahr, in dem der Tag in der Runde liegt.
- An diesen Tagen muss **auch** eine Uhrzeit in der Spalte So stehen. Fehlt sie, meldet das Programm einen Widerspruch.
- Weitere Bemerkungen dürfen davor oder danach stehen, am besten in eigenen Zeilen. Bemerkungen gelten für die ganze Saison, Vorrunde und Rückrunde stehen also in derselben Zeile.

Beispiel für ein vollständiges Bemerkungsfeld:

```
Nur Heim: 18.10., 01.11.
Bitte nicht vor 11 Uhr, wir haben morgens Training der Jüngsten.
```

## Woran das Programm die Kompaktspieltage erkennt

Die XML-Datei sagt nicht, dass eine Staffel in Kompaktspieltagen spielt (auch das Bemerkungsfeld der Gruppe kommt nicht mit). Das Programm fragt deshalb beim ersten Öffnen in der Einrichtung danach und merkt sich die Antwort mit der Staffel. Es schlägt „ja“ vor, wenn alle Heimtermine an Sonntagen liegen.
