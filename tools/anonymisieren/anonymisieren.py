#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
"""
Erzeugt aus den privaten Testdaten eine anonymisierte Kopie für das öffentliche Repository.

Alle Mannschafts-, Vereins- und Hallennamen werden wortweise durch Kunstwörter ersetzt (z. B. „TTC Beispiel IV“ →
„Tsfg Bfqa IV“). Jedes Wort bekommt überall dasselbe Kunstwort; alle Kunstwörter sind gleich lang und in derselben
Reihenfolge vergeben wie die Wörter (ordinal sortiert). Dadurch bleibt die ordinale Sortierung aller Namen und aller
„Heim Gast“-Verkettungen erhalten, und die Kosten, Pläne und CSV-Exporte ändern sich nicht. Staffelnamen, Termine,
IDs und Optionen bleiben unverändert (keine personenbezogenen Daten).

Aufruf (im Lösungsordner):  python3 tools/anonymisieren/anonymisieren.py [--testdaten ../Testdaten] [--referenz Referenz] [--ziel testdaten]
Die Quellen werden nur gelesen. Das Ziel wird überschrieben (Dateien, nicht gelöscht).
"""
import argparse
import html
import json
import re
import sys
from pathlib import Path

NAMENSATTRIBUTE = ["homeTeam", "guestTeam", "hometeamname", "guestteamname", "teamname", "teamnamea", "teamnameb",
                   "teamA", "teamB", "orgteamname", "courtHall"]
ATTR_RE = re.compile(r'(\s(' + "|".join(NAMENSATTRIBUTE) + r'|team)=")([^"]*)(")')
NAME_RE = re.compile(r'(<name>)([^<]*)(</name>)')
CSV_SPALTEN = [8, 10]  # Heim- und Gastmannschaft; die Spalte Spiellokal enthält nur Nummern


