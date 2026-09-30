// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Allgemein“ (Original <c>TDialogPanelMainData</c>): Name, Liganummer, Art und Rundendaten.</summary>
public sealed class LigadatenSeiteViewModel : DatenSeiteViewModel
{
    private string name = string.Empty;
    private string id = string.Empty;
    private string art = string.Empty;
    private DateTime? beginn;
    private DateTime? rueckrundenbeginn;
    private DateTime? ende;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public LigadatenSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "Allgemein")
    {
        Laden();
    }

    /// <summary>Holt die vorgeschlagenen Arten.</summary>
    public static IReadOnlyList<string> Arten => Ligadaten.Arten;

    /// <summary>Holt oder setzt den Namen der Liga.</summary>
    public string Name
    {
        get => name;
        set => Setzen(ref name, value);
    }

    /// <summary>Holt oder setzt die Liganummer.</summary>
    public string Id
    {
        get => id;
        set => Setzen(ref id, value);
    }

    /// <summary>Holt oder setzt die Art (Herren/Damen …).</summary>
    public string Art
    {
        get => art;
        set => Setzen(ref art, value);
    }

    /// <summary>Holt oder setzt den Start der Vorrunde.</summary>
    public DateTime? Beginn
    {
        get => beginn;
        set => Setzen(ref beginn, value);
    }

    /// <summary>Holt oder setzt den Start der Rückrunde.</summary>
    public DateTime? Rueckrundenbeginn
    {
        get => rueckrundenbeginn;
        set => Setzen(ref rueckrundenbeginn, value);
    }

    /// <summary>Holt oder setzt das Ende der Rückrunde.</summary>
    public DateTime? Ende
    {
        get => ende;
        set => Setzen(ref ende, value);
    }

    /// <inheritdoc/>
    public override bool IstStandard => Bearbeitung.StandardLigadaten is not Ligadaten standard || Werte() == standard;

    /// <inheritdoc/>
    public override void Laden() => Anzeigen(Bearbeitung.Ligadaten);

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.Ligadaten = Werte();

    /// <inheritdoc/>
    public override void Standard()
    {
        if (Bearbeitung.StandardLigadaten is Ligadaten standard)
        {
            Anzeigen(standard);
            AenderungMelden();
        }
    }

    /// <inheritdoc/>
    public override string? Pruefen() => Werte().Pruefen();

    private static DateOnly Tag(DateTime? wert) => DateOnly.FromDateTime(wert ?? DateTime.MinValue);

    private Ligadaten Werte() => new(Name, Id, Art, Tag(Beginn), Tag(Rueckrundenbeginn), Tag(Ende));

    private void Anzeigen(Ligadaten d)
    {
        name = d.Name;
        id = d.Id;
        art = d.Art;
        beginn = d.Beginn.ToDateTime(TimeOnly.MinValue);
        rueckrundenbeginn = d.Rueckrundenbeginn.ToDateTime(TimeOnly.MinValue);
        ende = d.Ende.ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(string.Empty);
    }

    private void Setzen<T>(ref T feld, T wert)
    {
        if (!EqualityComparer<T>.Default.Equals(feld, wert))
        {
            feld = wert;
            OnPropertyChanged(string.Empty);
            AenderungMelden();
        }
    }
}
