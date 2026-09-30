// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite des Dialogs „Spielplandaten bearbeiten“ (Original <c>TDialogPanel</c>): liest beim Betreten aus der Arbeitskopie
/// (<c>DataToForm</c>), schreibt beim Verlassen und bei OK zurück (<c>FormToData</c>), prüft die Eingaben
/// (<c>CheckData</c>) und kann auf den click-TT-Stand zurückgesetzt werden (<c>toDefault</c>/<c>isDefault</c>).
/// </summary>
public abstract class DatenSeiteViewModel : ObservableObject
{
    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="titel">Seitentitel in der Navigation.</param>
    protected DatenSeiteViewModel(Datenbearbeitung bearbeitung, string titel)
    {
        ArgumentNullException.ThrowIfNull(bearbeitung);
        Bearbeitung = bearbeitung;
        Titel = titel;
    }

    /// <summary>Tritt ein, wenn der Benutzer etwas geändert hat (Original <c>FireOnChange</c>).</summary>
    public event EventHandler? Geaendert;

    /// <summary>Holt den Seitentitel.</summary>
    public string Titel { get; }

    /// <summary>Holt einen Wert, der angibt, ob die Seite dem click-TT-Stand entspricht; ohne Standard immer <c>true</c>.</summary>
    public abstract bool IstStandard { get; }

    /// <summary>Holt die Arbeitskopie.</summary>
    protected Datenbearbeitung Bearbeitung { get; }

    /// <summary>Liest die Werte aus der Arbeitskopie.</summary>
    public abstract void Laden();

    /// <summary>Schreibt die Werte in die Arbeitskopie.</summary>
    public abstract void Speichern();

    /// <summary>Setzt die Werte auf den click-TT-Stand zurück.</summary>
    public abstract void Standard();

    /// <summary>Prüft die Eingaben.</summary>
    /// <returns>Die erste Fehlermeldung oder <c>null</c>.</returns>
    public virtual string? Pruefen() => null;

    /// <summary>Meldet eine Änderung des Benutzers.</summary>
    protected void AenderungMelden() => Geaendert?.Invoke(this, EventArgs.Empty);
}
