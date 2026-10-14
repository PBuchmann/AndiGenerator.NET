# Projektwissen für die Arbeit mit Claude

Ergänzt `CLAUDE.md` (Regeln) um das, was sich in der Zusammenarbeit zwischen Peter und Claude angesammelt hat: wie wir
arbeiten, was Peter wichtig ist, wie die Kompaktspieltage gebaut sind und was offen ist. Stand: 14.10.2026 (Commit f3a2142).
Entscheidungen mit Begründung stehen ausführlich im [MIGRATIONSPLAN](MIGRATIONSPLAN.md); Änderungen für Anwender im
[CHANGELOG](../CHANGELOG.md).

## Arbeitsweise

- **Ablauf je Änderung:** verstehen → umsetzen (mit Tests) → bauen und testen → Peter prüft in der Oberfläche
  (oft mit Bildschirmfoto) → nachbessern → CHANGELOG/Anleitung → Commit nach grünem Build.
- **Rückmeldungen** kommen kurz, oft mit Bildschirmfoto. Erst die Ursache benennen, dann die Lösung; nichts als
  „funktioniert“ melden, was nicht gebaut und – wo möglich – geprüft ist.
- **Antworten** auf Deutsch, knapp; bei Fragen zur Gestaltung lieber einmal kurz nachfragen als groß umbauen.
- **Branches:** Gearbeitet wird auf Feature-Branches (derzeit `feature/setzliste-qttr`); Peter führt sie über Pull
  Requests auf GitHub nach `main` zusammen und pusht selbst.
- **Werkzeuge:** `bauen.cmd` (Build + Tests, Protokoll `build-log.txt`), `bildschirmfotos.cmd` (Bilder der Anleitung,
  Protokoll `bildschirmfotos-log.txt`), `starten.cmd` (Programm starten), `abdeckung.cmd` (Testabdeckung).
- **Langsame/zeitabhängige Tests:** HiGHS- und Kompaktplaner-Tests (`KompaktplanerTests`, Sammlung `MessungenSammlung`)
  laufen seriell und hängen von der Rechnerlast ab. Zeitgrenzen nur erhöhen, wenn die Suche dann von selbst früher endet
  (vollständige 4er-Suche ja; HiGHS-Schritte mit Ausschluss-Schnitten nein – die rechnen ihren ganzen Zeitanteil).
  Lieber die Zusicherung robust machen.

## Was Peter wichtig ist (gelernte Vorlieben)

- Oberfläche ruhig und verständlich; Hinweise erst zeigen, wenn sie etwas bedeuten (z. B. keine Hinweise ohne Plan).
- Karten: gerade Linien (keine Bögen), Pfeilspitzen am Rand des Ziels, Ausgangshalle grün, Ziel in Linienfarbe,
  zweifarbige Pfeile aus zwei Hälften (nicht dicker als ein einfacher Pfeil), kurze Fahrten im Vordergrund mit hellem
  Rand, Namen nie abgeschnitten, Maßstab nur „N km“. Die Feinheiten (Abstände in Pixeln) stehen als Kommentare im
  `Kartenzeichner` – nicht ohne Rückfrage ändern.
- Raster: ★ Ausrichter, ● Gast, zentriert; Ring schwarz = Spitzenspieltag, blau = Abstiegsspieltag; Einzelbegegnung gelb.
- Druck für die Vereine: nur, was Vereine brauchen (keine Hinweise für die Staffelleitung wie „Halle verlegen“ oder
  Verstöße); jede Mannschaft genau eine Seite; großzügige Abstände.
- Gewichte der Kompaktplanung bleiben vorerst fest im Code (einstellbar erst auf Nachfrage, siehe MIGRATIONSPLAN 12).
- Signierung darf nichts kosten.

## Kompaktspieltage (Jugend, Halbrunde) – Kurzüberblick

- **Formen:** 4er-Spieltag (Ausrichter + Partner je gegen die zwei Gäste, 4 Spiele in 2 Runden), 3er-Spieltag (jeder
  gegen jeden), Einzelbegegnung (bei gerader Mannschaftszahl; unter der Woche bis 60 km).
