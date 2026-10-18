// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Zeichnet einmal den ganzen Verlauf (<see cref="Verlaufsdiagramm"/>): Legende und Meilensteine oben, je Stufe eine Spur mit
/// Treppe, Punkten je Verbesserung, Grenze und Kopf links, Verbesserungen je Herkunft rechts, unten die Aufteilung des
/// Prozessors und die Zeitachse; mit der Maus ein Kästchen.
/// </summary>
internal sealed class Verlaufszeichnung
{
    private const double Legende = 22;
    private const double Fahnenzeile = 21;
    private const double Skala = 38;
    private const double Lueckenhoehe = 28;
    private const double Lastspur = 44;
    private const double Achse = 20;
    private const double Punkt = 4.5;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private static readonly string[] Herkuenfte = ["Inseln", "HiGHS", "Teilneuplanung"];

    private readonly DrawingContext context;
    private readonly IReadOnlyList<Verlaufspunkt> punkte;
    private readonly IReadOnlyList<Verlaufsverbesserung> verbesserungen;
    private readonly IReadOnlyList<Meilenstein> meilensteine;
    private readonly Rect flaeche;
    private readonly double ende;
    private readonly bool automodus;
    private readonly double kopfBreite;
    private readonly double zaehlerBreite;
    private readonly IBrush tinte = Pinsel.Farbe("Tinte");
    private readonly IBrush tinte2 = Pinsel.Farbe("Tinte2");
    private readonly IBrush grau = Pinsel.Farbe("Grau");
    private readonly IBrush linie = Pinsel.Farbe("Linie");
    private readonly IBrush band = Pinsel.Farbe("Schwebe");
    private readonly IBrush grund = Pinsel.Farbe("Flaeche");
    private readonly IBrush gut = Pinsel.Farbe("Gut");
    private readonly IBrush schlecht = Pinsel.Farbe("Schlecht");
    private readonly IBrush highs = Pinsel.Farbe("Highs");
    private readonly IBrush highsDunkel = Pinsel.Farbe("HighsDunkel");

    /// <summary>Initialisiert die Zeichnung.</summary>
    /// <param name="context">Wohin gezeichnet wird.</param>
    /// <param name="verlauf">Der Verlauf (mindestens zwei Punkte).</param>
    /// <param name="flaeche">Die ganze Fläche.</param>
    public Verlaufszeichnung(DrawingContext context, Suchverlauf verlauf, Rect flaeche)
    {
        this.context = context;
        punkte = verlauf.Punkte;
        verbesserungen = verlauf.Verbesserungen;
        meilensteine = verlauf.Meilensteine;
        this.flaeche = flaeche;
        ende = Math.Max(1, punkte[^1].Sekunden);
        automodus = punkte.Any(p => p.A is not null);
        kopfBreite = flaeche.Width < 760 ? 112 : 150;
        zaehlerBreite = flaeche.Width < 900 ? 0 : 150;
    }

    private double PlotLinks => flaeche.Left + kopfBreite + Skala;

    private double PlotRechts => flaeche.Right - zaehlerBreite - 8;

    /// <summary>Holt die Schrift mit diesem Namen aus den Ressourcen (Grundschrift oder Ziffernschrift).</summary>
    /// <param name="name">Name der Ressource.</param>
    /// <returns>Die Schrift.</returns>
    public static FontFamily Schriftart(string name) =>
        global::Avalonia.Application.Current is { } app && app.TryFindResource(name, out object? schrift) && schrift is FontFamily f ? f : FontFamily.Default;

