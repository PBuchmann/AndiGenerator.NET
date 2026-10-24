// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Presentation;
using AndiGenerator.Rendering;
using AndiGenerator.UI;
using SkiaSharp;

namespace AndiGenerator.Mcp;

/// <summary>Terminplan und Diagramme lesen.</summary>
internal sealed partial class Fernsteuerung
{
    /// <summary>Die Diagramme mit ihren Namen für die KI.</summary>
    internal static readonly (string Name, Diagrammteil Teil)[] Diagrammnamen =
    [
        ("spieltage", Diagrammteil.Spieltage), ("wechsel", Diagrammteil.WechselHeimAuswaerts), ("ausrichter", Diagrammteil.Ausrichter),
        ("spielverteilung", Diagrammteil.Spielverteilung), ("abstand", Diagrammteil.AbstandHeimAuswaerts), ("woche", Diagrammteil.SpieleProWoche),
        ("setzliste", Diagrammteil.Setzliste),
    ];

    private const int Rand = 20;

    /// <summary>Holt den Terminplan wie in der Ansicht „Terminplan“.</summary>
    /// <param name="mannschaft">Nur Spiele dieser Mannschaft oder leer.</param>
    /// <param name="plan">Plan (Name wie unter „Startplaene“) oder leer = laufende Generierung.</param>
    /// <returns>Der Terminplan oder <c>null</c>, wenn es keinen Plan gibt.</returns>
    public Task<(Terminplan? Plan, string? Fehler)> TerminplanAsync(string mannschaft, string plan) => faden.Ausfuehren(() =>
    {
        if (PlanansichtHolen<TerminplanAnsichtViewModel>(hf.TerminplanansichtCommand, plan, out string? fehler) is not { } t)
        {
            return ((Terminplan?)null, fehler);
        }

        t.Darstellung = 0;
        string? name = null;
        if (mannschaft.Trim().Length > 0)
        {
            name = t.Wochen.SelectMany(w => w.Spiele).Concat(t.OhneTermin).SelectMany(sp => new[] { sp.Heim, sp.Gast }).FirstOrDefault(n => Gleich(n, mannschaft));
            if (name is null)
            {
                return (null, $"Keine Mannschaft „{mannschaft}“.");
            }
        }

        bool Passt(Terminspiel s) => name is null || s.Heim == name || s.Gast == name;
        List<Planwoche> wochen = t.Wochen
            .Select(w => new Planwoche(w.Titel, w.Zeitraum, w.Rundenwechsel, w.Spiele.Where(Passt).Select(Spieltext).ToList()))
            .Where(w => w.Spiele.Count > 0)
            .ToList();
        return (new Terminplan(t.Quelle?.Name ?? string.Empty, t.Zusammenfassung, wochen, t.OhneTermin.Where(Passt).Select(Spieltext).ToList(), t.Quellen.Select(q => q.Name).ToList()), null);
    });

    /// <summary>Zeichnet ein Diagramm wie in der Ansicht „Diagramme“ als PNG.</summary>
    /// <param name="teil">Name des Diagramms (siehe <see cref="Diagrammnamen"/>).</param>
    /// <param name="plan">Plan (Name wie unter „Startplaene“) oder leer = laufende Generierung.</param>
    /// <returns>Das Diagramm oder ein Fehler.</returns>
    public async Task<(Diagrammbild? Bild, string? Fehler)> DiagrammAsync(string teil, string plan)
    {
        (Diagrammdaten? daten, Diagrammteil gewaehlt, string quelle, IReadOnlyList<string> vorhanden, string? fehler) = await faden.Ausfuehren(() =>
        {
            if (PlanansichtHolen<DiagrammAnsichtViewModel>(hf.DiagrammansichtCommand, plan, out string? f) is not { } d)
            {
                return ((Diagrammdaten?)null, default(Diagrammteil), string.Empty, (IReadOnlyList<string>)[], f);
            }

            IReadOnlyList<string> namen = d.Auswahl.Select(w => Diagrammnamen.First(n => n.Teil == w.Teil).Name).ToList();
            string gesucht = teil.Trim().Length == 0 ? namen.FirstOrDefault() ?? string.Empty : teil.Trim();
            if (d.Daten is null || !namen.Any(n => Gleich(n, gesucht)))
            {
                return (null, default, string.Empty, namen, $"Kein Diagramm „{teil}“ für diesen Plan. Möglich: {string.Join(", ", namen)}.");
            }

            return (d.Daten, Diagrammnamen.First(n => Gleich(n.Name, gesucht)).Teil, d.Quelle?.Name ?? string.Empty, namen, (string?)null);
        }).ConfigureAwait(false);
        if (daten is null)
        {
            return (null, fehler);
        }

        byte[] png = await Task.Run(() => Zeichnen(daten, gewaehlt)).ConfigureAwait(false);
        return (new Diagrammbild(Diagrammwahl.TitelVon(gewaehlt), Diagrammwahl.BeschreibungVon(gewaehlt), quelle, Kennzahlen(daten), vorhanden, png), null);
    }