def _roemisch(i: int) -> str:
    einer = ["", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"]
    zehner = ["", "X", "XX", "XXX"]
    return zehner[i // 10] + einer[i % 10]


# Römische Mannschaftsnummern bleiben erhalten: Das Programm erkennt daran Mannschaften desselben Vereins.
ROEMISCH = frozenset(_roemisch(i) for i in range(1, 31))


# Wörter, die das Programm auswertet und die deshalb erhalten bleiben: römische Mannschaftsnummern (Mannschaften
# desselben Vereins) und „spielfrei“ (Spiele gegen „spielfrei“ werden beim click-TT-Import übergangen).
BEHALTEN = ROEMISCH | {"spielfrei"}

_ZEICHEN = [chr(c) for c in range(ord("A"), ord("Z") + 1)] + [chr(c) for c in range(ord("a"), ord("z") + 1)]
_KLEIN = _ZEICHEN[26:]


def _gemischt(n: int) -> str:
    """n-tes Wort aus vier Zeichen [A-Za-z], ordinal aufsteigend in n."""
    z = []
    for _ in range(4):
        z.append(_ZEICHEN[n % 52])
        n //= 52
    return "".join(reversed(z))


def _lesbar(n: int) -> str:
    """n-tes Wort aus einem Buchstaben [A-Za-z] und drei Kleinbuchstaben, ordinal aufsteigend in n."""
    rest = n % 26 ** 3
    return _ZEICHEN[n // 26 ** 3] + _KLEIN[rest // 676] + _KLEIN[rest // 26 % 26] + _KLEIN[rest % 26]


RAEUME = [(_lesbar, 52 * 26 ** 3), (_gemischt, 52 ** 4)]


def _erster_groesser(kandidat, groesse: int, unten: str) -> int:
    a, b = 0, groesse
    while a < b:
        m = (a + b) // 2
        if kandidat(m) > unten:
            b = m
        else:
            a = m + 1
    return a


def _erster_mindestens(kandidat, groesse: int, oben: str) -> int:
    a, b = 0, groesse
    while a < b:
        m = (a + b) // 2
        if kandidat(m) >= oben:
            b = m
        else:
            a = m + 1
    return a


def kunstwoerter(woerter):
    """
    Ordnet jedem Wort ein Kunstwort aus vier Buchstaben zu. Die Kunstwörter liegen ordinal zwischen denselben
    behaltenen Wörtern wie die Wörter und sind aufsteigend vergeben; ohne Rücksicht auf Groß-/Kleinschreibung eindeutig.
    Bevorzugt werden lesbare Kunstwörter (ein Buchstabe, dann Kleinbuchstaben); nur in engen Lücken gemischte.
    """
    import bisect
    grenzen = sorted(BEHALTEN)
    gruppen = {}
    for w in woerter:
        gruppen.setdefault(bisect.bisect_left(grenzen, w), []).append(w)
    ergebnis = {}
    benutzt = {k.lower() for k in grenzen}
    for luecke, liste in gruppen.items():
        unten = grenzen[luecke - 1] if luecke > 0 else ""
        oben = grenzen[luecke] if luecke < len(grenzen) else None
        for kandidat, groesse in RAEUME:
            a = _erster_groesser(kandidat, groesse, unten)
            b = _erster_mindestens(kandidat, groesse, oben) if oben else groesse
            frei = [n for n in range(a, b, max(1, (b - a) // (4 * len(liste) + 4))) if kandidat(n).lower() not in benutzt]
            if len(frei) >= 2 * len(liste) + 2:
                break
        else:
            raise SystemExit(f"Zu wenige Kunstwörter zwischen {unten!r} und {oben!r}")
        schritt = len(frei) // (len(liste) + 1)
        for k, w in enumerate(sorted(liste), start=1):
            ergebnis[w] = kandidat(frei[k * schritt])
            benutzt.add(ergebnis[w].lower())
    return ergebnis


def kodierung(roh: bytes) -> str:
    m = re.search(rb'encoding="([^"]+)"', roh[:200])
    return m.group(1).decode() if m else "utf-8"


def xml_lesen(pfad: Path):
    roh = pfad.read_bytes()
    enc = kodierung(roh)
    return roh.decode(enc), enc


def xml_namen(text: str):
    for m in ATTR_RE.finditer(text):
        yield html.unescape(m.group(3))
    for m in NAME_RE.finditer(text):
        yield html.unescape(m.group(2))


def csv_namen(pfad: Path):
    for zeile in pfad.read_bytes().decode("cp1252").split("\r\n")[1:]:
        felder = zeile.split(";")
        for i in CSV_SPALTEN:
            if i < len(felder) and felder[i]:
                yield felder[i]


class Anonymisierer:
    def __init__(self, namen):
        self.namen = {n for n in namen if n.strip()}
        woerter = sorted({w for n in self.namen for w in n.split(" ") if w})
        faelle = {}
        for w in woerter:
            faelle.setdefault(w.lower(), []).append(w)
        doppelt = [v for v in faelle.values() if len(v) > 1]
        if doppelt:
            print("Hinweis: Wörter, die sich nur in Groß-/Kleinschreibung unterscheiden:", doppelt, file=sys.stderr)
        self.wort = {r: r for r in BEHALTEN}
        self.wort.update(kunstwoerter([w for w in woerter if w not in BEHALTEN]))
        self.voll = {n: self.name(n) for n in self.namen}
        # Freitext: nur Namen ab zwei Zeichen, nur als ganzes Wort (sonst träfe z. B. „X“ jedes X).
        grenze = r"[0-9A-Za-zÄÖÜäöüß]"
        self.muster = [(re.compile(r"(?<!" + grenze + ")" + re.escape(n) + r"(?!" + grenze + ")"), self.voll[n])
                       for n in sorted(self.voll, key=len, reverse=True) if len(n) >= 2]

    def name(self, n: str) -> str:
        return " ".join(self.wort.get(w, w) if w else w for w in n.split(" "))

    def text(self, t: str) -> str:
        """Freitext (JSON, Markdown): ganze Namen ersetzen, die längsten zuerst."""
        for muster, ersatz in self.muster:
            t = muster.sub(lambda _: ersatz, t)
        return t

    def xml(self, text: str) -> str:
        def attr(m):
            return m.group(1) + self.name(html.unescape(m.group(3))) + m.group(4)
        text = ATTR_RE.sub(attr, text)
        return NAME_RE.sub(lambda m: m.group(1) + self.name(html.unescape(m.group(2))) + m.group(3), text)

    def csv(self, roh: bytes) -> bytes:
        zeilen = roh.decode("cp1252").split("\r\n")
        for z in range(1, len(zeilen)):
            felder = zeilen[z].split(";")
            for i in CSV_SPALTEN:
                if i < len(felder) and felder[i]:
                    felder[i] = self.name(felder[i])
            zeilen[z] = ";".join(felder)
        return "\r\n".join(zeilen).encode("cp1252")

    def json(self, wert):
        if isinstance(wert, dict):
            return {self.text(k): self.json(v) for k, v in wert.items()}
        if isinstance(wert, list):
            return [self.json(v) for v in wert]
        if isinstance(wert, str):
            return self.text(wert)
        return wert


def dateien(testdaten: Path, referenz: Path):
    """Liefert (Quelle, Ziel relativ) aller zu übernehmenden Dateien."""
    for p in sorted(testdaten.iterdir()):
        if p.is_file() and p.suffix in (".xml", ".modifications", ".csv"):
            yield p, Path("clicktt") / p.name
    for unter in ["Gemerkte Plaene", "Optionen (Stand 27.09.2026 19 Uhr)"]:
        for p in sorted((testdaten / unter).rglob("*")):
            if p.is_file():
                yield p, Path("clicktt") / p.relative_to(testdaten)
    for unter in ["eingabe", "werte", "perf", "datendialoge"]:
        for p in sorted((referenz / unter).rglob("*")):
            if p.is_file():
                yield p, Path("referenz") / p.relative_to(referenz)
    yield referenz / "README.md", Path("referenz") / "README.md"


def main():
    arg = argparse.ArgumentParser()
    arg.add_argument("--testdaten", default="../Testdaten")
    arg.add_argument("--referenz", default="Referenz")
    arg.add_argument("--ziel", default="testdaten")
    arg.add_argument("--zuordnung", default="Referenz/zuordnung-anonym.json", help="privat: Klarname → Kunstname")
    a = arg.parse_args()
    testdaten, referenz, ziel = Path(a.testdaten), Path(a.referenz), Path(a.ziel)
    liste = list(dateien(testdaten, referenz))

    namen = set()
    for quelle, _ in liste:
        if quelle.suffix in (".xml", ".modifications"):
            namen.update(xml_namen(xml_lesen(quelle)[0]))
        elif quelle.suffix == ".csv":
            namen.update(csv_namen(quelle))
        elif quelle.suffix == ".json":
            namen.update(json.loads(quelle.read_text(encoding="utf-8")).get("mannschaften", {}).keys())
    anon = Anonymisierer(namen)
    sortiert = sorted(anon.voll)
    if any(anon.voll[x] >= anon.voll[y] for x, y in zip(sortiert, sortiert[1:])):
        raise SystemExit("Fehler: Die Reihenfolge der Namen bleibt nicht erhalten.")
    klein = {}
    for w, k in anon.wort.items():
        if k not in BEHALTEN and klein.setdefault(k.lower(), w) != w:
            raise SystemExit(f"Fehler: Kunstwort {k} doppelt (ohne Groß-/Kleinschreibung).")

    for quelle, relativ in liste:
        z = ziel / relativ
        z.parent.mkdir(parents=True, exist_ok=True)
        if quelle.suffix in (".xml", ".modifications"):
            text, enc = xml_lesen(quelle)
            z.write_bytes(anon.xml(text).encode(enc))
        elif quelle.suffix == ".csv":
            z.write_bytes(anon.csv(quelle.read_bytes()))
        elif quelle.suffix == ".json":
            daten = anon.json(json.loads(quelle.read_text(encoding="utf-8")))
            z.write_text(json.dumps(daten, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
        elif quelle.suffix == ".md":
            z.write_text(anon.text(quelle.read_text(encoding="utf-8")), encoding="utf-8")
        elif quelle.suffix == ".options":
            z.write_bytes(quelle.read_bytes())
        else:
            print("übersprungen:", quelle, file=sys.stderr)
            continue

    zuordnung = {n: anon.voll[n] for n in sorted(anon.voll)}
    Path(a.zuordnung).write_text(json.dumps(zuordnung, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(f"{len(liste)} Dateien, {len(anon.namen)} Namen, {len(anon.wort)} Wörter anonymisiert.")


if __name__ == "__main__":
    main()