    /// <summary>Zeichnet alles; <paramref name="zeiger"/> ist die Mausposition (x), oder <c>null</c>.</summary>
    /// <param name="zeiger">Die Mausposition.</param>
    public void Zeichnen(double? zeiger)
    {
        Spur[] spuren = automodus
            ? [new Spur("A · muss", p => p.A, p => p.SchrankeA, v => v.A, Ganz), new Spur("B · soll", p => p.B, p => p.SchrankeB, v => v.B, Ganz), new Spur("C · wird geglättet", p => p.C, _ => null, v => v.C, Ganz)]
            : [new Spur("Kosten", p => p.Kosten is >= 0 and < 1e10 ? p.Kosten : null, _ => null, v => v.Kosten is >= 0 and < 1e10 ? v.Kosten : null, Kostenanzeige.Kurz)];

        List<List<Meilenstein>> fahnenzeilen = Fahnenzeilen();
        double oben = flaeche.Top + Legende + (fahnenzeilen.Count * Fahnenzeile) + 8;
        double unten = flaeche.Bottom - Achse - Lastspur - Lueckenhoehe;
        double hoehe = (unten - oben - (Lueckenhoehe * (spuren.Length - 1))) / spuren.Length;
        Rect[] bereiche = spuren.Select((_, i) => new Rect(PlotLinks, oben + (i * (hoehe + Lueckenhoehe)), PlotRechts - PlotLinks, hoehe)).ToArray();
        var last = new Rect(PlotLinks, unten + Lueckenhoehe, PlotRechts - PlotLinks, Lastspur);

        LegendeZeichnen();
        Basisband(new Rect(bereiche[0].TopLeft, bereiche[^1].BottomRight));
        Meilensteine(fahnenzeilen, bereiche[^1].Bottom);
        for (int i = 0; i < spuren.Length; i++)
        {
            SpurZeichnen(spuren[i], bereiche[i]);
        }

        Anhebungen(bereiche[^1]);
        Zeitachse(bereiche[^1].Bottom);
        LastZeichnen(last);
        if (zeiger is double x && x >= PlotLinks && x <= PlotRechts)
        {
            Kaestchen(x, new Rect(bereiche[0].TopLeft, last.BottomRight), spuren);
        }
    }

    private static string Ganz(double wert) => wert.ToString("N0", Deutsch);

    private static string Zeit(double sekunden)
    {
        var dauer = TimeSpan.FromSeconds(sekunden);
        return dauer.TotalHours >= 1 ? dauer.ToString(@"h\:mm\:ss", Deutsch) : dauer.ToString(@"m\:ss", Deutsch);
    }

    private static string Achsentext(double sekunden) => sekunden switch
    {
        0 => "0",
        < 60 => string.Create(Deutsch, $"{sekunden:0} s"),
        < 3600 => string.Create(Deutsch, $"{sekunden / 60:0.#} min"),
        _ => string.Create(Deutsch, $"{sekunden / 3600:0.#} h"),
    };

    /// <summary>Ein runder Abstand der Teilstriche: etwa drei über die <paramref name="spanne"/>.</summary>
    private static double Schritt(double spanne)
    {
        double roh = Math.Max(spanne / 3, 1e-9);
        double zehner = Math.Pow(10, Math.Floor(Math.Log10(roh)));
        double faktor = roh / zehner;

        // 2,5 nur bei Zehnern (25, 250 …), sonst stünden bei ganzen Zahlen Kommazahlen an der Skala.
        double[] stufen = zehner >= 10 ? [1, 2, 2.5, 5, 10] : [1, 2, 5, 10];
        return zehner * stufen.First(st => faktor <= st);
    }

    private static FormattedText Formatiert(string text, IBrush farbe, double groesse, bool fett = false, bool ziffern = false) =>
        new(text, Deutsch, FlowDirection.LeftToRight, new Typeface(Schriftart(ziffern ? "Ziffernschrift" : "Grundschrift"), FontStyle.Normal, fett ? FontWeight.SemiBold : FontWeight.Normal), groesse, farbe);

    private IBrush Herkunftsfarbe(string herkunft) => herkunft switch
    {
        "HiGHS" => highs,
        "Teilneuplanung" => Pinsel.Farbe("Teilneuplanung"),
        _ => Pinsel.Farbe("Akzent"),
    };

    private double X(double sekunden) => PlotLinks + ((PlotRechts - PlotLinks) * sekunden / ende);

    private double Text(string text, Point ort, IBrush farbe, double groesse, bool fett = false, bool ziffern = false, bool rechtsbuendig = false)
    {
        FormattedText f = Formatiert(text, farbe, groesse, fett, ziffern);
        context.DrawText(f, rechtsbuendig ? new Point(ort.X - f.Width, ort.Y) : ort);
        return f.Width;
    }

