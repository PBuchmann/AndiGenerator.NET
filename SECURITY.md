# Sicherheitsrichtlinie

*English speakers: please use the same reporting channel described below; reports in English are welcome.*

## Unterstützte Versionen

AndiGenerator.NET ist ein Freizeitprojekt mit einem Entwickler. Sicherheitsupdates gibt es nur für die **jeweils neueste Version** auf der Seite [Releases](https://github.com/PBuchmann/AndiGenerator.NET/releases). Das installierte Programm aktualisiert sich selbst; bitte prüfen Sie ein Problem vor der Meldung mit der neuesten Version.

| Version | Unterstützt |
| ------- | ----------- |
| neueste Version (aktuell 0.9.x) | :white_check_mark: |
| ältere Versionen | :x: |

## Eine Sicherheitslücke melden

**Bitte melden Sie Sicherheitslücken nicht als öffentliches Issue**, sondern vertraulich über GitHub:

1. Im Repository auf den Reiter **Security** gehen.
2. **Report a vulnerability** wählen und das Formular ausfüllen.

Hilfreich sind: betroffene Version, Windows-Version, eine Beschreibung der Lücke, die Schritte zum Nachvollziehen und gegebenenfalls eine Beispieldatei. Beispieldateien bitte ohne echte personenbezogene Daten.

**Was Sie erwarten können**

- Eine Eingangsbestätigung in der Regel innerhalb von **14 Tagen**.
- Eine erste Einschätzung, ob und wie die Lücke behoben wird, nach Möglichkeit innerhalb von **30 Tagen**.
- Wird die Lücke bestätigt, erscheint die Behebung in einer neuen Version; auf Wunsch werden Sie in den Versionshinweisen genannt. Wird sie nicht als Sicherheitslücke eingestuft, erhalten Sie eine Begründung.
- Bitte veröffentlichen Sie Einzelheiten erst, wenn eine behobene Version bereitsteht oder 90 Tage vergangen sind.

## Was in den Geltungsbereich fällt

- das Programm selbst, insbesondere das Einlesen von Dateien (click-TT-XML, `.modifications`, Optionen, gemerkte Pläne),
- Installation und automatische Updates (Velopack über GitHub Releases),
- die von diesem Repository veröffentlichten Dateien (Setup, Portable-ZIP),
- **Bibliotheken, soweit AndiGenerator.NET betroffen ist:** wenn das Programm eine Bibliotheksversion mit bekannter Sicherheitslücke ausliefert oder eine Bibliothek so verwendet, dass dadurch eine Lücke entsteht. Bitte auch das vertraulich hier melden.

Eine Lücke **in der Bibliothek selbst** (in ihrem Code, unabhängig von AndiGenerator.NET) melden Sie bitte zuerst beim jeweiligen Anbieter (siehe [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)); ein kurzer vertraulicher Hinweis hier hilft uns, rechtzeitig eine korrigierte Version auszuliefern. Nicht dazu gehören Lücken in click-TT selbst oder in Windows. Fehler ohne Sicherheitsbezug gehören in die normalen [Issues](https://github.com/PBuchmann/AndiGenerator.NET/issues).

## Datenschutz

Das Programm arbeitet lokal. Es verbindet sich nur mit GitHub, um nach einer neuen Version zu suchen und sie herunterzuladen; dabei werden keine Daten aus Ihren Staffeln übertragen.
