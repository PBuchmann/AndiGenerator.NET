// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;

namespace AndiGenerator.Persistence.Gemeinsam;

/// <summary>
/// Ablage je Staffel unter <c>%LOCALAPPDATA%\Andi-Generator\&lt;Staffelname Jahr&gt;</c>
/// (Original <c>GetCurrentPlanDirectory</c>, <c>EncodeFileName</c>, <c>DecodeFileName</c>, <c>SyncSavedPlans</c>).
/// Dort liegen <c>AndiGenerator.options</c> und die gemerkten Pläne (<c>*.xml</c>).
/// </summary>
public static class Staffelordner
{
    /// <summary>Name des Programmordners unter <c>%LOCALAPPDATA%</c> – derselbe wie im Original, damit beide Programme dieselben Daten sehen.</summary>
    public const string ProgrammOrdner = "Andi-Generator";

    private const string Sonderzeichen = "<>\"[]/\\?*:|";

    /// <summary>
    /// Ordnerpfad einer Staffel: <c>EncodeFileName(Name + ' ' + Jahr(Beginn))</c>. Fehlt der Beginn, nimmt das Original
    /// das Jahr des Delphi-Nullpunkts (1899); so entsteht bei leeren Plänen der Ordner „ 1899“.
    /// </summary>
    /// <param name="basisordner">Basisordner, normalerweise <see cref="StandardBasis"/>.</param>
    /// <param name="staffelName">Name der Staffel (Attribut <c>name</c>).</param>
    /// <param name="beginn">Beginn der Staffel (Attribut <c>from</c>) oder <c>null</c>.</param>
    /// <returns>Der vollständige Ordnerpfad.</returns>
    public static string Pfad(string basisordner, string staffelName, DateOnly? beginn)
    {
        ArgumentNullException.ThrowIfNull(basisordner);
        ArgumentNullException.ThrowIfNull(staffelName);
        int jahr = beginn?.Year ?? DelphiKompatibel.DelphiNull.Year;
        return Path.Combine(basisordner, ProgrammOrdner, DateinameKodieren(staffelName + " " + jahr.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Standard-Basisordner: <c>%LOCALAPPDATA%</c>.</summary>
    /// <returns>Der Pfad von <c>%LOCALAPPDATA%</c>.</returns>
    public static string StandardBasis() => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>Delphi <c>EncodeFileName</c>: <c>#</c> und <c>&lt;&gt;"[]/\?*:|</c> werden zu <c>#</c> + dreistelligem Zeichencode.</summary>
    /// <param name="wert">Name mit beliebigen Zeichen.</param>
    /// <returns>Der als Dateiname verwendbare Text.</returns>
    public static string DateinameKodieren(string wert)
    {
        ArgumentNullException.ThrowIfNull(wert);
        var text = new StringBuilder(wert.Length);
        foreach (char z in wert)
        {
            if (z == '#' || Sonderzeichen.Contains(z, StringComparison.Ordinal))
            {
                text.Append('#').Append(((int)z).ToString("000", CultureInfo.InvariantCulture));
            }
            else
            {
                text.Append(z);
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Delphi <c>DecodeFileName</c>: <c>#</c> gefolgt von drei Zeichen, die <c>StrToInt</c> versteht, wird zum Zeichen
    /// mit diesem Code; alles andere bleibt stehen.
    /// </summary>
    /// <param name="wert">Kodierter Dateiname (ohne Endung).</param>
    /// <returns>Der ursprüngliche Name.</returns>
    public static string DateinameDekodieren(string wert)
    {
        ArgumentNullException.ThrowIfNull(wert);
        var text = new StringBuilder(wert.Length);
        int i = 0;
        while (i < wert.Length)
        {
            if (wert[i] == '#' && i + 3 < wert.Length && TryStrToInt(wert.Substring(i + 1, 3), out int code))
            {
                text.Append(unchecked((char)code)); // Delphi Char(Integer) schneidet ab
                i += 4;
            }
            else
            {
                text.Append(wert[i]);
                i++;
            }
        }

        return text.ToString();
    }

    /// <summary>Dateipfad eines gemerkten Plans.</summary>
    /// <param name="staffelordner">Ordner der Staffel.</param>
    /// <param name="planName">Anzeigename des Plans.</param>
    /// <returns>Der Pfad der <c>.xml</c>-Datei.</returns>
    public static string GemerkterPlanPfad(string staffelordner, string planName) =>
        Path.Combine(staffelordner, DateinameKodieren(planName) + ".xml");

    /// <summary>Gemerkte Pläne im Staffelordner, neueste zuerst (Original <c>TSavedPlanComparer</c>).</summary>
    /// <param name="staffelordner">Ordner der Staffel.</param>
    /// <returns>Die gemerkten Pläne; leer, wenn der Ordner fehlt.</returns>
    public static IReadOnlyList<GemerkterPlanEintrag> GemerktePlaene(string staffelordner)
    {
        if (!Directory.Exists(staffelordner))
        {
            return [];
        }

        return Directory.EnumerateFiles(staffelordner, "*.xml")
            .Where(p => string.Equals(Path.GetExtension(p), ".xml", StringComparison.OrdinalIgnoreCase))
            .Select(p => new GemerkterPlanEintrag(DateinameDekodieren(Path.GetFileNameWithoutExtension(p)), p, File.GetLastWriteTime(p)))
            .OrderByDescending(e => e.Geaendert)
            .ToList();
    }

    private static bool TryStrToInt(string text, out int zahl)
    {
        try
        {
            zahl = DelphiKompatibel.StrToInt(text);
            return true;
        }
        catch (FormatException)
        {
            zahl = 0;
            return false;
        }
        catch (OverflowException)
        {
            zahl = 0;
            return false;
        }
    }
}
