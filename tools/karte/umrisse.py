# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
"""Erzeugt src/AndiGenerator.Persistence/Geo/umrisse-de.json aus ne_10m_admin_1_states_provinces.zip von Natural Earth
(https://www.naturalearthdata.com, gemeinfrei): die 16 Bundesländer als vereinfachte Polygone (Länge, Breite).

Aufruf: python tools/karte/umrisse.py <ne_10m_admin_1_states_provinces.zip> [ziel.json]
Nur Standardbibliothek; liest Shapefile und DBF selbst.
"""
import io
import json
import pathlib
import struct
import sys
import zipfile

TOLERANZ = 0.004  # Grad, etwa 300-400 m
STELLEN = 3


def dbf_lesen(daten):
    anzahl, kopf, satz = struct.unpack("<IHH", daten[4:12])
    felder, pos = [], 32
    while daten[pos] != 0x0D:
        name = daten[pos:pos + 11].split(b"\0")[0].decode("latin-1").lower()
        felder.append((name, daten[pos + 16]))
        pos += 32
    for i in range(anzahl):
        roh = daten[kopf + i * satz + 1: kopf + (i + 1) * satz]
        werte, p = {}, 0
        for name, laenge in felder:
            werte[name] = roh[p:p + laenge].decode("utf-8", "replace").replace("\0", "").strip()
            p += laenge
        yield werte


def polygon_lesen(shp, shx, index):
    offset = struct.unpack(">i", shx[100 + index * 8: 104 + index * 8])[0] * 2
    inhalt = offset + 8
    typ = struct.unpack("<i", shp[inhalt:inhalt + 4])[0]
    if typ != 5:
        return []
    teile, punkte = struct.unpack("<ii", shp[inhalt + 36:inhalt + 44])
    starts = struct.unpack(f"<{teile}i", shp[inhalt + 44:inhalt + 44 + 4 * teile])
    p0 = inhalt + 44 + 4 * teile
    xy = struct.unpack(f"<{2 * punkte}d", shp[p0:p0 + 16 * punkte])
    pts = [(xy[2 * k], xy[2 * k + 1]) for k in range(punkte)]
    grenzen = list(starts) + [punkte]
    return [pts[grenzen[j]:grenzen[j + 1]] for j in range(teile)]


def vereinfachen(pts, tol):
    if len(pts) < 3:
        return pts
    stapel, behalten = [(0, len(pts) - 1)], {0, len(pts) - 1}
    while stapel:
        a, b = stapel.pop()
        (ax, ay), (bx, by) = pts[a], pts[b]
        dx, dy = bx - ax, by - ay
        laenge = (dx * dx + dy * dy) ** 0.5
        best, idx = -1.0, -1
        for k in range(a + 1, b):
            px, py = pts[k]
            d = abs(dy * (px - ax) - dx * (py - ay)) / laenge if laenge else ((px - ax) ** 2 + (py - ay) ** 2) ** 0.5
            if d > best:
                best, idx = d, k
        if best > tol:
            behalten.add(idx)
            stapel += [(a, idx), (idx, b)]
    return [pts[k] for k in sorted(behalten)]


def main():
    z = zipfile.ZipFile(sys.argv[1])
    basis = "ne_10m_admin_1_states_provinces"
    shp, shx, dbf = (z.read(f"{basis}.{e}") for e in ("shp", "shx", "dbf"))
    laender = []
    for i, w in enumerate(dbf_lesen(dbf)):
        if w.get("adm0_a3") != "DEU":
            continue
        ringe = []
        for ring in polygon_lesen(shp, shx, i):
            v = vereinfachen(ring, TOLERANZ)
            if len(v) >= 4:
                ringe.append([[round(x, STELLEN), round(y, STELLEN)] for x, y in v])
        laender.append({"name": w.get("name_de") or w.get("name"), "ringe": ringe})
    laender.sort(key=lambda l: l["name"])
    ziel = pathlib.Path(sys.argv[2]) if len(sys.argv) > 2 else pathlib.Path(__file__).resolve().parents[2] / "src" / "AndiGenerator.Persistence" / "Geo" / "umrisse-de.json"
    ziel.write_text(json.dumps({"quelle": "Natural Earth 1:10m Admin 1 (gemeinfrei), vereinfacht", "laender": laender}, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(len(laender), "Länder,", sum(len(r) for l in laender for r in l["ringe"]), "Punkte,", ziel.stat().st_size, "Bytes")


if __name__ == "__main__":
    main()