    /// <summary>Oben rechts: die Farben der Herkunft.</summary>
    private void LegendeZeichnen()
    {
        Text("Punkt = Verbesserung, Farbe = Herkunft:", new Point(flaeche.Left, flaeche.Top + 3), grau, 10);
        double x = flaeche.Left + Formatiert("Punkt = Verbesserung, Farbe = Herkunft:", grau, 10).Width + 12;
        foreach (string herkunft in Herkuenfte)
        {
            context.DrawEllipse(Herkunftsfarbe(herkunft), null, new Point(x + Punkt, flaeche.Top + 10), Punkt, Punkt);
            x += (2 * Punkt) + 5;
            x += Text(herkunft, new Point(x, flaeche.Top + 3), tinte2, 11) + 16;
        }
    }

    /// <summary>Die Meilensteine auf Zeilen verteilt (höchstens drei), damit sich ihre Fähnchen nicht überdecken.</summary>
    private List<List<Meilenstein>> Fahnenzeilen()
    {
        var zeilen = new List<List<Meilenstein>>();
        var rechtsFrei = new List<double>();
        foreach (Meilenstein m in meilensteine)
        {
            double x = X(m.Sekunden);
            double breite = Formatiert(m.Text, tinte2, 10).Width + 12;
            int zeile = rechtsFrei.FindIndex(r => r < x);
            if (zeile < 0)
            {
                if (zeilen.Count >= 3)
                {
                    continue;
                }

                zeilen.Add([]);
                rechtsFrei.Add(0);
                zeile = zeilen.Count - 1;
            }

            zeilen[zeile].Add(m);
            rechtsFrei[zeile] = x + breite + 4;
        }

        return zeilen;
    }

    /// <summary>Fähnchen über den Spuren, gestrichelt durch alle Spuren.</summary>
    private void Meilensteine(List<List<Meilenstein>> zeilen, double spurenUnten)
    {
        var strich = new Pen(Pinsel.Farbe("Rand"), 1, new DashStyle([3, 3], 0));
        for (int z = 0; z < zeilen.Count; z++)
        {
            double y = flaeche.Top + Legende + (z * Fahnenzeile);
            foreach (Meilenstein m in zeilen[z])
            {
                double x = X(m.Sekunden);
                context.DrawLine(strich, new Point(x, y + Fahnenzeile - 4), new Point(x, spurenUnten));
                FormattedText text = Formatiert(m.Text, tinte2, 10);
                var kasten = new Rect(x, y, text.Width + 10, Fahnenzeile - 4);
                context.DrawRectangle(band, null, kasten, 3, 3);
                context.DrawText(text, new Point(x + 5, y + ((kasten.Height - text.Height) / 2)));
            }
        }
    }

    /// <summary>Hinter allen Spuren grau die Basisoptimierung.</summary>
    private void Basisband(Rect alle)
    {
        Verlaufspunkt? letzte = punkte.LastOrDefault(p => p.Phase == "Basisoptimierung");
        if (letzte is null || punkte.All(p => p.Phase == "Basisoptimierung"))
        {
            return;
        }

        double bis = X(punkte.First(p => p.Sekunden > letzte.Sekunden).Sekunden);
        context.FillRectangle(band, new Rect(alle.Left, alle.Top, Math.Max(0, bis - alle.Left), alle.Height));
        Text("Basis", new Point(alle.Left + 4, alle.Top + 2), grau, 9);
    }

