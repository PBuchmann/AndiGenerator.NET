# Richtlinie zur Code-Signierung

*Code signing policy – English summary below.*

## Signierung

Die Programmdateien und das Setup von AndiGenerator.NET sollen künftig digital signiert werden, damit Windows sie als vertrauenswürdig erkennt.

**Kostenlose Code-Signierung bereitgestellt von [SignPath.io](https://about.signpath.io), Zertifikat von der [SignPath Foundation](https://signpath.org).**
*(Die Aufnahme in das Programm der SignPath Foundation ist beantragt. Bis zur Zusage sind die veröffentlichten Dateien noch nicht signiert.)*

Signiert werden ausschließlich Dateien, die aus dem Quellcode dieses Repositorys gebaut werden:

- `AndiGenerator.NET.exe` und die Programmbibliotheken `AndiGenerator.*.dll`,
- das Setup `AndiGeneratorNET-win-Setup.exe`.

Mitgelieferte Bibliotheken anderer Anbieter (siehe [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)) werden nicht von uns signiert.

## Ablauf

1. Änderungen gelangen nur über Pull Requests nach `main` (siehe [CONTRIBUTING.md](CONTRIBUTING.md)). Ein Ruleset auf GitHub verlangt, dass der Build mit allen Tests und die CodeQL-Prüfung erfolgreich sind; Beiträge Dritter brauchen die Freigabe eines Prüfers.
2. Eine neue Version entsteht, wenn ein Tag wie `v1.2.3` auf `main` gesetzt wird. GitHub Actions baut daraus mit dem Workflow [`release.yml`](.github/workflows/release.yml) die Programmdateien und das Setup – ausschließlich auf den Build-Servern von GitHub, nie auf einem privaten Rechner.
3. Jede Signierung wird vor der Ausführung von einem Freigeber in SignPath **einzeln von Hand bestätigt**.
4. Das Ergebnis wird als Release auf GitHub veröffentlicht – zusammen mit einer Stückliste aller enthaltenen Bibliotheken (SBOM, SPDX-Format) und einem Herkunftsnachweis von GitHub, der bestätigt, dass Setup und Pakete aus genau diesem Commit auf GitHub Actions gebaut wurden. Prüfen lässt sich das mit `gh attestation verify <Datei> --repo PBuchmann/AndiGenerator.NET`.

## Rollen

| Rolle | Aufgabe | Personen |
|---|---|---|
| Committer und Prüfer | ändern den Quellcode, prüfen Pull Requests Dritter | [Peter Buchmann (@PBuchmann)](https://github.com/PBuchmann) |
| Freigeber | bestätigen jede Signierung | [Peter Buchmann (@PBuchmann)](https://github.com/PBuchmann) |

Alle Beteiligten nutzen für GitHub und SignPath eine Zwei-Faktor-Anmeldung.

## Datenschutz

AndiGenerator.NET arbeitet lokal auf Ihrem Rechner. Es überträgt keine Daten an andere Systeme – mit einer Ausnahme:

- **Update-Suche:** Das installierte Programm fragt beim Start GitHub (github.com) nach der neuesten Version und lädt sie gegebenenfalls herunter. Dabei werden keine Daten aus Ihren Staffeln übertragen; GitHub erhält nur die bei jedem Webzugriff üblichen technischen Angaben (z. B. die IP-Adresse), siehe die [Datenschutzerklärung von GitHub](https://docs.github.com/de/site-policy/privacy-policies/github-general-privacy-statement). Die Suche lässt sich im Dialog **Über** mit „Beim Start nach neuen Versionen suchen“ abschalten.

Links, die Sie im Programm anklicken (z. B. zum Lizenztext), öffnen Ihren Browser; das geschieht nur auf Ihre Aktion hin.

---

### English summary

Free code signing provided by SignPath.io, certificate by SignPath Foundation (application pending; releases are unsigned until approval). Only binaries built from this repository by GitHub Actions are signed, and every signing request is approved manually. Committer, reviewer and approver: Peter Buchmann (@PBuchmann), using multi-factor authentication. Privacy: the program does not transfer any data to other networked systems, except for an update check against GitHub at startup, which sends no user data and can be turned off in the *About* dialog.
