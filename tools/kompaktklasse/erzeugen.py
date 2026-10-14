# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
"""Erzeugt eine erfundene Klasse mit Kompaktspieltagen (Jugend, Hinrunde) im Format des click-TT-Exports.

Für Bildschirmfotos und Anleitung: neun Mannschaften mit erfundenen Namen, Meldungen an allen Sonntagen der Hinrunde
(Heim mit zwei Tischen, auswärts, gesperrt), fester Zufall – die Datei ist bei jedem Lauf gleich. Hallenadressen und
Bemerkungen kommen erst mit dem (ebenfalls erfundenen) Bericht „Terminwünsche“ im Test dazu.

Aufruf im Repository: python3 tools/kompaktklasse/erzeugen.py
"""

import datetime as dt
import random
from pathlib import Path

ZIEL = Path("testdaten/kompakt/Jugend_19_Testliga_(HR).xml")
MANNSCHAFTEN = [
    "TTC Aowi", "TV Bekra", "SV Dlimo", "TTF Gorna", "TSG Helto", "TuS Kavre", "SG Lumpa", "DJK Morsi", "TTG Nefri",
]
BEGINN = dt.date(2026, 8, 17)
MITTE = dt.date(2026, 12, 7)
ENDE = dt.date(2027, 5, 9)


def sonntage():
    t = BEGINN + dt.timedelta(days=(6 - BEGINN.weekday()) % 7)
    while t < MITTE:
        yield t
        t += dt.timedelta(days=7)


def main():
    zufall = random.Random(19)
    tage = list(sonntage())
    teams = []
    for i, name in enumerate(MANNSCHAFTEN):
        halle = "Sporthalle " + name.split(" ")[-1]
        heim, sperren = set(), set()
        while len(heim) < 5:
            heim.add(zufall.choice(tage))
        while len(sperren) < 3:
            t = zufall.choice(tage)
            if t not in heim:
                sperren.add(t)
        spiele = "".join(
            f'<homeGame secondary="false" courtHall="{halle}" maxParallelGames="2" day="{t:%d.%m.%Y}" time="11:00" /> '
            for t in sorted(heim))
        frei = "".join(f'<noGame from="{t:%d.%m.%Y}" until="{t:%d.%m.%Y}" /> ' for t in sorted(sperren))
        teams.append(f"""
      <team gender="Jugend 19" clubId="{90001 + i}" number="1" id="{9900001 + i}">
        <name>{name}</name>
        <gameDays>
           {spiele}{frei}
        </gameDays>
        <sisterTeams>
        </sisterTeams>
      </team>""")

    text = f"""<?xml version="1.0" encoding="iso-8859-15"?>
<TT name="Jugend 19 Testliga (HR)" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="http://www.tt-liga.de/TTGenerator http://www.tt-liga.de/includes/TTGenerator.xsd" xmlns="http://www.tt-liga.de/TTGenerator" mid="{MITTE:%d.%m.%Y}" from="{BEGINN:%d.%m.%Y}" seasonType="STANDARD" until="{ENDE:%d.%m.%Y}" gender="Jugend 19" id="990001">
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
    ZIEL.parent.mkdir(parents=True, exist_ok=True)
    ZIEL.write_bytes(text.replace("\n", "\r\n").encode("iso-8859-15"))
    print(f"{ZIEL}: {len(MANNSCHAFTEN)} Mannschaften, {len(tage)} Sonntage")


if __name__ == "__main__":
    main()