    private void SpurZeichnen(Spur spur, Rect bereich)
    {
        (double Sekunden, double Wert)[] werte = punkte.Select(p => (p.Sekunden, Wert: spur.Wert(p))).Where(w => w.Wert is not null).Select(w => (w.Sekunden, w.Wert!.Value)).ToArray();
        if (werte.Length == 0)
        {
            return;
        }

        int? grenze = punkte.Select(spur.Grenze).LastOrDefault(g => g is not null);
        double? grenzeAb = grenze is null ? null : punkte.FirstOrDefault(p => spur.Grenze(p) is not null)?.Sekunden;
        double letzter = werte[^1].Wert;
        bool optimal = grenze is int g0 && letzter <= g0;

        // Skala: bei A und B ab 0, bei C und Kosten ab knapp unter dem kleinsten Wert.
        double max = werte.Max(w => w.Wert);
        double min = spur.Titel[0] is 'A' or 'B' ? 0 : werte.Min(w => w.Wert);
        double schritt = Schritt(Math.Max(max - min, 1));
        min = Math.Floor(min / schritt) * schritt;
        max = Math.Max(Math.Ceiling(max * 1.06 / schritt) * schritt, min + schritt);
        double Y(double wert) => bereich.Bottom - (bereich.Height * (wert - min) / (max - min));

        // Raster und Skala.
        var raster = new Pen(linie, 1);
        for (double t = min; t <= max + 1e-9; t += schritt)
        {
            double y = Y(t);
            context.DrawLine(raster, new Point(bereich.Left, y), new Point(bereich.Right, y));
            FormattedText zahl = Formatiert(spur.Format(t), grau, 9.5, ziffern: true);
            context.DrawText(zahl, new Point(bereich.Left - 6 - zahl.Width, y - (zahl.Height / 2)));
        }

        // Treppe des besten Plans.
        var treppe = new StreamGeometry();
        using (StreamGeometryContext tg = treppe.Open())
        {
            tg.BeginFigure(new Point(X(werte[0].Sekunden), Y(werte[0].Wert)), false);
            for (int i = 1; i < werte.Length; i++)
            {
                tg.LineTo(new Point(X(werte[i].Sekunden), Y(werte[i - 1].Wert)));
                tg.LineTo(new Point(X(werte[i].Sekunden), Y(werte[i].Wert)));
            }

            tg.LineTo(new Point(bereich.Right, Y(letzter)));
            tg.EndFigure(false);
        }

        // Grenze: grün erreicht, sonst orange mit schraffierter Fläche („noch zu holen“).
        if (grenze is int gr && grenzeAb is double ab)
        {
            double gy = Y(gr);
            if (!optimal)
            {
                Schraffur(werte, ab, gr, X, Y, bereich.Right);
            }

            context.DrawLine(new Pen(optimal ? gut : highs, 1.4, new DashStyle([5, 3], 0)), new Point(X(ab), gy), new Point(bereich.Right, gy));
            string text = optimal
                ? string.Create(Deutsch, $"Grenze ≥ {gr}  ·  Optimum erreicht")
                : string.Create(Deutsch, $"Grenze ≥ {gr}  ·  höchstens {letzter - gr:N0} weniger möglich");
            FormattedText f = Formatiert(text, optimal ? gut : highsDunkel, 10, fett: optimal);
            double ty = optimal ? gy - f.Height - 3 : gy + 3;
            context.DrawRectangle(Pinsel.Mit(grund, 0.85), null, new Rect(bereich.Right - f.Width - 8, ty - 1, f.Width + 6, f.Height + 2), 3, 3);
            context.DrawText(f, new Point(bereich.Right - f.Width - 5, ty));
        }
        else if (automodus && spur.Titel[0] == 'C')
        {
            Text("Grenze für C erst, wenn A und B bestmöglich sind", new Point(bereich.Right - 4, bereich.Top + 2), grau, 10, rechtsbuendig: true);
        }

        context.DrawGeometry(null, new Pen(tinte, 2, lineJoin: PenLineJoin.Miter), treppe);
        PunkteZeichnen(spur, Y, max - min);
        KopfZeichnen(spur, bereich, werte[0].Wert, letzter, grenze, optimal);
        ZaehlerZeichnen(spur, bereich);
    }

