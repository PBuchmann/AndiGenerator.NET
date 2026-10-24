# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
"""Erzeugt Beispielklassen für Kompaktspieltage im Format des click-TT-Exports aus Tabellen von myTischtennis.

Übernommen sind nur die Mannschaften (Name, Mannschafts-ID, Mannschaftsnummer) aus der Tabelle der Saison 2026/27
(abgerufen am 25.10.2026). Alles andere ist erfunden: Vereins-IDs, Hallen (nach dem Ort benannt), Heimtermine (Samstag
15 Uhr bzw. Sonntag 11 Uhr, je Halle zwei Tische frei) und Sperren an den Wochenenden der Hinrunde außerhalb der
Herbstferien, fester Zufall – die Dateien sind bei jedem Lauf gleich. Die Hallenadressen (nur Postleitzahl und Ort, für
Karte und Entfernungen) schreibt tools/kompaktklasse/mytischtennis-adressen.cs in die .modifications-Datei daneben.

Aufruf im Repository: python3 tools/kompaktklasse/mytischtennis.py
"""

import datetime as dt
import random
from pathlib import Path

ORDNER = Path("src/AndiGenerator.Desktop/Beispiele")

KLASSEN = [
    {
        # https://www.mytischtennis.de/click-tt/HeTTV/26--27/ligen/Hessenliga/gruppe/519096/tabelle/gesamt
        "name": "Jugend 19 Hessenliga (HR)",
        "id": 519096,
        "zufall": 2627,
        "herbstferien": (dt.date(2026, 10, 5), dt.date(2026, 10, 17)),
        "mannschaften": [
            # Name, Mannschafts-ID, Vereinsnummer (gleich = derselbe Verein), Mannschaftsnummer, Halle
            ("TTC G.-W. Staffel 1953", 3146940, 1, 1, "Sporthalle Staffel"),
            ("TV Eschersheim 1895", 3155427, 2, 1, "Sporthalle Eschersheim"),
            ("TV 1874 Bergen-Enkheim", 3143021, 3, 1, "Sporthalle Bergen-Enkheim"),
            ("SV Darmstadt 98", 3155928, 4, 1, "Sporthalle Darmstadt"),
            ("TTC OE Clarity Telefonie-Systeme Bad Homburg e.V.", 3150705, 5, 1, "Sporthalle Bad Homburg"),
            ("TTC 1957 Lampertheim", 3152914, 6, 1, "Sporthalle Lampertheim"),
            ("TV Niederrad", 3144406, 7, 1, "Sporthalle Niederrad"),
            ("TSV 1907 Allendorf/Lda", 3150546, 8, 1, "Sporthalle Allendorf"),
            ("TTC Langen 1950", 3154439, 9, 1, "Sporthalle Langen"),
            ("Neuenhainer TTV 1955", 3153269, 10, 1, "Sporthalle Neuenhain"),
        ],
    },
    {
        # https://www.mytischtennis.de/click-tt/WTTV/26--27/ligen/NRW-Liga_1/gruppe/522590/tabelle/gesamt
        "name": "Jugend 19 NRW-Liga 1 (HR)",
        "id": 522590,
        "zufall": 1926,
        "herbstferien": (dt.date(2026, 10, 17), dt.date(2026, 11, 1)),
        "mannschaften": [
            ("DJK Roland Rauxel", 3105642, 21, 1, "Sporthalle Rauxel"),
            ("SV Union Velbert", 3108782, 22, 1, "Sporthalle Velbert"),
            ("TSV Hahlen", 3105844, 23, 1, "Sporthalle Hahlen"),
            ("TTF Bönen", 3110579, 24, 1, "Sporthalle Bönen"),
            ("TTV Ronsdorf", 3108674, 25, 1, "Sporthalle Ronsdorf"),
            ("TSSV Bottrop", 3108720, 26, 1, "Sporthalle Bottrop"),
            ("RW Stiepel", 3108071, 27, 1, "Sporthalle Stiepel"),
            ("SV Union Velbert II", 3108692, 22, 2, "Sporthalle Velbert"),
        ],
    },
]
BEGINN = dt.date(2026, 8, 17)
MITTE = dt.date(2026, 12, 7)
ENDE = dt.date(2027, 5, 9)


def wochenenden(ferien):
    """Samstage und Sonntage von Mitte September bis Ende November ohne die Herbstferien."""
    t = dt.date(2026, 9, 12)
    while t < dt.date(2026, 11, 30):
        if t.weekday() in (5, 6) and not ferien[0] <= t <= ferien[1]:
            yield t
        t += dt.timedelta(days=1)


def klasse(k):
    zufall = random.Random(k["zufall"])
    tage = list(wochenenden(k["herbstferien"]))
    teams = []
    for name, nummer, verein, mannschaftsnummer, halle in k["mannschaften"]:
        heim, sperren = set(), set()
        while len(heim) < 5:
            heim.add(zufall.choice(tage))
        while len(sperren) < 3:
            t = zufall.choice(tage)
            if t not in heim:
                sperren.add(t)
        spiele = "".join(
            f'<homeGame secondary="false" courtHall="{halle}" maxParallelGames="2" day="{t:%d.%m.%Y}" '
            f'time="{"15:00" if t.weekday() == 5 else "11:00"}" /> '
            for t in sorted(heim))
        frei = "".join(f'<noGame from="{t:%d.%m.%Y}" until="{t:%d.%m.%Y}" /> ' for t in sorted(sperren))
        teams.append(f"""
      <team gender="Jugend 19" clubId="{92000 + verein}" number="{mannschaftsnummer}" id="{nummer}">
        <name>{name}</name>
        <gameDays>
           {spiele}{frei}
        </gameDays>
        <sisterTeams>
        </sisterTeams>
      </team>""")

    text = f"""<?xml version="1.0" encoding="iso-8859-15"?>
<TT name="{k["name"]}" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="http://www.tt-liga.de/TTGenerator http://www.tt-liga.de/includes/TTGenerator.xsd" xmlns="http://www.tt-liga.de/TTGenerator" mid="{MITTE:%d.%m.%Y}" from="{BEGINN:%d.%m.%Y}" seasonType="STANDARD" until="{ENDE:%d.%m.%Y}" gender="Jugend 19" id="{k["id"]}">
  <noGameDays />
  <mandatoryGameDays>
  </mandatoryGameDays>
  <predefinedGames>
  </predefinedGames>
  <existingSchedule>
  </existingSchedule>
  <teams>{"".join(teams)}
  </teams>
</TT>
"""
    ziel = ORDNER / (k["name"] + ".xml")
    ziel.parent.mkdir(parents=True, exist_ok=True)
    ziel.write_bytes(text.replace("\n", "\r\n").encode("iso-8859-15"))
    print(f"{ziel}: {len(k['mannschaften'])} Mannschaften, {len(tage)} Wochenendtage")


if __name__ == "__main__":
    for k in KLASSEN:
        klasse(k)
