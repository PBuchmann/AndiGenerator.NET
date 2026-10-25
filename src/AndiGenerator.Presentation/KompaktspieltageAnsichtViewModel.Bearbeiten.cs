// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Bearbeitungsmodus des Rasters „Meldungen und Plan“ (Peter, 13.10.2026): Ein Rahmen steht auf einem Feld (Klick oder
/// Pfeiltasten); A, H, B und Entf setzen auswärts, nur Heim, beides bzw. kann nicht. Für Heimtermine lassen sich Beginn,
/// Spiellokal und zwei Tische einstellen, weitere Tage als Zeilen anlegen. Gespeichert wird sofort wie die Spielplandaten.
/// </summary>
public sealed partial class KompaktspieltageAnsichtViewModel
{
    private bool bearbeiten;
    private (string Mannschaft, DateOnly Tag)? cursor;
    private string cursorText = string.Empty;
    private bool cursorHatTermin;
    private string cursorZeit = string.Empty;
    private Kompaktlokal? cursorLokal;
    private IReadOnlyList<Kompaktlokal> cursorLokale = [];
    private bool cursorZweiTische;
    private DateTime? neuerTag;
    private string bearbeitungsmeldung = string.Empty;

    /// <summary>Die zuletzt begonnene Änderung: Eingaben werden nacheinander gespeichert (schnelles Tippen geht nicht verloren).</summary>
    private Task kette = Task.CompletedTask;

    /// <summary>Holt den Befehl „Feld wählen“ (Klick im Bearbeitungsmodus).</summary>
    public IRelayCommand<Kompaktzelle> ZelleWaehlenCommand { get; }

    /// <summary>Holt den Befehl „Meldung setzen“: <c>A</c>, <c>H</c>, <c>B</c> oder <c>-</c> (kann nicht).</summary>
    public IAsyncRelayCommand<string> FeldSetzenCommand { get; }

    /// <summary>Holt den Befehl „Heimtermin übernehmen“ (Beginn, Spiellokal, Tische).</summary>
    public IAsyncRelayCommand TerminSpeichernCommand { get; }

    /// <summary>Holt den Befehl „Tag anlegen“.</summary>
    public IAsyncRelayCommand TagAnlegenCommand { get; }

    /// <summary>Holt den Befehl „Beginn löschen“: zurück zum Standard (3er 10:00, 4er 11:00), sofort gespeichert.</summary>
    public IAsyncRelayCommand ZeitLoeschenCommand { get; }

    /// <summary>Holt den Befehl „Spiellokal für alle Heimtermine der Mannschaft“.</summary>
    public IAsyncRelayCommand LokalFuerAlleCommand { get; }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob das Raster bearbeitet wird (sonst nur Anzeige).</summary>
    public bool Bearbeiten
    {
        get => bearbeiten;
        set
        {
            if (!SetProperty(ref bearbeiten, value))
            {
                return;
            }

            hauptfenster.KompaktMeldungenBearbeiten = value;
            hauptfenster.KompaktLesekopieVerwerfen();

            if (value && cursor is null && auswertung is { Mannschaften.Count: > 0, Tage.Count: > 0 } a)
            {
                cursor = (a.Mannschaften[0].Name, a.Tage[0]);
            }

            Bearbeitungsmeldung = string.Empty;
            AuswahlZeigen();
            CursorLaden();
        }
    }