    /// <summary>
    /// Die Fläche zwischen Treppe und Grenze, schraffiert: so viel ist höchstens noch zu holen. Je Stufe der Treppe ein Rechteck
    /// (die Schraffur läuft über alle durch).
    /// </summary>
    private void Schraffur((double Sekunden, double Wert)[] werte, double ab, int grenze, Func<double, double> x, Func<double, double> y, double rechts)
    {
        var stufen = new List<(double Von, double Bis, double Wert)>();
        double wert = werte.LastOrDefault(w => w.Sekunden <= ab).Wert;
        if (werte.All(w => w.Sekunden > ab))
        {
            wert = werte[0].Wert;
        }

        double von = x(ab);
        foreach ((double sekunden, double neu) in werte.Where(w => w.Sekunden > ab))
        {
            stufen.Add((von, x(sekunden), wert));
            (von, wert) = (x(sekunden), neu);
        }

        stufen.Add((von, rechts, wert));
        IBrush fuellung = Pinsel.Mit(Pinsel.Farbe("HighsHell"), 0.6);
        var stift = new Pen(Pinsel.Mit(highs, 0.45), 1);
        double gy = y(grenze);
        foreach ((double links, double bis, double w) in stufen.Where(st => st.Wert > grenze && st.Bis > st.Von))
        {
            var r = new Rect(links, y(w), bis - links, gy - y(w));
            context.FillRectangle(fuellung, r);

            // Diagonalen (45°) auf einem festen Raster, damit sie über die Rechtecke hinweg durchlaufen; selbst auf das Rechteck
            // gekürzt (ohne Clipping). Eine Diagonale c: x = c + (gy − y).
            for (double c = Math.Floor((r.Left - r.Height) / 7) * 7; c <= r.Right; c += 7)
            {
                double oben = Math.Max(r.Top, gy - (r.Right - c));
                double unten = Math.Min(r.Bottom, gy - (r.Left - c));
                if (unten > oben)
                {
                    context.DrawLine(stift, new Point(c + gy - unten, unten), new Point(c + gy - oben, oben));
                }
            }
        }
    }

    /// <summary>Je Verbesserung, die den Wert der Spur ändert, ein Punkt in der Farbe ihrer Herkunft; große Sprünge beschriftet.</summary>
    private void PunkteZeichnen(Spur spur, Func<double, double> y, double spanne)
    {
        double? vorher = punkte.Select(spur.Wert).FirstOrDefault(w => w is not null);
        var ring = new Pen(grund, 1.6);
        foreach (Verlaufsverbesserung v in verbesserungen)
        {
            if (spur.Verbesserung(v) is not double wert || (vorher is double alt && Math.Abs(alt - wert) < 1e-9))
            {
                continue;
            }

            var mitte = new Point(X(v.Sekunden), y(wert));
            context.DrawEllipse(Herkunftsfarbe(v.Herkunft), ring, mitte, Punkt, Punkt);
            if (v.Herkunft != "Inseln" && vorher is double von && von - wert >= Math.Max(3, spanne * 0.15))
            {
                FormattedText f = Formatiert($"{spur.Format(von)} → {spur.Format(wert)}", highsDunkel, 10.5, fett: true, ziffern: true);
                var ort = new Point(mitte.X + 8, mitte.Y - f.Height - 4);
                context.DrawRectangle(Pinsel.Mit(grund, 0.85), null, new Rect(ort.X - 2, ort.Y, f.Width + 4, f.Height), 3, 3);
                context.DrawText(f, ort);
            }

            vorher = wert;
        }
    }

    /// <summary>Links neben der Spur: Titel, aktueller Wert, Weg seit dem Start und ob das Optimum erreicht ist.</summary>
    private void KopfZeichnen(Spur spur, Rect bereich, double start, double jetzt, int? grenze, bool optimal)
    {
        double x = flaeche.Left;
        double y = bereich.Top;
        Text(spur.Titel, new Point(x, y), tinte, 12, fett: true);
        y += 18;
        FormattedText wert = Formatiert(spur.Format(jetzt), optimal ? gut : tinte, Math.Min(26, Math.Max(16, bereich.Height / 4)), ziffern: true);
        context.DrawText(wert, new Point(x, y));
        y += wert.Height + 2;
        if (bereich.Height < 64)
        {
            return;
        }

        string weg = start > 0 && jetzt < start
            ? string.Create(Deutsch, $"Start {spur.Format(start)}  ·  −{Math.Round((1 - (jetzt / start)) * 100):0} %")
            : "Start " + spur.Format(start);
        Text(weg, new Point(x, y), tinte2, 10);
        y += 16;
        if (optimal)
        {
            Text("✓ bestmöglich", new Point(x, y), gut, 11, fett: true);
        }
        else if (grenze is int g)
        {
            Text(string.Create(Deutsch, $"Ziel ≥ {g}"), new Point(x, y), highsDunkel, 11, fett: true);
        }
    }

