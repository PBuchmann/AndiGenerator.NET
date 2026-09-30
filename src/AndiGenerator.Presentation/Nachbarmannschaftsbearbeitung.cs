// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Öffnet den Dialog „Nachbarmannschaft“ und übernimmt das Ergebnis (von mehreren Seiten genutzt).</summary>
internal static class Nachbarmannschaftsbearbeitung
{
    /// <summary>Bearbeitet eine Nachbarmannschaft oder legt eine neue an.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="index">Position der Nachbarmannschaft oder <c>null</c> für eine neue.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche.</param>
    /// <returns><c>true</c>, wenn übernommen wurde.</returns>
    public static async Task<bool> BearbeitenAsync(Datenbearbeitung bearbeitung, string mannschaft, int? index, IOberflaeche oberflaeche)
    {
        Nachbarmannschaftsentwurf entwurf = bearbeitung.NachbarmannschaftBearbeiten(mannschaft, index);
        var dialog = new NachbarmannschaftsdialogViewModel(
            entwurf,
            oberflaeche,
            bearbeitung.Ligadaten.Beginn,
            e => bearbeitung.NachbarmannschaftPruefen(mannschaft, index, e));
        if (!await oberflaeche.NachbarmannschaftBearbeitenAsync(dialog))
        {
            return false;
        }

        bearbeitung.NachbarmannschaftUebernehmen(mannschaft, index, entwurf);
        return true;
    }
}
