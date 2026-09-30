// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Eine Staffel in einem eigenen temporären Ordner mit geöffnetem Dialog „Spielplandaten bearbeiten“ (alle Seiten wie
/// im Hauptfenster) und einer <see cref="TestOberflaeche"/>. Die Referenzdaten selbst bleiben unberührt.
/// </summary>
internal sealed class Arbeitsplatz : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-vm-" + Guid.NewGuid().ToString("N"));
    private readonly string datei;

    /// <summary>Initialisiert den Arbeitsplatz.</summary>
    /// <param name="quelle">Pfad relativ zu den Referenzfällen, z. B. <c>datendialoge/D01_Allgemein.xml</c>.</param>
    public Arbeitsplatz(string quelle)
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        datei = Path.Combine(ordner, "daten", Path.GetFileName(quelle));
        File.Copy(Path.Combine(Testdaten.Referenz, quelle), datei);
        string aenderungen = Path.ChangeExtension(Path.Combine(Testdaten.Referenz, quelle), ".modifications");
        if (!quelle.StartsWith("datendialoge", StringComparison.Ordinal) && File.Exists(aenderungen))
        {
            File.Copy(aenderungen, Plansitzung.ModifikationenPfad(datei));
        }

        Sitzung = Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis"));
        Bearbeitung = Sitzung.DatenBearbeiten();
        Dialog = new DatenDialogViewModel(Bearbeitung, DatenDialogViewModel.AlleSeiten(Bearbeitung, Oberflaeche));
    }

    /// <summary>Holt die geöffnete Staffel.</summary>
    public Plansitzung Sitzung { get; }

    /// <summary>Holt die Arbeitskopie.</summary>
    public Datenbearbeitung Bearbeitung { get; }

    /// <summary>Holt die Attrappe der Oberfläche.</summary>
    public TestOberflaeche Oberflaeche { get; } = new();

    /// <summary>Holt den Dialog.</summary>
    public DatenDialogViewModel Dialog { get; }

    /// <summary>Holt die geschriebene Änderungsdatei.</summary>
    public DatenKnoten Aenderungen => PlanDatenDatei.Laden(Plansitzung.ModifikationenPfad(datei));

    /// <summary>Wählt auf einer Seite mit Mannschaftsliste eine Mannschaft und liefert deren Detailansicht.</summary>
    /// <typeparam name="T">Typ der Detailansicht.</typeparam>
    /// <param name="seite">Die Seite.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Detailansicht.</returns>
    public static T Detail<T>(MehrMannschaftenSeiteViewModel seite, string mannschaft)
        where T : DatenSeiteViewModel
    {
        seite.Mannschaft = mannschaft;
        Assert.Equal(mannschaft, seite.Mannschaft);
        return Assert.IsType<T>(seite.Detail);
    }

    /// <summary>Wechselt wie per Klick in der Seitenliste auf eine Seite.</summary>
    /// <typeparam name="T">Typ der Seite.</typeparam>
    /// <returns>Die Seite.</returns>
    public T Seite<T>()
        where T : DatenSeiteViewModel
    {
        T seite = Dialog.Seiten.OfType<T>().Single();
        Dialog.Seite = seite;
        Assert.Same(seite, Dialog.Seite);
        return seite;
    }

    /// <summary>OK im Dialog und Übernehmen wie im Hauptfenster (schreibt die Änderungsdatei).</summary>
    public void Uebernehmen()
    {
        Assert.True(Dialog.Bestaetigen(), Dialog.Meldung);
        Sitzung.DatenUebernehmen(Bearbeitung);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(ordner, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist nicht Teil des Tests.
        }
    }
}
