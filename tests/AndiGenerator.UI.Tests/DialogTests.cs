// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Presentation;
using AndiGenerator.Presentation.Tests;
using AndiGenerator.UI.Seiten;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        Anzeigen(new Druckauswahldialog(DruckauswahlViewModel.Kompakt(PlanQuelle.Laufend, "Bestes Gesamtpaket")));
        Anzeigen(new Druckauswahldialog(DruckauswahlViewModel.Kompakt(null, string.Empty)));
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

    /// <summary>Der Dialog der B-Varianten: jede Spalte wählen, Abbrechen und Übernehmen schließen ihn.</summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public Task Alternativendialog_wird_angezeigt_und_geschlossen() => oberflaeche.Ausfuehren(() =>
    {
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        IReadOnlyList<Spiel> spiele = a.Sitzung.Staffel.BestehenderSpielplan;
        Planbewertung bewertung = Referenzbewertung.Bewerten(a.Sitzung.Staffel, a.Sitzung.Optionen);
        var aktuell = new Planstand(spiele, bewertung);
        var sperren = new Planalternative(Kostenkriterium.Sperrtermine, "Sperrtermine", spiele, Bewiesen: true);
        var summe = new Planalternative(null, "Summe in B", spiele, Bewiesen: false);
        string mannschaft = a.Sitzung.Staffel.Mannschaften[0].Name;
        IReadOnlyList<Meldungsgruppe> Meldungen(Planstand plan, Kostenkriterium k) => ReferenceEquals(plan, aktuell)
            ? [new Meldungsgruppe(mannschaft, "1 Verstoß", ["Sperrtermin: Sa 03.10."])]
            : [new Meldungsgruppe(mannschaft, "1 Verstoß", ["Sperrtermin: Sa 10.10."])];

        foreach (bool uebernehmen in new[] { false, true })
        {
            var dialog = new AlternativendialogViewModel(
                aktuell,
                [(sperren, bewertung), (summe, bewertung)],
                sperren,
                Stufeneinteilung.Standard,
                Meldungen,
                true,
                "Ein Hinweis zu den Varianten.");
            var fenster = new Alternativendialog(dialog);
            fenster.Show();
            Oberflaeche.Zeichnen(fenster);
            foreach (Alternativspalte spalte in dialog.Spalten)
            {
                spalte.AuswaehlenCommand.Execute(null);
                Oberflaeche.Zeichnen(fenster);
            }

            dialog.Spalten[^1].AuswaehlenCommand.Execute(null);
            Oberflaeche.Zeichnen(fenster);
            Button knopf = Knopf(fenster, uebernehmen ? "Uebernehmen" : "Abbrechen");
            Assert.True(knopf.IsEffectivelyEnabled);
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(fenster.IsVisible);
        }

        oberflaeche.KeineFehler(ausgabe);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Doppelklick_auf_einen_Wunschtermin_oeffnet_den_Dialog() => oberflaeche.Ausfuehren(() =>
    {
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        bool geschlossen = false;
        a.Oberflaeche.Wunschtermin = d =>
        {
            // Alle Arten einmal zeigen, dann mit OK schließen.
            var wunsch = new Wunschtermindialog(d);
            wunsch.Show();
            Oberflaeche.Zeichnen(wunsch);
            d.ArtSperrtermin = true;
            Oberflaeche.Zeichnen(wunsch);
            d.ArtKeinHeimspiel = true;
            Oberflaeche.Zeichnen(wunsch);
            d.ArtHeimspiel = true;
            d.Uhrzeit = new TimeSpan(19, 30, 0);
            Oberflaeche.Zeichnen(wunsch);
            Knopf(wunsch, "Ok").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            geschlossen = !wunsch.IsVisible;
            wunsch.Close();
            return false;
        };
        var fenster = new Datendialog(a.Dialog);
        fenster.Show();
        try
        {
            WunschtermineSeiteViewModel seite = a.Dialog.Seiten.OfType<WunschtermineSeiteViewModel>().Single();
            Seitenliste(fenster).SelectedItem = seite;
            seite.Mannschaft = Namen.RheinduerkheimIV;
            Oberflaeche.Zeichnen(fenster);

            // Ein Klick auf ein Kärtchen öffnet den Dialog „Wunschtermin“ (früher schluckte das Textfeld den Doppelklick).
            WunschterminDetail detail = Assert.Single(fenster.GetVisualDescendants().OfType<WunschterminDetail>());
            Point punkt = Assert.IsType<Point>(detail.GetVisualDescendants()
                .OfType<Border>()
                .Where(t => t.Classes.Contains("kaertchen") && t.DataContext is WunschterminZelle)
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

        Assert.True(geschlossen);
        oberflaeche.KeineFehler(ausgabe);
        return Task.CompletedTask;
    });

    private static Button Knopf(Window fenster, string name) =>
        fenster.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);

    private static ListBox Seitenliste(Window fenster) =>
        fenster.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("seitenliste"));

    private static void Anzeigen(Window dialog)
    {
        dialog.Show();
        Oberflaeche.Zeichnen(dialog);
        dialog.Close();
    }
}
