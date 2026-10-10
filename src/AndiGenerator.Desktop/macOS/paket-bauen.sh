#!/usr/bin/env bash
# macOS-Paket: App-Paket AndiGenerator.NET.app aus einer eigenständigen Veröffentlichung, ad hoc signiert, als
# Disk-Image (.dmg) mit Verknüpfung zum Ordner „Programme“. Läuft nur auf macOS (codesign, hdiutil).
# Aufruf (Release-Workflow): paket-bauen.sh <rid> <version> <publish-ordner> <ziel-ordner>
#   rid: osx-arm64 (Macs mit Apple-Chip M1 …) oder osx-x64 (Macs mit Intel-Prozessor)
set -euo pipefail

rid="$1"
version="$2"
publish="$(realpath "$3")"
ziel="$(realpath "$4")"
hier="$(dirname "$(realpath "$0")")"

case "$rid" in
  osx-arm64 | osx-x64) name="macos-${rid#osx-}" ;;
  *) echo "Unbekannte Laufzeit: $rid" >&2; exit 1 ;;
esac

arbeit="$(mktemp -d)"
inhalt="$arbeit/dmg"
app="$inhalt/AndiGenerator.NET.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

# Programm (mit .NET-Laufzeit und Anleitung) nach Contents/MacOS, Symbol nach Contents/Resources.
cp -R "$publish/." "$app/Contents/MacOS/"
cp "$hier/AndiGenerator.NET.icns" "$app/Contents/Resources/"
chmod 755 "$app/Contents/MacOS/AndiGenerator.NET"

# Die Kurzversion muss rein numerisch sein (1.0.0-beta.1 → 1.0.0); die volle Version steht in CFBundleVersion.
sed -e "s/@VERSION@/$version/g" -e "s/@KURZVERSION@/${version%%-*}/g" "$hier/Info.plist" > "$app/Contents/Info.plist"
plutil -lint "$app/Contents/Info.plist"

# Ad-hoc-Signatur: Ohne Signatur startet auf Macs mit Apple-Chip gar nichts. Sie ersetzt keine Beglaubigung durch
# Apple (Notarisierung) – beim ersten Start fragt macOS deshalb nach (Anleitung: „Dennoch öffnen“).
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"

# Disk-Image: App-Paket und Verknüpfung zu /Applications zum Hineinziehen.
ln -s /Applications "$inhalt/Programme"
hdiutil create -volname "AndiGenerator.NET" -srcfolder "$inhalt" -ov -format UDZO "$ziel/AndiGeneratorNET-$version-$name.dmg"
rm -rf "$arbeit"
