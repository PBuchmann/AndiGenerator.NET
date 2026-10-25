// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Halle aller Heimspiele einer Mannschaft (Klick auf die Mannschaft über dem Raster): Die gewählte Halle wird ihre Standardhalle
/// und gilt für alle Heimtermine – auch für die, die später eingetragen werden.
/// </summary>
public sealed partial class KompaktspieltageAnsichtViewModel
{
    private string halleMannschaft = string.Empty;
    private IReadOnlyList<Kompaktlokal> halleLokale = [];
    private Kompaktlokal? halleLokal;
    private string halleMeldung = string.Empty;

    /// <summary>Holt den Befehl „Halle für alle Heimspiele setzen“.</summary>
    public IAsyncRelayCommand HalleFuerAlleCommand { get; }

    /// <summary>Holt die gewählte Mannschaft, deren Halle eingestellt werden kann; leer, wenn keine gewählt ist.</summary>
    public string HalleMannschaft
    {
        get => halleMannschaft;
        private set
        {
            if (SetProperty(ref halleMannschaft, value))
            {
                OnPropertyChanged(nameof(ZeigtHalle));
                OnPropertyChanged(nameof(HalleTitel));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob eine Mannschaft gewählt ist (dann lässt sich ihre Halle einstellen).</summary>
    public bool ZeigtHalle => halleMannschaft.Length > 0;

    /// <summary>Holt die Überschrift, z. B. „Halle aller Heimspiele von TTC Wirges“.</summary>
    public string HalleTitel => "Halle aller Heimspiele von " + halleMannschaft;

    /// <summary>Holt die Hallen der Mannschaft zur Wahl.</summary>
    public IReadOnlyList<Kompaktlokal> HalleLokale
    {
        get => halleLokale;
        private set => SetProperty(ref halleLokale, value);
    }

    /// <summary>Holt oder setzt die gewählte Halle (zuerst die Standardhalle der Mannschaft).</summary>
    public Kompaktlokal? HalleLokal
    {
        get => halleLokal;
        set
        {
            if (SetProperty(ref halleLokal, value))
            {
                HalleMeldung = string.Empty;
            }
        }
    }

    /// <summary>Holt die Rückmeldung nach dem Setzen, z. B. „… gilt jetzt für alle Heimtermine“.</summary>
    public string HalleMeldung
    {
        get => halleMeldung;
        private set => SetProperty(ref halleMeldung, value);
    }

    /// <summary>
    /// Zeigt die Hallen der gewählten Mannschaft (oder nichts, wenn keine gewählt ist). Bei einer anderen Mannschaft ist zuerst
    /// ihre Standardhalle gewählt; bei derselben bleibt die Wahl, die Liste wird nur aufgefrischt.
    /// </summary>
    private void HalleLaden(string? team)
    {
        if (team is null)
        {
            HalleMannschaft = string.Empty;
            HalleLokale = [];
            HalleLokal = null;
            return;
        }

        (IReadOnlyList<Kompaktlokal> lokale, string standard) = hauptfenster.KompaktDatenLesen(
            d => (d.KompaktLokale(team), d.KompaktStandardlokal(team)),
            ((IReadOnlyList<Kompaktlokal>)[], string.Empty));
        bool andere = team != halleMannschaft;
        string gewaehlt = andere || halleLokal is null ? standard : halleLokal.Nummer;
        HalleMannschaft = team;
        if (!lokale.SequenceEqual(halleLokale))
        {
            HalleLokale = lokale;
        }

        HalleLokal = lokale.FirstOrDefault(l => l.Nummer == gewaehlt) ?? (lokale.Count > 0 ? lokale[0] : null);
        if (andere)
        {
            HalleMeldung = string.Empty;
        }
    }

    private async Task HalleFuerAlleAsync()
    {
        if (Rechnet || halleMannschaft.Length == 0 || halleLokal is not Kompaktlokal lokal)
        {
            return;
        }

        string team = halleMannschaft;
        await NacheinanderAsync(() => hauptfenster.KompaktMeldungAendernAsync(d => d.KompaktLokalFuerAlle(team, lokal.Nummer)));
        HalleMeldung = $"„{lokal}“ gilt jetzt für alle Heimtermine von {team} – auch für die, die noch eingetragen werden.";
    }
}
