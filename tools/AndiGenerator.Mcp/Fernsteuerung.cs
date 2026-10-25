// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Presentation;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;

namespace AndiGenerator.Mcp;

/// <summary>
/// Steuert das Hauptfenster-Modell ohne Fenster – dieselbe Ablauflogik wie die Oberfläche (Einrichtung, Kostenoptimierung,
/// Automodus mit HiGHS, Kompaktspieltage, Merken, Export). Jede Methode läuft auf dem <see cref="Bedienfaden"/>; die Werkzeuge
/// des MCP-Servers rufen nur sie auf.
/// </summary>
internal sealed partial class Fernsteuerung : IDisposable
{
    private static readonly TimeSpan HoechstensWarten = TimeSpan.FromMinutes(2);

    private readonly Bedienfaden faden = new();
    private readonly Fernoberflaeche oberflaeche;
    private readonly HauptfensterViewModel hf;

    /// <summary>Initialisiert die Steuerung mit den Ablageorten des Programms (gemerkte Pläne und Optionen wie in der Oberfläche).</summary>
    public Fernsteuerung()
        : this(null, null)
    {
    }

    /// <summary>Initialisiert die Steuerung mit eigenen Ablageorten (Tests).</summary>
    /// <param name="eigeneBasis">Basis der Staffelordner oder <c>null</c> für die des Programms.</param>
    /// <param name="uebernahmeAus">Basis des Originals oder <c>null</c>.</param>
    public Fernsteuerung(string? eigeneBasis, string? uebernahmeAus)
    {
        oberflaeche = new Fernoberflaeche(faden);

        // Konstruktor und Dispose laufen auf dem Faden des Servers, nie auf dem Bedienfaden: synchrones Warten ist hier sicher.
#pragma warning disable VSTHRD002
        hf = faden.Ausfuehren(() => eigeneBasis is null
            ? new HauptfensterViewModel(oberflaeche, KeinFenster)
            : new HauptfensterViewModel(oberflaeche, KeinFenster, eigeneBasis, uebernahmeAus)).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    /// <summary>Holt den Stand.</summary>
    /// <returns>Der Stand.</returns>
    public Task<Stand> StandAsync() => faden.Ausfuehren(StandBilden);

    /// <summary>Öffnet eine Staffel (click-TT-Export als XML).</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <returns>Ergebnis und offene Punkte der Einrichtung.</returns>
    public Task<Aktionsergebnis> OeffnenAsync(string pfad) => faden.Ausfuehren(async () =>
    {
        if (!File.Exists(pfad) || !string.Equals(Path.GetExtension(pfad), ".xml", StringComparison.OrdinalIgnoreCase))
        {
            return Ergebnis(false, $"„{pfad}“ ist keine vorhandene XML-Datei (Export der Staffel aus click-TT).");
        }

        if (hf.Laeuft || hf.KompaktRechnet)
        {
            hf.StoppenCommand.Execute(null);
        }

        oberflaeche.DateiZumOeffnen = Path.GetFullPath(pfad);
        await hf.OeffnenCommand.ExecuteAsync(null);
        if (!hf.IstGeoeffnet)
        {
            return Ergebnis(false, "Die Staffel ließ sich nicht öffnen.");
        }

        return Ergebnis(true, hf.ZeigtEinrichtung ? "Geöffnet. Die Einrichtung ist offen: „einrichtung“ zeigt die Punkte, „einrichtung_abschliessen“ übernimmt sie." : "Geöffnet.");
    });

    /// <summary>Öffnet ein beiliegendes Beispiel (als Kopie im Datenordner wie auf der Startseite).</summary>
    /// <param name="name">Name des Beispiels; leer = nur auflisten.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> BeispielOeffnenAsync(string name) => faden.Ausfuehren(async () =>
    {
        string liste = string.Join("; ", hf.Beispiele.Select(b => b.Name + (b.Beschreibung.Length > 0 ? " – " + b.Beschreibung : string.Empty)));
        if (hf.Beispiele.FirstOrDefault(b => Gleich(b.Name, name)) is not { } beispiel)
        {
            return Ergebnis(false, hf.HatBeispiele ? $"Beispiele: {liste}" : "Es liegen keine Beispiele bei.");
        }

        if (hf.Laeuft || hf.KompaktRechnet)
        {
            hf.StoppenCommand.Execute(null);
        }

        await hf.BeispielOeffnenCommand.ExecuteAsync(beispiel.Pfad);
        if (!hf.IstGeoeffnet)
        {
            return Ergebnis(false, "Das Beispiel ließ sich nicht öffnen.");
        }

        return Ergebnis(true, hf.ZeigtEinrichtung ? "Geöffnet. Die Einrichtung ist offen: „einrichtung“ zeigt die Punkte, „einrichtung_abschliessen“ übernimmt sie." : "Geöffnet.");
    });

