// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Presentation;
using AndiGenerator.Presentation.Tests;
using AndiGenerator.UI.Seiten;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit.Abstractions;

namespace AndiGenerator.UI.Tests;

/// <summary>Rauchtests der Dialoge: öffnen, alle Seiten zeigen, zeichnen, schließen – ohne Fehler von Avalonia.</summary>
[Collection(OberflaechenSammlung.Name)]
public sealed class DialogTests
{
    private readonly Oberflaeche oberflaeche;
    private readonly ITestOutputHelper ausgabe;

    public DialogTests(Oberflaeche oberflaeche, ITestOutputHelper ausgabe)
    {
        this.oberflaeche = oberflaeche;
        this.ausgabe = ausgabe;
        oberflaeche.Protokoll.Leeren();
    }

    [Fact]
    public Task Einfache_Dialoge_werden_ohne_Fehler_angezeigt() => oberflaeche.Ausfuehren(() =>
    {
        var o = new TestOberflaeche();
        Anzeigen(new Ueberdialog(new UeberViewModel(o, "1.0")));
        Anzeigen(new Druckauswahldialog(new DruckauswahlViewModel([PlanQuelle.ClickTt])));
        Anzeigen(new Dialogfenster("Frage", "Wirklich?", null, mitAbbrechen: true));
        Anzeigen(new Dialogfenster("Name", "Name des Plans:", "Plan 1", mitAbbrechen: true));
        Anzeigen(new Dialogfenster("Entscheidung", "Was tun?", ["Erster Punkt", "Zweiter Punkt"], "Ja", "Nein"));
        Anzeigen(new Gewichtungsdialog("Gewichtung", "Wunschtermine", Gewichtung.Normal, ["Ein Hinweis"]));
        oberflaeche.KeineFehler(ausgabe);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Spielplandaten_zeigen_alle_Seiten_ohne_Fehler() => oberflaeche.Ausfuehren(() =>
    {
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        var fenster = new Datendialog(a.Dialog);
        fenster.Show();
        try
        {
            ListBox liste = Seitenliste(fenster);
            foreach (DatenSeiteViewModel seite in a.Dialog.Seiten)
            {
                liste.SelectedItem = seite;
                Oberflaeche.Zeichnen(fenster);
                Assert.Same(seite, a.Dialog.Seite);
                if (seite is MehrMannschaftenSeiteViewModel mehr && mehr.Namen.Count > 0)
                {
                    mehr.Mannschaft = mehr.Namen[^1];
                    Oberflaeche.Zeichnen(fenster);
                    Assert.NotNull(mehr.Detail);
                }
            }
        }
        finally
        {
            fenster.Close();
        }

        oberflaeche.KeineFehler(ausgabe);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Doppelklick_auf_einen_Wunschtermin_oeffnet_den_Dialog() => oberflaeche.Ausfuehren(() =>
    {
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        var fenster = new Datendialog(a.Dialog);
        fenster.Show();
        try
        {
            WunschtermineSeiteViewModel seite = a.Dialog.Seiten.OfType<WunschtermineSeiteViewModel>().Single();
            Seitenliste(fenster).SelectedItem = seite;
            seite.Mannschaft = Namen.RheinduerkheimIV;
            Oberflaeche.Zeichnen(fenster);

            // Regression: Das Textfeld schluckte den Doppelklick (Wort markieren), der Dialog ging nicht auf.
            WunschterminDetail detail = Assert.Single(fenster.GetVisualDescendants().OfType<WunschterminDetail>());
            Point punkt = Assert.IsType<Point>(detail.GetVisualDescendants()
                .OfType<TextBox>()
                .Where(t => t.DataContext is WunschterminZelle)
                .Select(t => t.TranslatePoint(new Point(t.Bounds.Width / 2, t.Bounds.Height / 2), fenster))
                .FirstOrDefault(p => p is Point q && new Rect(fenster.ClientSize).Contains(q)));
            fenster.MouseDown(punkt, MouseButton.Left);
            fenster.MouseUp(punkt, MouseButton.Left);
            fenster.MouseDown(punkt, MouseButton.Left);
            fenster.MouseUp(punkt, MouseButton.Left);
            Oberflaeche.Zeichnen(fenster);
        }
        finally
        {
            fenster.Close();
        }

        Assert.Contains("Wunschtermin", a.Oberflaeche.Aufrufe);
        oberflaeche.KeineFehler(ausgabe);
        return Task.CompletedTask;
    });

    private static ListBox Seitenliste(Window fenster) =>
        fenster.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("seitenliste"));

    private static void Anzeigen(Window dialog)
    {
        dialog.Show();
        Oberflaeche.Zeichnen(dialog);
        dialog.Close();
    }
}
