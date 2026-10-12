# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
"""Erzeugt src/AndiGenerator.Persistence/Geo/plz-de.csv.gz aus DE.zip von GeoNames
(https://download.geonames.org/export/zip/DE.zip, CC BY 4.0): je Postleitzahl und Ort die mittlere Lage.

Aufruf: python tools/geonames/plz_tabelle.py <DE.zip>
"""
import collections
import gzip
import io
import pathlib
import sys
import zipfile

ziel = pathlib.Path(__file__).resolve().parents[2] / "src" / "AndiGenerator.Persistence" / "Geo" / "plz-de.csv.gz"
text = zipfile.ZipFile(sys.argv[1]).read("DE.txt").decode("utf-8").splitlines()
orte = collections.defaultdict(list)
for zeile in text:
    f = zeile.split("\t")
    if len(f) >= 11 and f[9]:
        orte[(f[1], f[2].replace(";", ","))].append((float(f[9]), float(f[10])))
zeilen = ["# Postleitzahl;Ort;Breite;Länge – aus GeoNames (www.geonames.org), CC BY 4.0; je Postleitzahl und Ort gemittelt (tools/geonames)"]
zeilen += [f"{p};{o};{sum(a for a, _ in v) / len(v):.4f};{sum(b for _, b in v) / len(v):.4f}" for (p, o), v in sorted(orte.items())]
puffer = io.BytesIO()
with gzip.GzipFile(fileobj=puffer, mode="wb", mtime=0, filename="") as g:
    g.write(("\n".join(zeilen) + "\n").encode("utf-8"))
ziel.write_bytes(puffer.getvalue())
print(f"{len(zeilen) - 1} Einträge nach {ziel}")