    /// <summary>Zeichnet ein Diagramm auf weißem Grund.</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <param name="teil">Das Diagramm.</param>
    /// <returns>Das PNG.</returns>
    internal static byte[] Zeichnen(Diagrammdaten daten, Diagrammteil teil)
    {
        Zeichengroesse groesse;
        using (var messung = new SkiaZeichenflaeche(null))
        {
            groesse = new Diagrammzeichner(daten, messung, mitTiteln: false).Zeichnen(teil, 0);
        }

        int breite = (int)Math.Ceiling(Math.Max(groesse.Breite, 200)) + (2 * Rand);
        int hoehe = (int)Math.Ceiling(Math.Max(groesse.Hoehe, 50)) + (2 * Rand);
        using SKSurface flaeche = SKSurface.Create(new SKImageInfo(breite, hoehe));
        SKCanvas leinwand = flaeche.Canvas;
        leinwand.Clear(SKColors.White);
        leinwand.Translate(Rand, Rand);
        using (var zeichnung = new SkiaZeichenflaeche(leinwand))
        {
            new Diagrammzeichner(daten, zeichnung, mitTiteln: false).Zeichnen(teil, 0);
        }

        using SKImage bild = flaeche.Snapshot();
        using SKData png = bild.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static List<string> Kennzahlen(Diagrammdaten daten)
    {
        if (daten.Verstoesse is not { } v)
        {
            return [];
        }

        static string Zahl(string text, int? wert) => wert is int w && w > 0 ? string.Create(CultureInfo.InvariantCulture, $"{text} {w}") : string.Empty;
        return v.JeMannschaft
            .OrderBy(e => e.Key, StringComparer.CurrentCulture)
            .Select(e => (e.Key, Teile: new[]
            {
                Zahl("Wechselfehler", e.Value.Wechsel), Zahl("enge Termine", e.Value.EngeTermine), Zahl("Wochen mit zwei Spielen", e.Value.ZweiSpieleProWoche),
                Zahl("lange Pausen", e.Value.Pausen), Zahl("Abstand Hin-/Rückspiel", e.Value.AbstandHinRueck), Zahl("Setzliste", e.Value.Setzliste),
            }.Where(t => t.Length > 0).ToList()))
            .Select(e => e.Key + ": " + (e.Teile.Count == 0 ? "keine Verstöße" : string.Join(", ", e.Teile)))
            .ToList();
    }

    private static string Spieltext(Terminspiel s)
    {
        string termin = s.Zeit.Length == 0 ? "ohne Termin" : $"{s.Tag} {s.Datum} {s.Zeit}";
        string hinweise = s.Chips.Count == 0 ? string.Empty : " [" + string.Join(", ", s.Chips.Select(c => c.Text)) + "]";
        return string.Create(CultureInfo.InvariantCulture, $"{s.Nummer}. {termin}: {s.Heim} – {s.Gast} ({s.Runde}){hinweise}");
    }

    private T? PlanansichtHolen<T>(CommunityToolkit.Mvvm.Input.IRelayCommand oeffnen, string plan, out string? fehler)
        where T : AnsichtViewModel
    {
        if (!BereitFuerAnsichten(out string grund))
        {
            fehler = grund;
            return null;
        }

        if (!oeffnen.CanExecute(null))
        {
            fehler = "Es gibt noch keinen Plan („starten“ oder einen gemerkten Plan wählen).";
            return null;
        }

        if (Ansicht<T>() is null)
        {
            oeffnen.Execute(null);
        }

        T? ansicht = Ansicht<T>();
        fehler = null;
        if (ansicht is not null)
        {
            PlanQuelle? quelle = plan.Trim().Length > 0
                ? ansicht.Quellen.FirstOrDefault(q => Gleich(q.Name, plan))
                : ansicht.Quellen.FirstOrDefault(q => q == ansicht.Quelle) ?? ansicht.Quellen.FirstOrDefault();
            if (quelle is null)
            {
                fehler = $"Keinen Plan „{plan}“. Möglich: {string.Join(", ", ansicht.Quellen.Select(q => q.Name))}.";
                return null;
            }

            ansicht.Quelle = quelle;
        }

        return ansicht;
    }
}