    /// <summary>Rechts neben der Spur: wie viele Verbesserungen dieser Stufe von wem kamen.</summary>
    private void ZaehlerZeichnen(Spur spur, Rect bereich)
    {
        if (zaehlerBreite <= 0)
        {
            return;
        }

        var zaehl = new Dictionary<string, int>();
        double? vorher = punkte.Select(spur.Wert).FirstOrDefault(w => w is not null);
        foreach (Verlaufsverbesserung v in verbesserungen)
        {
            if (spur.Verbesserung(v) is double wert && (vorher is not double alt || Math.Abs(alt - wert) > 1e-9))
            {
                zaehl[v.Herkunft] = zaehl.GetValueOrDefault(v.Herkunft) + 1;
                vorher = wert;
            }
        }

        double x = flaeche.Right - zaehlerBreite + 8;
        double rechts = flaeche.Right - 4;
        double y = bereich.Top;
        Text("Verbesserungen", new Point(x, y), grau, 10);
        foreach ((string herkunft, int anzahl) in zaehl.OrderByDescending(e => e.Value))
        {
            y += 17;
            if (y > bereich.Bottom - 12)
            {
                break;
            }

            context.FillRectangle(Herkunftsfarbe(herkunft), new Rect(x, y + 3, 7, 9), 1.5f);
            Text(herkunft, new Point(x + 12, y), tinte2, 10.5);
            Text(anzahl.ToString(Deutsch), new Point(rechts, y), tinte, 10.5, ziffern: true, rechtsbuendig: true);
        }
    }

    /// <summary>Kleine Striche unter der letzten Spur: hier hat der Automodus Gewichte angehoben.</summary>
    private void Anhebungen(Rect bereich)
    {
        var stift = new Pen(Pinsel.Farbe("Akzent"), 1.5);
        for (int i = 1; i < punkte.Count; i++)
        {
            if (punkte[i].Anhebungen > punkte[i - 1].Anhebungen)
            {
                double x = X(punkte[i].Sekunden);
                context.DrawLine(stift, new Point(x, bereich.Bottom + 2), new Point(x, bereich.Bottom + 6));
            }
        }
    }

    /// <summary>Zeitachse unter der letzten Spur mit runden Schritten (Sekunden, Minuten, Stunden).</summary>
    private void Zeitachse(double y)
    {
        double[] schritte = [10, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200];
        double schritt = schritte.FirstOrDefault(s => ende / s <= 6, 7200);
        for (double t = 0; t <= ende + 1e-9; t += schritt)
        {
            FormattedText f = Formatiert(Achsentext(t), grau, 10);
            double x = Math.Min(X(t) - (t == 0 ? 0 : f.Width / 2), PlotRechts - f.Width);
            context.DrawText(f, new Point(x, y + 7));
        }
    }

    /// <summary>Unten: wer den Prozessor nutzt (Suche, HiGHS, Rest), gestapelt; ohne Messung die Pläne je Sekunde.</summary>
    private void LastZeichnen(Rect bereich)
    {
        double y0 = bereich.Top + 4;
        Text("Wer rechnet", new Point(flaeche.Left, y0), tinte, 12, fett: true);
        bool gemessen = punkte.Any(p => p.Suche + p.Highs + p.Rest > 0);
        Text(gemessen ? "Anteil des Prozessors" : "Pläne je Sekunde", new Point(flaeche.Left, y0 + 17), tinte2, 10);
        if (!gemessen)
        {
            double max = Math.Max(1, punkte.Max(p => p.PlaeneProSekunde));
            Flaeche(bereich, p => 0, p => p.PlaeneProSekunde / max, Pinsel.Mit(Pinsel.Farbe("Akzent"), 0.8));
            return;
        }

        Flaeche(bereich, _ => 0, p => p.Suche, Pinsel.Farbe("Akzent"));
        Flaeche(bereich, p => p.Suche, p => p.Suche + p.Highs, highs);
        Flaeche(bereich, p => p.Suche + p.Highs, p => p.Suche + p.Highs + p.Rest, Pinsel.Farbe("Fremdlast"));
        if (zaehlerBreite > 0)
        {
            double x = flaeche.Right - zaehlerBreite + 8;
            double y = bereich.Top;
            foreach ((string name, IBrush farbe) in new[] { ("Suche (Inseln)", Pinsel.Farbe("Akzent")), ("HiGHS", highs), ("Rest", Pinsel.Farbe("Fremdlast")) })
            {
                context.FillRectangle(farbe, new Rect(x, y + 3, 7, 9), 1.5f);
                Text(name, new Point(x + 12, y), tinte2, 10);
                y += 14;
            }
        }
    }

