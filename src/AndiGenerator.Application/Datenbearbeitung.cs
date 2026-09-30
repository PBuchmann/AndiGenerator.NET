// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>
/// Arbeitskopie der Spielplandaten für den Dialog „Spielplandaten bearbeiten“ (Original <c>TDialogForMultiplePanel</c>).
/// Die Seiten lesen und schreiben wie im Original direkt im Datenbaum, damit die gespeicherten Änderungen
/// (<c>.modifications</c>) dieselben sind. Erst <see cref="Plansitzung.DatenUebernehmen"/> übernimmt die Kopie.
/// Der Standard ist der unveränderte click-TT-Import; bei eigenen Plandateien gibt es keinen.
/// </summary>
public sealed partial class Datenbearbeitung
{
    private readonly DatenKnoten? standard;

    /// <summary>Initialisiert die Arbeitskopie.</summary>
    /// <param name="stand">Aktueller Stand (wird kopiert).</param>
    /// <param name="standard">Unveränderter click-TT-Import oder <c>null</c>.</param>
    internal Datenbearbeitung(DatenKnoten stand, DatenKnoten? standard)
    {
        Arbeitsstand = stand.Kopie();
        this.standard = standard;
    }

    /// <summary>Holt einen Wert, der angibt, ob es einen Standard (click-TT-Import) gibt („Standard wiederherstellen“).</summary>
    public bool HatStandard => standard is not null;

    /// <summary>Holt oder setzt die allgemeinen Daten der Staffel (Original <c>DataToForm</c>/<c>FormToData</c>).</summary>
    public Ligadaten Ligadaten
    {
        get => LigadatenLesen(Arbeitsstand);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Arbeitsstand.Setzen("name", value.Name);
            Arbeitsstand.Setzen("gender", value.Art);
            Arbeitsstand.Setzen("id", value.Id);
            Arbeitsstand.Setzen("from", DatumText(value.Beginn));
            Arbeitsstand.Setzen("until", DatumText(value.Ende));
            Arbeitsstand.Setzen("mid", DatumText(value.Rueckrundenbeginn));
        }
    }

    /// <summary>Holt die allgemeinen Daten des Standards oder <c>null</c> ohne Standard.</summary>
    public Ligadaten? StandardLigadaten => standard is null ? null : LigadatenLesen(standard);

    /// <summary>Holt die Arbeitskopie.</summary>
    internal DatenKnoten Arbeitsstand { get; }

    private static DateOnly Nullpunkt => DateOnly.FromDateTime(DelphiKompatibel.DelphiNull);

    /// <summary>
    /// Original <c>TDialogPanelMainData.DataToForm</c>: Ohne Vorrundenbeginn werden 1.7., 30.12. und 1.5. des Folgejahres
    /// vorgeschlagen; fehlt nur Rückrundenbeginn oder Ende, steht dort wie im Original der Nullpunkt 30.12.1899.
    /// </summary>
    private static Ligadaten LigadatenLesen(DatenKnoten plan)
    {
        DateOnly beginn = Datum(plan.Lesen("from"));
        DateOnly mitte = Datum(plan.Lesen("mid"));
        DateOnly ende = Datum(plan.Lesen("until"));
        if (beginn == Nullpunkt)
        {
            int jahr = DateTime.Today.Year;
            beginn = new DateOnly(jahr, 7, 1);
            mitte = new DateOnly(jahr, 12, 30);
            ende = new DateOnly(jahr + 1, 5, 1);
        }

        return new Ligadaten(plan.Lesen("name"), plan.Lesen("id"), plan.Lesen("gender"), beginn, mitte, ende);
    }

    private static DateOnly Datum(string text) => text.Length == 0 ? Nullpunkt : DelphiKompatibel.DateFromString(text);

    private static string DatumText(DateOnly datum) => datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}
