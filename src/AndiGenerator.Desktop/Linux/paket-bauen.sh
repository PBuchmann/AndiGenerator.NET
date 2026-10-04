#!/usr/bin/env bash
# Linux-Pakete (u. a. für Chromebooks mit Linux-Entwicklungsumgebung): Debian-Paket und portables tar.gz aus einer
# eigenständigen Veröffentlichung. Aufruf (Release-Workflow): paket-bauen.sh <rid> <version> <publish-ordner> <ziel-ordner>
#   rid: linux-x64 (Intel/AMD-Chromebooks) oder linux-arm64 (ARM-Chromebooks)
set -euo pipefail

rid="$1"
version="$2"
publish="$(realpath "$3")"
ziel="$(realpath "$4")"
hier="$(dirname "$(realpath "$0")")"

case "$rid" in
  linux-x64) arch=amd64 ;;
  linux-arm64) arch=arm64 ;;
  *) echo "Unbekannte Laufzeit: $rid" >&2; exit 1 ;;
esac

# Portables Archiv: entpacken und ./AndiGenerator.NET starten.
tar -czf "$ziel/AndiGeneratorNET-$version-$rid.tar.gz" -C "$publish" .

# Debian-Paket: Programm unter /opt, Aufruf andigenerator-net, Eintrag im Startmenü (auf dem Chromebook im Launcher).
wurzel="$(mktemp -d)"
mkdir -p "$wurzel/DEBIAN" "$wurzel/opt/andigenerator-net" "$wurzel/usr/bin" \
  "$wurzel/usr/share/applications" "$wurzel/usr/share/icons/hicolor/256x256/apps"
cp -r "$publish/." "$wurzel/opt/andigenerator-net/"
ln -s /opt/andigenerator-net/AndiGenerator.NET "$wurzel/usr/bin/andigenerator-net"
cp "$hier/andigenerator-net.desktop" "$wurzel/usr/share/applications/"
cp "$hier/andigenerator-net.png" "$wurzel/usr/share/icons/hicolor/256x256/apps/"

# Rechte wie bei Systempaketen: Ordner 755, Dateien 644, das Programm ausführbar.
chmod 755 "$wurzel"
find "$wurzel" -type d -exec chmod 755 {} +
find "$wurzel" -type f -exec chmod 644 {} +
chmod 755 "$wurzel/opt/andigenerator-net/AndiGenerator.NET"

# Debian-Versionen: „-“ trennt die Paketrevision ab, Vorabversionen (1.0.0-beta.1) werden deshalb zu 1.0.0~beta.1.
debversion="${version//-/\~}"
groesse="$(du -sk --exclude=DEBIAN "$wurzel" | cut -f1)"
cat > "$wurzel/DEBIAN/control" <<STEUER
Package: andigenerator-net
Version: $debversion
Architecture: $arch
Maintainer: Peter Buchmann <PBuchmann@users.noreply.github.com>
Installed-Size: $groesse
Depends: libc6, libfontconfig1, libx11-6, libice6, libsm6, libicu76 | libicu74 | libicu72 | libicu71 | libicu70 | libicu67, libgtk-3-0t64 | libgtk-3-0
Section: misc
Priority: optional
Homepage: https://github.com/PBuchmann/AndiGenerator.NET
Description: Spielplan-Generator für Tischtennis-Staffeln
 Portierung des AndiGenerators von Andreas Hofmann (GPL-3.0). Erstellt Spielpläne
 aus den in click-TT gemeldeten Terminwünschen und exportiert sie für click-TT.
STEUER

dpkg-deb --root-owner-group --build "$wurzel" "$ziel/andigenerator-net_${debversion}_${arch}.deb"
rm -rf "$wurzel"