    private void Flaeche(Rect bereich, Func<Verlaufspunkt, double> von, Func<Verlaufspunkt, double> bis, IBrush farbe)
    {
        double Y(double anteil) => bereich.Bottom - (bereich.Height * Math.Clamp(anteil, 0, 1));
        var geo = new StreamGeometry();
        using (StreamGeometryContext g = geo.Open())
        {
            g.BeginFigure(new Point(X(punkte[0].Sekunden), Y(bis(punkte[0]))), true);
            foreach (Verlaufspunkt p in punkte.Skip(1))
            {
                g.LineTo(new Point(X(p.Sekunden), Y(bis(p))));
            }

            for (int i = punkte.Count - 1; i >= 0; i--)
            {
                g.LineTo(new Point(X(punkte[i].Sekunden), Y(von(punkte[i]))));
            }

            g.EndFigure(true);
        }

        context.DrawGeometry(farbe, null, geo);
    }

    /// <summary>Senkrechte Linie an der Maus und ein Kästchen: die Verbesserung in der Nähe oder die Werte des Zeitpunkts.</summary>
    private void Kaestchen(double x, Rect alle, Spur[] spuren)
    {
        double sekunden = (x - PlotLinks) / (PlotRechts - PlotLinks) * ende;
        Verlaufsverbesserung? v = verbesserungen.Where(e => Math.Abs(X(e.Sekunden) - x) <= 8).MinBy(e => Math.Abs(e.Sekunden - sekunden));
        double px = v is null ? x : X(v.Sekunden);
        context.DrawLine(new Pen(Pinsel.Mit(tinte2, 0.6), 1), new Point(px, alle.Top), new Point(px, alle.Bottom));

        var zeilen = new List<FormattedText>();
        if (v is not null)
        {
            zeilen.Add(Formatiert(string.Create(Deutsch, $"{Zeit(v.Sekunden)} · Verbesserung {v.Nummer}"), tinte, 11, fett: true));
            zeilen.Add(Formatiert(v.Herkunft, Herkunftsfarbe(v.Herkunft), 10.5));
            zeilen.AddRange(v.Aenderungen.Select(a => Formatiert(a.Text, a.Besser ? gut : schlecht, 10.5, ziffern: true)));
            string werte = automodus
                ? string.Create(Deutsch, $"A {v.A} · B {v.B} · C {v.C}")
                : Kostenanzeige.Kurz(v.Kosten);
            zeilen.Add(Formatiert(werte, tinte2, 10.5, ziffern: true));
        }
        else
        {
            Verlaufspunkt p = punkte.MinBy(q => Math.Abs(q.Sekunden - sekunden))!;
            zeilen.Add(Formatiert(Zeit(p.Sekunden) + (p.Phase.Length > 0 ? " · " + p.Phase : string.Empty), tinte, 11, fett: true));
            zeilen.AddRange(spuren.Select(s => s.Wert(p) is double w ? Formatiert(s.Titel + ": " + s.Format(w), tinte2, 10.5) : null).OfType<FormattedText>());
            zeilen.Add(Formatiert(p.PlaeneProSekunde.ToString("N0", Deutsch) + " Pläne/s", tinte2, 10.5));
        }

        double breite = zeilen.Max(t => t.Width) + 20;
        double hoehe = zeilen.Sum(t => t.Height + 2) + 14;
        double links = px + 12 + breite > PlotRechts ? px - 12 - breite : px + 12;
        var kasten = new Rect(links, alle.Top + 6, breite, hoehe);
        context.DrawRectangle(grund, new Pen(linie, 1), kasten, 8, 8);
        double y = kasten.Top + 7;
        foreach (FormattedText zeile in zeilen)
        {
            context.DrawText(zeile, new Point(kasten.Left + 10, y));
            y += zeile.Height + 2;
        }
    }

    /// <summary>Eine Spur: Titel, Wert je Punkt, Grenze je Punkt, Wert nach einer Verbesserung und Format.</summary>
    private sealed record Spur(
        string Titel,
        Func<Verlaufspunkt, double?> Wert,
        Func<Verlaufspunkt, int?> Grenze,
        Func<Verlaufsverbesserung, double?> Verbesserung,
        Func<double, string> Format);
}