    /// <summary>Holt, welches Feld der Rahmen zeigt und was dort gemeldet ist.</summary>
    public string CursorText
    {
        get => cursorText;
        private set => SetProperty(ref cursorText, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob das Feld einen Heimtermin hat (dann Beginn, Spiellokal, Tische).</summary>
    public bool CursorHatTermin
    {
        get => cursorHatTermin;
        private set => SetProperty(ref cursorHatTermin, value);
    }

    /// <summary>Holt oder setzt den Beginn des Heimtermins, z. B. „10:30“; leer = Standard (3er 10:00, 4er 11:00).</summary>
    public string CursorZeit
    {
        get => cursorZeit;
        set => SetProperty(ref cursorZeit, value);
    }

    /// <summary>Holt oder setzt das Spiellokal des Heimtermins.</summary>
    public Kompaktlokal? CursorLokal
    {
        get => cursorLokal;
        set => SetProperty(ref cursorLokal, value);
    }

    /// <summary>Holt die Spiellokale der Mannschaft zur Wahl.</summary>
    public IReadOnlyList<Kompaktlokal> CursorLokale
    {
        get => cursorLokale;
        private set => SetProperty(ref cursorLokale, value);
    }

    /// <summary>Holt oder setzt, ob zwei Spiele parallel gehen (4er-Spieltag möglich).</summary>
    public bool CursorZweiTische
    {
        get => cursorZweiTische;
        set => SetProperty(ref cursorZweiTische, value);
    }

    /// <summary>Holt oder setzt den Tag, der als Zeile angelegt werden soll.</summary>
    public DateTime? NeuerTag
    {
        get => neuerTag;
        set => SetProperty(ref neuerTag, value);
    }

    /// <summary>Holt eine Meldung zur letzten Eingabe (z. B. ungültige Uhrzeit) oder leer.</summary>
    public string Bearbeitungsmeldung
    {
        get => bearbeitungsmeldung;
        private set => SetProperty(ref bearbeitungsmeldung, value);
    }

    /// <summary>Bewegt den Rahmen (Pfeiltasten), am Rand bleibt er stehen.</summary>
    /// <param name="spalten">Spalten nach rechts (negativ: links).</param>
    /// <param name="zeilen">Zeilen nach unten (negativ: oben).</param>
    public void CursorBewegen(int spalten, int zeilen)
    {
        if (!Bearbeiten || auswertung is not { Mannschaften.Count: > 0, Tage.Count: > 0 } a)
        {
            return;
        }

        int s = cursor is { } c ? Math.Max(0, a.Mannschaften.ToList().FindIndex(m => m.Name == c.Mannschaft)) : 0;
        int z = cursor is { } d ? Math.Max(0, a.Tage.ToList().IndexOf(d.Tag)) : 0;
        s = Math.Clamp(s + spalten, 0, a.Mannschaften.Count - 1);
        z = Math.Clamp(z + zeilen, 0, a.Tage.Count - 1);
        CursorSetzen(a.Mannschaften[s].Name, a.Tage[z]);
    }

    /// <summary>Liest eine Uhrzeit wie „10“, „10:00“ oder „9.30“.</summary>
    private static TimeOnly? ZeitLesen(string text)
    {
        string t = text.Trim();
        if (int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int stunde) && stunde is >= 0 and < 24)
        {
            return new TimeOnly(stunde, 0);
        }

        return TimeOnly.TryParseExact(t, ["H:mm", "HH:mm", "H.mm", "HH.mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly zeit) ? zeit : null;
    }

    private void CursorSetzen(string? mannschaft, DateOnly? tag)
    {
        if (!Bearbeiten || mannschaft is null || tag is not DateOnly t)
        {
            return;
        }

        cursor = (mannschaft, t);
        Bearbeitungsmeldung = string.Empty;

        // Nur den Rahmen umsetzen, nicht das ganze Raster neu aufbauen.
        foreach (Kompaktzelle zelle in Zeilen.SelectMany(z => z.Zellen))
        {
            zelle.IstCursor = zelle.Mannschaft == mannschaft && zelle.Tag == t;
        }

        CursorLaden();
    }

    /// <summary>Liest den Heimtermin und die Spiellokale des Felds unter dem Rahmen.</summary>
    private void CursorLaden()
    {
        if (!Bearbeiten || cursor is not (string mannschaft, DateOnly tag))
        {
            CursorText = string.Empty;
            CursorHatTermin = false;
            return;
        }

        CursorText = Zeilen.SelectMany(z => z.Zellen).FirstOrDefault(z => z.IstCursor)?.Hinweis ?? string.Empty;
        (Kompakttermin? termin, IReadOnlyList<Kompaktlokal> lokale) = hauptfenster.KompaktDatenLesen(
            d => (d.KompaktterminLesen(mannschaft, tag), d.KompaktLokale(mannschaft)),
            ((Kompakttermin?)null, (IReadOnlyList<Kompaktlokal>)Array.Empty<Kompaktlokal>()));
        CursorHatTermin = termin is not null;
        CursorLokale = lokale;
        CursorZeit = termin?.Zeit?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
        string lokal = termin?.Spiellokal ?? string.Empty;
        CursorLokal = lokale.FirstOrDefault(l => l.Nummer == lokal) ?? (lokale.Count > 0 ? lokale[0] : null);
        CursorZweiTische = termin?.ZweiTische ?? false;
    }

    private async Task FeldSetzenAsync(string? art)
    {
        if (!Bearbeiten || cursor is not (string mannschaft, DateOnly tag))
        {
            return;
        }

        Kompaktfeld? feld = art?.ToUpperInvariant() switch
        {
            "A" => Kompaktfeld.Auswaerts,
            "H" => Kompaktfeld.Heim,
            "B" => Kompaktfeld.Beides,
            "-" => Kompaktfeld.Leer,
            _ => null,
        };
        if (feld is Kompaktfeld f)
        {
            await NacheinanderAsync(() => hauptfenster.KompaktMeldungAendernAsync(d => d.KompaktfeldSetzen(mannschaft, tag, f)));
        }
    }

    private async Task TerminSpeichernAsync()
    {
        if (!Bearbeiten || cursor is not (string mannschaft, DateOnly tag) || !CursorHatTermin)
        {
            return;
        }

        TimeOnly? zeit = null;
        if (CursorZeit.Trim().Length > 0)
        {
            zeit = ZeitLesen(CursorZeit);
            if (zeit is null)
            {
                Bearbeitungsmeldung = "Bitte den Beginn als Uhrzeit eingeben, z. B. 10:30 – oder leer lassen für den Standard (3er 10:00, 4er 11:00).";
                return;
            }
        }

        Bearbeitungsmeldung = string.Empty;
        var termin = new Kompakttermin(zeit, CursorLokal?.Nummer ?? string.Empty, CursorZweiTische);
        await NacheinanderAsync(() => hauptfenster.KompaktMeldungAendernAsync(d => d.KompaktterminSetzen(mannschaft, tag, termin)));
    }

    /// <summary>Führt eine Änderung aus, wenn die vorige gespeichert ist, und liest danach das Feld unter dem Rahmen neu.</summary>
    private async Task NacheinanderAsync(Func<Task> aenderung)
    {
        // Alles läuft im Oberflächen-Thread: die Kette wird ohne Sperre weitergereicht.
        Task vorher = kette;
        var fertig = new TaskCompletionSource();
        kette = fertig.Task;
        try
        {
            await vorher;
            await aenderung();
        }
        finally
        {
            fertig.SetResult();
        }

        CursorLaden();
    }

    private async Task ZeitLoeschenAsync()
    {
        CursorZeit = string.Empty;
        await TerminSpeichernAsync();
    }

    private async Task LokalFuerAlleAsync()
    {
        if (!Bearbeiten || cursor is not (string mannschaft, _) || CursorLokal is not Kompaktlokal lokal)
        {
            return;
        }

        await NacheinanderAsync(() => hauptfenster.KompaktMeldungAendernAsync(d => d.KompaktLokalFuerAlle(mannschaft, lokal.Nummer)));
    }

    private async Task TagAnlegenAsync()
    {
        if (!Bearbeiten || NeuerTag is not DateTime gewaehlt || auswertung is not { Mannschaften.Count: > 0 } a)
        {
            return;
        }

        DateOnly tag = DateOnly.FromDateTime(gewaehlt);
        if (!a.Tage.Contains(tag))
        {
            await NacheinanderAsync(() => hauptfenster.KompaktMeldungAendernAsync(d => d.KompakttagAnlegen(tag)));
        }

        if (auswertung is { } neu && neu.Tage.Contains(tag))
        {
            Bearbeitungsmeldung = string.Empty;
            NeuerTag = null;
            CursorSetzen(neu.Mannschaften[0].Name, tag);
        }
        else
        {
            Bearbeitungsmeldung = string.Create(Deutsch, $"Der {tag:dd.MM.yyyy} liegt nicht in der geplanten Halbrunde oder ist spielfrei.");
        }
    }
}