- **Planung:** `Kompaktplaner`/`Kompaktlauf` (Engine). Gezielte Konstruktion als ganzzahlige Optimierung mit HiGHS
  (`Kompaktoptimierung`, `Ganzzahlmodell`); Portfolio paralleler Läufe mit verschiedenen Startwerten, der erste Beweis
  beendet die anderen; Zufallssuche liefert Startpunkte. Ohne HiGHS: vollständige 4er-Suche (`Viererbaum`) und Zufallssuche.
- **Punkte** (`Kompaktgesamt`, `Kompaktpunkte`): 10 je Verstoß, 15 je Mannschaft ohne 3er/4er-Heimspieltag (+2 ohne
  jedes Heimspiel), 3 je Dreier wenn reiner 4er-Plan möglich, 3 je fehlendem Spitzen-/Abstiegsspieltag (+0,5 früh),
  km-Punkte 10 · km/Bezug (Einzel-km doppelt), Stand, gemeinsamer Spieltag, gleich weite Fahrten.
- **Meldungen im Raster** (`Datenbearbeitung.Kompaktmeldungen`): Heim/Auswärts/Beides/Kann nicht → Heimtermin,
  Sperrtermin, Zeile „Nur Heim: …“ in der Bemerkung; Uhrzeit 00:00 = Standard (3er 10 Uhr, 4er 11 Uhr).
- **Gespeicherte Pläne** speichern das Spiellokal (Attribut `location`, nur wenn vorhanden); daran ist der Ausrichter
  beim 4er-Spieltag wieder erkennbar.
- **Export für click-TT:** Prüfung mit den Kompakt-Hinweisen; Spiellokal nur bei Spielen des Ausrichters
  (`Spiellokale.AmTag`); die Spiele der zweiten „Heimmannschaft“ verlegt die Staffelleitung in click-TT.
- **Druck** (`Kompaktdruck`): Raster mit Legende → Karte aller Fahrten mit km je Mannschaft → Spieltage → je Mannschaft
  eine Seite mit Karte ihrer Fahrten.
- **Testdaten:** echte Klassen nur in `Referenz/jugend` (privat). Öffentlich die erfundene Klasse
  `testdaten/kompakt/Jugend_19_Testliga_(HR).xml` (`python3 tools/kompaktklasse/erzeugen.py`).

## Anleitung

- Quelle `Doku/ANLEITUNG.md` (Kapitel 1–13; Kompaktspieltage in 6.8, Druck in 9.2), Bilder `Doku/Anleitung/Bilder`
  (01–34), PDF `Doku/Anleitung.pdf` (wird mit dem Programm ausgeliefert).
- Bilder neu: `bildschirmfotos.cmd` (Test `tests/AndiGenerator.UI.Tests/Bildschirmfotos.cs`; neue Bilder dort ergänzen).
- PDF neu: pandoc + Node/Playwright, siehe `tools/anleitung/README.md`. Auf Windows müssen pandoc und Node mit Playwright
  installiert sein; alternativ in einer Linux-Umgebung erzeugen.
- Nach Änderungen an der Oberfläche: Bilder neu erzeugen, ansehen, dann PDF neu erzeugen.

## Offene Punkte (Stand 14.10.2026)

- Release 0.9.8 vorbereiten (CHANGELOG „Unveröffentlicht“ ist gefüllt).
- Gewichte der Kompaktplanung einstellbar – erst auf Nachfrage (MIGRATIONSPLAN 12).
- Idee: 6er-Spieltage (vier Mannschaften, jeder gegen jeden) bei gerader Mannschaftszahl (10/12) statt vieler
  Einzelbegegnungen – vorher klären, ob die Wettspielordnung drei Spiele am Tag erlaubt.
- Kompaktspieltage: Zeitwünsche aus den Bemerkungen, Routenplaner statt Luftlinie.
- Testabdeckung der Presentation-Schicht (Bearbeiten im Raster) könnte höher sein.
- Optional: Im Wunschtermin-Dialog „Standard“ statt 00:00 anzeigen.
- Optional: Langsame Planer-Tests als eigene Kategorie, damit `bauen.cmd` schneller ist.
