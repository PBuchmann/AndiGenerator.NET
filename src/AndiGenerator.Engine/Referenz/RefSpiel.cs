// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>TGame</c>.</summary>
internal sealed class RefSpiel(RefMannschaft heim, RefMannschaft gast)
{
    /// <summary>Stand der zuletzt akzeptierten Lösung (Original <c>FDateRef</c> usw.).</summary>
    private (double Datum, Wunschterminoptionen Optionen, int MaxHeim, RefWunschtermin? Wunsch, bool NichtNotwendig) gemerkt;

    public RefMannschaft Heim { get; } = heim;

    public RefMannschaft Gast { get; } = gast;

    public double Datum { get; private set; }

    public Wunschterminoptionen DatumOptionen { get; private set; }

    public int DatumMaxHeim { get; private set; }

    public RefWunschtermin? Wunschtermin { get; private set; }

    public bool FestesDatum { get; private set; }

    public bool NichtNotwendig { get; private set; }

    public string UeberschriebenesLokal { get; set; } = string.Empty;

    public bool DatumGueltig => Datum != 0.0;

    /// <summary>Original <c>getLocation</c>.</summary>
    public string Lokal => UeberschriebenesLokal.Length > 0 ? UeberschriebenesLokal : Wunschtermin?.Spiellokal ?? string.Empty;

    /// <summary>Original <c>setDate</c>.</summary>
    public void SetzeDatum(double datum, Wunschterminoptionen optionen, int maxHeim, bool festesDatum, bool nichtNotwendig, RefWunschtermin? wunschtermin)
    {
        Datum = datum;
        DatumOptionen = optionen;
        DatumMaxHeim = maxHeim;
        FestesDatum = festesDatum;
        NichtNotwendig = nichtNotwendig;
        if (Wunschtermin is not null)
        {
            Wunschtermin.ZugeordnetesSpiel = null;
        }

        Wunschtermin = wunschtermin;
        if (Wunschtermin is not null)
        {
            Wunschtermin.ZugeordnetesSpiel = this;
        }
    }

    /// <summary>Original <c>setEmptyDate</c> bzw. <c>setDate(0.0, [], 0, false, false, nil)</c>.</summary>
    public void DatumLeeren() => SetzeDatum(0.0, default, 0, false, false, null);

    /// <summary>Original <c>TransferDateToRefDate</c> für dieses Spiel.</summary>
    public void Merken() => gemerkt = (Datum, DatumOptionen, DatumMaxHeim, Wunschtermin, NichtNotwendig);

    /// <summary>Original <c>TransferRefDateToDate</c> für dieses Spiel (feste Spiele bleiben).</summary>
    public void Zuruecksetzen()
    {
        if (!FestesDatum)
        {
            SetzeDatum(gemerkt.Datum, gemerkt.Optionen, gemerkt.MaxHeim, false, gemerkt.NichtNotwendig, gemerkt.Wunsch);
        }
    }

    /// <summary>Original <c>ClearAllValues</c>.</summary>
    public void AlleWerteLoeschen()
    {
        Datum = 0.0;
        DatumOptionen = default;
        DatumMaxHeim = 0;
        FestesDatum = false;
        Wunschtermin = null;
        NichtNotwendig = false;
        gemerkt = default;
        UeberschriebenesLokal = string.Empty;
    }

    /// <summary>Original <c>isTheSame</c>.</summary>
    public bool IstGleich(RefSpiel anderes) =>
        Datum == anderes.Datum && anderes.Heim.TeamId == Heim.TeamId && anderes.Gast.TeamId == Gast.TeamId;

    /// <summary>Original <c>AsString</c> (vereinfacht, nur für Meldungen).</summary>
    public string Text() => (DatumGueltig ? DelphiDatum.Text(Datum) + ": " : string.Empty) + Heim.TeamName + " - " + Gast.TeamName;
}