    /// <summary>Schließt die Staffel (der Plan wird vorher gesichert wie in der Oberfläche).</summary>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> SchliessenAsync() => faden.Ausfuehren(async () =>
    {
        await hf.SchliessenCommand.ExecuteAsync(null);
        return Ergebnis(!hf.IstGeoeffnet, hf.IstGeoeffnet ? "Die Staffel ist noch offen." : "Geschlossen.");
    });

    /// <summary>Holt die Punkte der Einrichtung.</summary>
    /// <returns>Die Punkte (leer ohne Einrichtung).</returns>
    public Task<IReadOnlyList<Einrichtungspunkt>> EinrichtungAsync() => faden.Ausfuehren<IReadOnlyList<Einrichtungspunkt>>(() =>
        hf.Einrichtung is { } e
            ? e.Schritte.Select(s => new Einrichtungspunkt(s.Nummer, s.Titel, s.Status, s.Text, s.Optionen.Select(o => o.Titel).ToList(), s.Auswahl)).ToList()
            : []);

    /// <summary>Wählt bei einem Punkt der Einrichtung eine Option.</summary>
    /// <param name="nummer">Nummer des Punkts.</param>
    /// <param name="option">Titel der Option.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> EinrichtungWaehlenAsync(int nummer, string option) => faden.Ausfuehren(() =>
    {
        if (hf.Einrichtung?.Schritte.FirstOrDefault(s => s.Nummer == nummer) is not { } schritt)
        {
            return Ergebnis(false, $"Die Einrichtung hat keinen Punkt {nummer}.");
        }

        int index = schritt.Optionen.ToList().FindIndex(o => Gleich(o.Titel, option));
        if (index < 0)
        {
            return Ergebnis(false, $"Punkt {nummer} hat keine Option „{option}“. Möglich: {string.Join(", ", schritt.Optionen.Select(o => o.Titel))}.");
        }

        schritt.Auswahl = index;
        return Ergebnis(true, $"Punkt {nummer}: „{schritt.Optionen[index].Titel}“ gewählt.");
    });

    /// <summary>Schließt die Einrichtung ab (gewählte Optionen gelten, übrige Punkte bleiben unverändert).</summary>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> EinrichtungAbschliessenAsync() => faden.Ausfuehren(async () =>
    {
        if (hf.Einrichtung is not { } e)
        {
            return Ergebnis(true, "Es gibt keine offene Einrichtung.");
        }

        await e.AbschliessenCommand.ExecuteAsync(null);
        return Ergebnis(!hf.ZeigtEinrichtung, hf.ZeigtEinrichtung ? "Die Einrichtung ist noch offen." : "Einrichtung abgeschlossen.");
    });

    /// <summary>Wählt das Verfahren für normale Runden.</summary>
    /// <param name="automodus"><c>true</c> für den Automodus, sonst Kostenoptimierung.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> VerfahrenAsync(bool automodus) => faden.Ausfuehren(() =>
    {
        if (!BereitFuerAnsichten(out string grund))
        {
            return Ergebnis(false, grund);
        }

        if (hf.KompaktAktiv)
        {
            return Ergebnis(false, "Die Staffel wird mit Kompaktspieltagen geplant; dort gibt es keine Wahl des Verfahrens.");
        }

        (automodus ? hf.AutomodusCommand : hf.KostenoptimierungCommand).Execute(null);
        return Ergebnis(true, automodus ? "Automodus gewählt." : "Kostenoptimierung gewählt.");
    });

    /// <summary>Wählt den Ausgangsplan für den nächsten Start.</summary>
    /// <param name="name">Name aus „Startplaene“ im Stand.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> StartplanAsync(string name) => faden.Ausfuehren(() =>
    {
        if (hf.Startplaene.FirstOrDefault(p => Gleich(p.Name, name)) is not { } plan)
        {
            return Ergebnis(false, $"Keinen Ausgangsplan „{name}“. Möglich: {string.Join(", ", hf.Startplaene.Select(p => p.Name))}.");
        }

        hf.Startplan = plan;
        return Ergebnis(Equals(hf.Startplan, plan), Equals(hf.Startplan, plan) ? $"Ausgangsplan: {plan.Name}." : "Während einer Generierung lässt sich der Ausgangsplan nicht wechseln (erst „stoppen“).");
    });

    /// <summary>Startet die Generierung bzw. im Kompaktmodus die Berechnung der Pläne; läuft im Hintergrund weiter.</summary>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> StartenAsync() => faden.Ausfuehren(() =>
    {
        if (!BereitFuerAnsichten(out string grund))
        {
            return Ergebnis(false, grund);
        }

        if (hf.Laeuft || hf.KompaktRechnet)
        {
            return Ergebnis(false, "Es läuft schon eine Generierung bzw. Berechnung („anhalten“, „fortsetzen“, „stoppen“).");
        }

        // Nicht abwarten: Die Kompaktplanung kehrt erst nach der Berechnung zurück.
        _ = hf.GenerierungCommand.ExecuteAsync(null);
        return Ergebnis(true, hf.KompaktAktiv ? "Berechnung der Kompaktspieltage gestartet." : "Generierung gestartet.");
    });

    /// <summary>Hält an bzw. setzt fort.</summary>
    /// <param name="anhalten"><c>true</c> = anhalten, <c>false</c> = fortsetzen.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> PauseAsync(bool anhalten) => faden.Ausfuehren(() =>
    {
        bool rechnet = hf.Laeuft || hf.KompaktRechnet;
        bool pausiert = hf.KompaktRechnet ? hf.KompaktPausiert : hf.Pausiert;
        if (!rechnet)
        {
            return Ergebnis(false, "Es läuft keine Generierung.");
        }

        if (pausiert == anhalten)
        {
            return Ergebnis(true, anhalten ? "Ist schon angehalten." : "Läuft schon.");
        }

        _ = hf.GenerierungCommand.ExecuteAsync(null);
        return Ergebnis(true, anhalten ? "Angehalten." : "Fortgesetzt.");
    });

    /// <summary>Beendet die Generierung; der beste Plan bleibt.</summary>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> StoppenAsync() => faden.Ausfuehren(() =>
    {
        if (!hf.StoppenCommand.CanExecute(null))
        {
            return Ergebnis(false, "Es läuft keine Generierung.");
        }

        hf.StoppenCommand.Execute(null);
        return Ergebnis(true, "Gestoppt; der beste Plan bleibt erhalten.");
    });

    /// <summary>Wartet, bis sich etwas Wesentliches tut (Verbesserung, Phase, Ende der Berechnung), höchstens die angegebene Zeit.</summary>
    /// <param name="sekunden">Höchstens so lange (1 bis 120).</param>
    /// <returns>Der Stand danach.</returns>
    public async Task<Stand> WartenAsync(int sekunden)
    {
        TimeSpan dauer = TimeSpan.FromSeconds(Math.Clamp(sekunden, 1, (int)HoechstensWarten.TotalSeconds));
        string vorher = await faden.Ausfuehren(Merkmal).ConfigureAwait(false);
        DateTime ende = DateTime.UtcNow + dauer;
        while (DateTime.UtcNow < ende)
        {
            await Task.Delay(500).ConfigureAwait(false);
            if (await faden.Ausfuehren(Merkmal).ConfigureAwait(false) != vorher)
            {
                break;
            }
        }

        return await StandAsync().ConfigureAwait(false);
    }

    /// <summary>Holt die Qualitätsansicht (öffnet sie bei Bedarf).</summary>
    /// <returns>Die Qualität oder <c>null</c> ohne Plan.</returns>
    public Task<Qualitaet?> QualitaetAsync() => faden.Ausfuehren(() =>
    {
        if (QualitaetsansichtHolen() is not { } q)
        {
            return null;
        }

        return new Qualitaet(
            q.Quelle?.Name ?? string.Empty,
            q.Zusammenfassung,
            q.Zeilen.Where(z => z.Kopf is not null).Select(z => $"{z.Kopf!.Titel}: {z.Kopf.Plan} · {z.Kopf.Grenze} · {z.Kopf.Spielraum}").ToList(),
            q.Zeilen.Select(z => new Qualitaetseintrag(z.Stufe, z.Name, z.Verstoesse, z.Bestmoeglich, z.BestmoeglichErreicht, z.Festgelegt, z.NurCGrund)).ToList(),
            q.Hinweis);
    });

    /// <summary>Holt die Verstöße eines Kriteriums je Mannschaft.</summary>
    /// <param name="kriterium">Name des Kriteriums wie in der Qualität.</param>
    /// <returns>Die Verstöße oder <c>null</c>, wenn es das Kriterium nicht gibt.</returns>
    public Task<Verstossliste?> VerstoesseAsync(string kriterium) => faden.Ausfuehren(() =>
    {
        if (QualitaetsansichtHolen() is not { } q || q.Zeilen.FirstOrDefault(z => Gleich(z.Name, kriterium)) is not { } zeile)
        {
            return null;
        }

        if (!zeile.IstGewaehlt)
        {
            q.Waehlen(zeile);
        }

        var liste = new Verstossliste(
            q.DetailTitel,
            q.Details.Select(d => new Mannschaftsverstoesse(d.Name, d.Anzahl, d.Meldungen)).ToList(),
            q.ZeigtFolge ? [q.FolgeTitel, .. q.Folge.Select(f => f.Text)] : []);
        q.Waehlen(zeile);
        return liste;
    });

    /// <summary>Stuft ein Kriterium in A, B oder C ein (Automodus; gilt sofort, auch während der Generierung).</summary>
    /// <param name="kriterium">Name des Kriteriums.</param>
    /// <param name="stufe">„A“, „B“ oder „C“.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> EinstufenAsync(string kriterium, string stufe) => faden.Ausfuehren(async () =>
    {
        if (QualitaetsansichtHolen() is not { } q || q.Zeilen.FirstOrDefault(z => Gleich(z.Name, kriterium)) is not { } zeile)
        {
            return Ergebnis(false, $"Kein Kriterium „{kriterium}“ (Namen siehe „qualitaet“).");
        }

        IRelayCommand? befehl = stufe.Trim().ToUpperInvariant() switch
        {
            "A" => zeile.EinstufenACommand,
            "B" => zeile.EinstufenBCommand,
            "C" => zeile.EinstufenCCommand,
            _ => null,
        };
        if (befehl is null || !befehl.CanExecute(null))
        {
            string grund = zeile.NurC ? zeile.NurCGrund : "Einstufen geht nur im Automodus und nur bei Kriterien, die man einstufen kann.";
            return Ergebnis(false, $"„{zeile.Name}“ lässt sich nicht nach {stufe} einstufen: {grund}");
        }

        // Die Einteilung wird gespeichert und die Ansicht neu aufgebaut (asynchron wie in der Oberfläche).
        string ziel = stufe.Trim().ToUpperInvariant();
        befehl.Execute(null);
        for (int i = 0; i < 50 && q.Zeilen.FirstOrDefault(z => Gleich(z.Name, kriterium))?.Klasse != ziel; i++)
        {
            await Task.Delay(100);
        }

        return Ergebnis(true, $"„{zeile.Name}“ steht jetzt in Stufe {ziel}.");
    });

    /// <summary>Holt die B-Varianten aus HiGHS im Vergleich zum aktuellen Plan.</summary>
    /// <returns>Die Tabelle oder <c>null</c>, wenn es keine gibt.</returns>
    public Task<Variantentabelle?> VariantenAsync() => faden.Ausfuehren(async () =>
    {
        oberflaeche.Varianten = null;
        if (await VariantendialogAsync() is not { } d)
        {
            return null;
        }

        return new Variantentabelle(
            d.Hinweis,
            d.KannUebernehmen,
            d.Spalten.Select(s => $"{s.Titel} ({s.Summen})").ToList(),
            d.Zeilen.Select(z => $"{z.Stufe} {z.Name}: " + string.Join(" | ", z.Zellen.Select(c => c.Text))).ToList());
    });

    /// <summary>Übernimmt eine B-Variante: A und B sind damit festgelegt, der Automodus glättet nur noch C.</summary>
    /// <param name="titel">Titel der Spalte aus „b_varianten“ (ohne Summen).</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> VarianteFestlegenAsync(string titel) => faden.Ausfuehren(async () =>
    {
        string? fehler = null;
        oberflaeche.Varianten = d =>
        {
            if (d.Spalten.FirstOrDefault(s => !s.IstAktuell && Gleich(s.Titel, titel)) is not { } spalte)
            {
                fehler = $"Keine Variante „{titel}“. Möglich: {string.Join(", ", d.Spalten.Where(s => !s.IstAktuell).Select(s => s.Titel))}.";
                return false;
            }

            spalte.AuswaehlenCommand.Execute(null);
            if (!d.UebernehmenMoeglich)
            {
                fehler = "Festlegen geht nur während der Generierung im Automodus.";
                return false;
            }

            return true;
        };
        try
        {
            if (await VariantendialogAsync() is null)
            {
                return Ergebnis(false, "Es gibt keine B-Varianten, die den Plan verbessern.");
            }
        }
        finally
        {
            oberflaeche.Varianten = null;
        }

        return fehler is null ? Ergebnis(true, $"„{titel}“ übernommen: A und B sind festgelegt, der Automodus glättet nur noch C.") : Ergebnis(false, fehler);
    });

    /// <summary>Hebt die Festlegung von A und B auf.</summary>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> FestlegungAufhebenAsync() => faden.Ausfuehren(() =>
    {
        if (QualitaetsansichtHolen() is not { } q || !q.ZeigtFestlegung)
        {
            return Ergebnis(false, "Es ist nichts festgelegt.");
        }

        q.AufhebenCommand.Execute(null);
        return Ergebnis(true, "Festlegung aufgehoben.");
    });

    /// <summary>Merkt den besten Plan unter einem Namen.</summary>
    /// <param name="name">Name des Plans.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> MerkenAsync(string name) => faden.Ausfuehren(async () =>
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Ergebnis(false, "Bitte einen Namen angeben.");
        }

        if (!hf.PlanMerkenCommand.CanExecute(null))
        {
            return Ergebnis(false, "Es gibt noch keinen Plan zum Merken (erst „starten“).");
        }

        oberflaeche.Texteingabe = name.Trim();
        await hf.PlanMerkenCommand.ExecuteAsync(null);
        return Ergebnis(true, $"Als „{name.Trim()}“ gemerkt.");
    });

    /// <summary>Exportiert den aktuellen Plan als CSV für den Import in click-TT.</summary>
    /// <param name="pfad">Zieldatei (.csv).</param>
    /// <param name="trotzHinweisen">Auch exportieren, wenn der Plan harte Fehler bzw. Hinweise hat.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> ExportierenAsync(string pfad, bool trotzHinweisen) => faden.Ausfuehren(async () =>
    {
        if (!string.Equals(Path.GetExtension(pfad), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Ergebnis(false, "Die Zieldatei muss auf .csv enden.");
        }

        if (!hf.CsvExportierenCommand.CanExecute(null))
        {
            return Ergebnis(false, "Es gibt keinen Plan zum Exportieren.");
        }

        string ziel = Path.GetFullPath(pfad);
        DateTime vorher = File.Exists(ziel) ? File.GetLastWriteTimeUtc(ziel) : DateTime.MinValue;
        oberflaeche.DateiZumSpeichern = ziel;
        oberflaeche.Zustimmen = trotzHinweisen;
        try
        {
            await hf.CsvExportierenCommand.ExecuteAsync(null);
        }
        finally
        {
            oberflaeche.Zustimmen = false;
            oberflaeche.DateiZumSpeichern = null;
        }

        bool geschrieben = File.Exists(ziel) && File.GetLastWriteTimeUtc(ziel) > vorher;
        return Ergebnis(
            geschrieben,
            geschrieben ? $"Exportiert nach „{ziel}“." : "Nicht exportiert – siehe Meldungen (bei Hinweisen mit trotzHinweisen=true wiederholen).");
    });

    /// <summary>Holt die Ansicht Kompaktspieltage (öffnet sie bei Bedarf).</summary>
    /// <returns>Der Stand oder <c>null</c>, wenn die Staffel nicht mit Kompaktspieltagen geplant wird.</returns>
    public Task<Kompaktstand?> KompaktAsync() => faden.Ausfuehren(() =>
    {
        if (KompaktansichtHolen() is not { } k)
        {
            return null;
        }

        return new Kompaktstand(
            k.Kopfzeile,
            k.Rechnet,
            k.FortschrittText,
            k.Plaene.Select(p => (p.Gewaehlt ? "▶ " : string.Empty) + p.Titel + " – " + p.Zeile).ToList(),
            k.Erlaeuterungstitel.Length == 0 ? [] : [k.Erlaeuterungstitel, .. k.Erlaeuterung.Select(z => z.Replace("**", string.Empty, StringComparison.Ordinal))],
            k.Spieltage.Select(s => $"{s.Datum} {s.Form} {s.Ausrichter}: {string.Join("; ", s.Spiele)}").ToList(),
            [.. k.Hinweise]);
    });

    /// <summary>Zeigt einen Plan der Kompaktspieltage (er gilt dann für Terminplan, Druck und Export).</summary>
    /// <param name="titel">Titel des Plans aus „kompakt“.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> KompaktplanAsync(string titel) => faden.Ausfuehren(() =>
    {
        if (KompaktansichtHolen() is not { } k)
        {
            return Ergebnis(false, "Die Staffel wird nicht mit Kompaktspieltagen geplant.");
        }

        if (k.Plaene.FirstOrDefault(p => Gleich(p.Titel, titel)) is not { } plan)
        {
            return Ergebnis(false, $"Keinen Plan „{titel}“. Möglich: {string.Join(", ", k.Plaene.Select(p => p.Titel))}.");
        }

        k.PlanWaehlenCommand.Execute(plan);
        return Ergebnis(true, $"„{plan.Titel}“ gewählt.");
    });

    /// <summary>Holt die Führung „Schritt für Schritt“, die zur Lage passt.</summary>
    /// <returns>Die Schritte.</returns>
    public Task<Schrittfolge> SchritteAsync() => faden.Ausfuehren(() =>
    {
        Fuehrungsart art = hf.PassendeFuehrung;
        return new Schrittfolge(
            Fuehrungen.Titel(art),
            hf.FuehrungsHinweis,
            Fuehrungen.Schritte(art).Select((s, i) => $"{i + 1}. {s.Titel} – {s.Text} (Anleitung {s.Abschnitt})").ToList());
    });

    /// <inheritdoc/>
    public void Dispose()
    {
#pragma warning disable VSTHRD002 // siehe Konstruktor
        faden.Ausfuehren(() =>
        {
            if (hf.StoppenCommand.CanExecute(null))
            {
                hf.StoppenCommand.Execute(null);
            }

            hf.Dispose();
            return true;
        }).Wait(TimeSpan.FromSeconds(10));
#pragma warning restore VSTHRD002
        faden.Dispose();
    }

    private static IHostWindow KeinFenster() => throw new NotSupportedException("Ohne Oberfläche gibt es keine herausgelösten Fenster.");

    private static bool Gleich(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void Sammeln<T>(IDockable dockable, List<T> liste)
        where T : class
    {
        if (dockable is T gesucht)
        {
            liste.Add(gesucht);
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } kinder)
        {
            foreach (IDockable kind in kinder)
            {
                Sammeln(kind, liste);
            }
        }
    }

    private T? Ansicht<T>()
        where T : class
    {
        var liste = new List<T>();
        Sammeln(hf.Layout, liste);
        return liste.FirstOrDefault();
    }

    private bool BereitFuerAnsichten(out string grund)
    {
        grund = (hf.IstGeoeffnet, hf.ZeigtEinrichtung) switch
        {
            (false, _) => "Es ist keine Staffel geöffnet („staffel_oeffnen“).",
            (_, true) => "Erst die Einrichtung abschließen („einrichtung_abschliessen“).",
            _ => string.Empty,
        };
        return grund.Length == 0;
    }

    private QualitaetAnsichtViewModel? QualitaetsansichtHolen()
    {
        if (!BereitFuerAnsichten(out _) || hf.KompaktAktiv || !hf.QualitaetsansichtCommand.CanExecute(null))
        {
            return null;
        }

        if (Ansicht<QualitaetAnsichtViewModel>() is null)
        {
            hf.QualitaetsansichtCommand.Execute(null);
        }

        return Ansicht<QualitaetAnsichtViewModel>();
    }

    private KompaktspieltageAnsichtViewModel? KompaktansichtHolen()
    {
        if (!BereitFuerAnsichten(out _) || !hf.KompaktAktiv)
        {
            return null;
        }

        if (Ansicht<KompaktspieltageAnsichtViewModel>() is null)
        {
            hf.KompaktspieltageCommand.Execute(null);
        }

        return Ansicht<KompaktspieltageAnsichtViewModel>();
    }

    private async Task<AlternativendialogViewModel?> VariantendialogAsync()
    {
        AlternativendialogViewModel? vorher = oberflaeche.LetzteVarianten;
        await hf.BVariantenCommand.ExecuteAsync(null);
        return ReferenceEquals(oberflaeche.LetzteVarianten, vorher) ? null : oberflaeche.LetzteVarianten;
    }

    /// <summary>Was sich ändern muss, damit „warten“ zurückkehrt: Verbesserungen, Phase, Laufen, Berechnung der Kompaktspieltage.</summary>
    private string Merkmal() =>
        $"{hf.Anzeige.Verbesserungen}|{hf.Anzeige.Titel}|{hf.Laeuft}|{hf.Pausiert}|{hf.KompaktRechnet}|{hf.Anzeige.Highs?.Varianten}";

    private Aktionsergebnis Ergebnis(bool erfolg, string text) => new(erfolg, text, StandBilden());

    private Stand StandBilden()
    {
        string verfahren = (hf.KompaktAktiv, hf.Automodus) switch
        {
            (true, _) => "Kompaktspieltage",
            (_, true) => "Automodus",
            _ => "Kostenoptimierung",
        };
        Generierungsanzeige a = hf.Anzeige;
        string highs = a.Highs is { } h ? $"{h.Titel} · {h.Schritt} · {h.Danach}".Trim(' ', '·') : string.Empty;
        Generierungsstand? ergebnis = hf.HatPlan && !hf.KompaktAktiv && (hf.Laeuft || a.Verbesserungen.Length > 0)
            ? new Generierungsstand(
                a.Titel,
                a.Kosten,
                a.VerstossA,
                a.VerstossB,
                a.VerstossC,
                a.Phase,
                a.Verbesserungen,
                a.LetzteVerbesserung,
                a.Plaene,
                highs,
                a.Highs?.Varianten ?? 0)
            : null;
        List<string> meldungen = [.. oberflaeche.MeldungenAbholen()];
        if (faden.LetzterFehler is { } fehler)
        {
            meldungen.Add("Fehler: " + fehler.Message);
        }

        return new Stand(
            hf.IstGeoeffnet ? hf.Staffelname : string.Empty,
            hf.IstGeoeffnet ? hf.Staffelinfo : string.Empty,
            hf.ZeigtEinrichtung,
            verfahren,
            hf.Laeuft || hf.KompaktRechnet,
            hf.KompaktRechnet ? hf.KompaktPausiert : hf.Pausiert,
            hf.Startplan?.Name ?? string.Empty,
            hf.Startplaene.Select(p => p.Name).ToList(),
            ergebnis,
            hf.Statuszeile,
            hf.FuehrungsHinweis,
            meldungen);
    }
}
