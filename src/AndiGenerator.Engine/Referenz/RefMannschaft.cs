// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>TMannschaft</c>: Stammdaten, Spiele und Hilfsfunktionen (Kosten in <c>RefMannschaft.Kosten.cs</c>).</summary>
internal sealed partial class RefMannschaft
{
    public RefMannschaft(int index)
    {
        Index = index;
    }

    private enum Suchrichtung
    {
        Beide,
        Hoeher,
        Tiefer,
    }

    public string TeamName { get; private set; } = string.Empty;

    public string ClubId { get; private set; } = string.Empty;

    public string TeamId { get; private set; } = string.Empty;

    public int TeamNummer { get; private set; }

    public string StandardLokal { get; private set; } = string.Empty;

    /// <summary>Nachbarspiele je Tag (Original <c>TMapSisterGames</c>, Schlüssel <c>Trunc(Date)</c>).</summary>
    public Dictionary<int, List<RefNachbarspiel>> Nachbarspiele { get; } = [];

    public List<string> KeinWochenspiel { get; } = [];

    public List<double> Sperrtermine { get; } = [];

    public List<double> Ausweichtermine { get; } = [];

    public List<RefWunschtermin> Wunschtermine { get; } = [];

    public List<RefAuswaertskoppel> Auswaertskoppeln { get; } = [];

    public int HeimspieleRundeEins { get; private set; } = -1;

    public List<(string Team, HeimrechtWert Wert)> Heimrechte { get; } = [];

    public List<RefMannschaft> VereinsteamsImPlan { get; } = [];

    /// <summary>Spiele nach Datum sortiert, undatierte vorne (Original <c>gamesRef</c>).</summary>
    public List<RefSpiel> Spiele { get; } = [];

    public int Index { get; set; }

    /// <summary>Original <c>TMannschaft.load</c> und <c>loadGameDays</c>/<c>loadSisterGameDays</c>.</summary>
    public void Laden(Mannschaft quelle)
    {
        ClubId = quelle.VereinsId;
        TeamId = quelle.Id;
        TeamNummer = quelle.Nummer;
        TeamName = quelle.Name;
        StandardLokal = quelle.Spiellokal;
        HeimspieleRundeEins = quelle.HeimspieleHinrunde;

        foreach (Heimspieltermin termin in quelle.Heimspieltermine)
        {
            HeimspielLaden(termin);
        }

        foreach (DateOnly tag in quelle.Sperrtermine)
        {
            Sperrtermine.Add(DelphiDatum.Wert(tag));
            Sperrtermine.Sort();
        }

        foreach (Auswaertskoppel koppel in quelle.Auswaertskoppeln)
        {
            AuswaertsKoppelTyp art = koppel.Art switch
            {
                Auswaertskoppelart.AmSelbenTag => AuswaertsKoppelTyp.GleicherTag,
                Auswaertskoppelart.AnVerschiedenenTagen => AuswaertsKoppelTyp.AndererTag,
                _ => AuswaertsKoppelTyp.Beliebig,
            };
            Auswaertskoppeln.Add(new RefAuswaertskoppel(koppel.MannschaftA, koppel.MannschaftB, art));
        }

        foreach (Heimrechtvorgabe recht in quelle.Heimrechte)
        {
            Heimrechte.Add((recht.Gegner, recht.Runde == Runde.Hinrunde ? HeimrechtWert.Runde1 : HeimrechtWert.Runde2));
        }

        KeinWochenspiel.AddRange(quelle.KeinWochenspielGegen);

        RefPlan.StabilSortieren(Wunschtermine, (a, b) => a.Datum.CompareTo(b.Datum));
        Ausweichtermine.Sort();
        Sperrtermine.Sort();

        foreach (Nachbarmannschaft nachbar in quelle.Nachbarmannschaften)
        {
            NachbarmannschaftLaden(nachbar);
        }
    }

    /// <summary>Original <c>THomeRightList.getValueForTeam</c>.</summary>
    public HeimrechtWert HeimrechtGegen(string teamName)
    {
        foreach ((string team, HeimrechtWert wert) in Heimrechte)
        {
            if (team == teamName)
            {
                return wert;
            }
        }

        return HeimrechtWert.Keins;
    }

    /// <summary>Original <c>getKoppeledGameAfter</c>.</summary>
    public RefSpiel? GekoppeltesSpielDanach(int index)
    {
        if (index < Spiele.Count - 1 && index >= 0 && IstGekoppelt(Spiele[index], Spiele[index + 1]))
        {
            return GekoppeltesSpielDanach(index + 1) is not null ? null : Spiele[index + 1];
        }

        return null;
    }

    /// <summary>Original <c>getKoppeledGameBefore</c>.</summary>
    public RefSpiel? GekoppeltesSpielDavor(int index) =>
        index > 0 && GekoppeltesSpielDanach(index - 1) is not null ? Spiele[index - 1] : null;

    /// <summary>Original <c>getKoppeledGame(Index)</c>.</summary>
    public RefSpiel? GekoppeltesSpiel(int index)
    {
        RefSpiel? ergebnis = GekoppeltesSpielDanach(index);
        if (ergebnis is null && index > 0 && ReferenceEquals(Spiele[index], GekoppeltesSpielDanach(index - 1)))
        {
            ergebnis = Spiele[index - 1];
        }

        return ergebnis;
    }

    /// <summary>Original <c>IsKoppelTermin</c>: Optionen des Termins, nur für die Heimmannschaft.</summary>
    public Wunschterminoptionen KoppelOptionen(RefSpiel spiel) => ReferenceEquals(spiel.Heim, this) ? spiel.DatumOptionen : default;

    /// <summary>Original <c>InternIsKoppeledGames</c>.</summary>
    public bool IstGekoppelt(RefSpiel spiel1, RefSpiel spiel2)
    {
        int abstand = Math.Abs(DelphiDatum.Trunc(spiel1.Datum) - DelphiDatum.Trunc(spiel2.Datum));
        if (abstand > 1)
        {
            return false;
        }

        bool ergebnis = ReferenceEquals(spiel1.Heim, this) && ReferenceEquals(spiel2.Heim, this)
            && Terminoptionen.IstKoppel(KoppelOptionen(spiel1)) && Terminoptionen.IstKoppel(KoppelOptionen(spiel2));

        if (ReferenceEquals(spiel1.Gast, this) && ReferenceEquals(spiel2.Gast, this)
            && spiel1.Gast.IstGueltigesAuswaertsKoppelDatum(spiel1.Datum, spiel2.Datum, spiel1.Heim.TeamName, spiel2.Heim.TeamName))
        {
            ergebnis = true;
        }

        return ergebnis;
    }

    /// <summary>Original <c>IsValidAuswaertsKoppelDate</c>.</summary>
    public bool IstGueltigesAuswaertsKoppelDatum(double datum1, double datum2, string teamA, string teamB)
    {
        int tage = Math.Abs(DelphiDatum.Trunc(datum1) - DelphiDatum.Trunc(datum2));
        if (tage > 1)
        {
            return false;
        }

        bool ergebnis = false;
        foreach (RefAuswaertskoppel koppel in Auswaertskoppeln)
        {
            bool datumGueltig = koppel.Art switch
            {
                AuswaertsKoppelTyp.Keiner => false,
                AuswaertsKoppelTyp.GleicherTag => tage == 0 && Math.Abs(datum1 - datum2) >= DelphiDatum.MinGameDistance,
                AuswaertsKoppelTyp.AndererTag => tage == 1,
                _ => tage <= 1 && Math.Abs(datum1 - datum2) >= DelphiDatum.MinGameDistance,
            };

            if (datumGueltig
                && ((teamA == koppel.TeamA && teamB == koppel.TeamB) || (teamA == koppel.TeamB && teamB == koppel.TeamA)))
            {
                ergebnis = true;
            }
        }

        return ergebnis;
    }

    /// <summary>Original <c>isAusWeichTermin</c>.</summary>
    public bool IstAusweichtermin(double datum) => Ausweichtermine.BinarySearch(DelphiDatum.Trunc(datum)) >= 0;

    /// <summary>Original <c>IsTerminFree</c>.</summary>
    public bool IstTerminFrei(RefWunschtermin termin, RefPlan plan, RefMannschaft heim, RefMannschaft gast, RefSpiel? ignorieren, bool alleZweitzeitenGueltig)
    {
        bool ergebnis = true;
        double datum = termin.Datum;

        if (!alleZweitzeitenGueltig)
        {
            if (Terminoptionen.Hat(termin.Optionen, Wunschterminoptionen.KoppelZweitzeit) && ReferenceEquals(heim, this))
            {
                ergebnis = false;
            }

            if (Terminoptionen.Hat(termin.Optionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit) && ReferenceEquals(gast, this))
            {
                ergebnis = false;
            }
        }

        foreach (RefSpiel spiel in Spiele)
        {
            if (ReferenceEquals(spiel, ignorieren) || DelphiDatum.Trunc(spiel.Datum) != DelphiDatum.Trunc(datum))
            {
                continue;
            }

            if (!plan.HatAuswaertsKoppeltermine() && !plan.HatKoppeltermine())
            {
                return false;
            }

            if (spiel.Datum <= 0)
            {
                continue;
            }

            if (Math.Abs(spiel.Datum - datum) < DelphiDatum.MinGameDistance)
            {
                return false;
            }

            if (plan.HatAuswaertsKoppeltermine() && ReferenceEquals(gast, this) && ReferenceEquals(spiel.Gast, this)
                && IstGueltigesAuswaertsKoppelDatum(spiel.Datum, datum, heim.TeamName, spiel.Heim.TeamName))
            {
                ergebnis = true;
                continue;
            }

            if (!ReferenceEquals(this, heim) || !Terminoptionen.IstKoppel(termin.Optionen) || !Terminoptionen.IstKoppel(KoppelOptionen(spiel)))
            {
                return false;
            }

            ergebnis = true;
        }

        return ergebnis;
    }

    /// <summary>Original <c>IsMannschaftInAuswaertsKoppelWunschAtOneDay</c> (Cache <c>TeamsOnDay</c>).</summary>
    public bool HatAuswaertskoppelAmSelbenTagMit(RefPlan plan, RefMannschaft heim) =>
        Auswaertskoppeln.Exists(k => k.Art is AuswaertsKoppelTyp.GleicherTag or AuswaertsKoppelTyp.Beliebig
            && k.MannschaftA(plan) is not null && k.MannschaftB(plan) is not null
            && (k.TeamA == heim.TeamName || k.TeamB == heim.TeamName));

    /// <summary>
    /// Original <c>CreateAuswaertsKoppeledGamesDependenciesList</c>: die Spiele dieser Mannschaft als Gast bei den
    /// Auswärtskoppel-Partnern von <paramref name="heim"/>; <c>null</c>, wenn es keine gibt.
    /// </summary>
    public List<RefSpiel>? AuswaertskoppelAbhaengigkeiten(RefPlan plan, RefMannschaft heim)
    {
        var partner = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (RefAuswaertskoppel koppel in Auswaertskoppeln)
        {
            RefMannschaft? a = koppel.MannschaftA(plan);
            RefMannschaft? b = koppel.MannschaftB(plan);
            if (a is not null && b is not null)
            {
                PartnerHinzufuegen(partner, a.TeamId, b.TeamId);
                PartnerHinzufuegen(partner, b.TeamId, a.TeamId);
            }
        }

        if (partner.Count == 0 || !partner.TryGetValue(heim.TeamId, out List<string>? ids))
        {
            return null;
        }

        List<RefSpiel>? ergebnis = null;
        foreach (List<RefSpiel> spiele in ids.Select(id => plan.SpieleDerPaarung(id, TeamId)).OfType<List<RefSpiel>>())
        {
            ergebnis ??= [];
            ergebnis.AddRange(spiele);
        }

        return ergebnis;
    }

    /// <summary>Original <c>getCache</c>: Nachbarmannschaften, deren Heimspiele am selben Tag stattfinden sollen.</summary>
    public HashSet<string> TeamsFuerGleicheHeimspieltage()
    {
        var ergebnis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RefNachbarspiel spiel in Nachbarspiele.Values.SelectMany(l => l).Where(s => s.ParalleleHeimspiele))
        {
            ergebnis.Add(spiel.Geschlecht + "\n" + spiel.Mannschaftsname);
        }

        return ergebnis;
    }

    /// <summary>Original <c>GetHallenBelegung</c>.</summary>
    public int Hallenbelegung(double datum, string lokal, RefPlan plan, List<string>? belegtVon)
    {
        int anzahl = 0;
        double fenster = DelphiDatum.EncodeTime(1, 29);
        if (Nachbarspiele.TryGetValue(DelphiDatum.Trunc(datum), out List<RefNachbarspiel>? liste))
        {
            foreach (RefNachbarspiel spiel in liste.Where(s => s.IstHeimspiel && Math.Abs(s.Datum - datum) <= fenster && lokal == s.Lokal))
            {
                anzahl++;
                belegtVon?.Add(spiel.Heim + " (" + spiel.Geschlecht + ")");
            }
        }

        foreach (RefMannschaft verein in VereinsteamsImPlan)
        {
            foreach (RefSpiel spiel in verein.Spiele)
            {
                if (ReferenceEquals(spiel.Heim, verein) && !ReferenceEquals(spiel.Gast, this)
                    && Math.Abs(spiel.Datum - datum) <= fenster && spiel.Lokal == lokal)
                {
                    anzahl++;
                    belegtVon?.Add(verein.TeamName + " (" + plan.Geschlecht + ")");
                }
            }
        }

        return anzahl;
    }

    /// <summary>Original <c>GetMissingParallelHomeGames</c>.</summary>
    public int FehlendeGleichzeitigeHeimspiele(double datum)
    {
        HashSet<string> teams = TeamsFuerGleicheHeimspieltage();
        if (Nachbarspiele.TryGetValue(DelphiDatum.Trunc(datum), out List<RefNachbarspiel>? liste))
        {
            foreach (RefNachbarspiel spiel in liste.Where(s => s.IstHeimspiel && s.ParalleleHeimspiele && DelphiDatum.Trunc(s.Datum) == DelphiDatum.Trunc(datum)))
            {
                teams.Remove(spiel.Geschlecht + "\n" + spiel.Mannschaftsname);
            }
        }

        return teams.Count;
    }

    /// <summary>Original <c>GetParallelGames</c>.</summary>
    public int ParalleleSpiele(double datum, RefPlan plan)
    {
        int anzahl = 0;
        ParalleleSpieleIntern(datum, plan, TeamNummer, Suchrichtung.Beide, ref anzahl);
        return anzahl;
    }

    /// <summary>Original <c>TAuswaertsKoppelCachedTeams.AddTeamIdToTeamId</c>.</summary>
    private static void PartnerHinzufuegen(Dictionary<string, List<string>> partner, string von, string zu)
    {
        if (!partner.TryGetValue(von, out List<string>? liste))
        {
            liste = [];
            partner.Add(von, liste);
        }

        if (!liste.Contains(zu))
        {
            liste.Add(zu);
        }
    }

    private void ParalleleSpieleIntern(double datum, RefPlan plan, int teamNummer, Suchrichtung richtung, ref int anzahl)
    {
        double fenster = DelphiDatum.EncodeTime(3, 59);
        if (Nachbarspiele.Count > 0 && Nachbarspiele.TryGetValue(DelphiDatum.Trunc(datum), out List<RefNachbarspiel>? liste))
        {
            foreach (RefNachbarspiel spiel in liste)
            {
                bool pruefen;
                if (richtung == Suchrichtung.Beide)
                {
                    pruefen = spiel.KeineParallelenSpiele;
                }
                else
                {
                    int diff = teamNummer - spiel.Nummer;
                    pruefen = spiel.Geschlecht == plan.Geschlecht
                        && ((diff == -1 && richtung == Suchrichtung.Tiefer) || (diff == 1 && richtung == Suchrichtung.Hoeher));
                }

                if (pruefen && Math.Abs(spiel.Datum - datum) <= fenster)
                {
                    anzahl++;
                    WeiterSuchen(datum, plan, spiel.Nummer, richtung, ref anzahl);
                }
            }
        }

        foreach (RefMannschaft verein in VereinsteamsImPlan)
        {
            foreach (RefSpiel spiel in verein.Spiele)
            {
                if (ReferenceEquals(spiel.Heim, this) || ReferenceEquals(spiel.Gast, this))
                {
                    continue;
                }

                int diff = teamNummer - verein.TeamNummer;
                bool passend = (diff == -1 && richtung is Suchrichtung.Beide or Suchrichtung.Tiefer)
                    || (diff == 1 && richtung is Suchrichtung.Beide or Suchrichtung.Hoeher);
                if (passend && Math.Abs(spiel.Datum - datum) <= fenster)
                {
                    anzahl++;
                    WeiterSuchen(datum, plan, verein.TeamNummer, richtung, ref anzahl);
                }
            }
        }
    }

    private void WeiterSuchen(double datum, RefPlan plan, int nummer, Suchrichtung richtung, ref int anzahl)
    {
        if (richtung is Suchrichtung.Beide or Suchrichtung.Tiefer)
        {
            ParalleleSpieleIntern(datum, plan, nummer, Suchrichtung.Tiefer, ref anzahl);
        }

        if (richtung is Suchrichtung.Beide or Suchrichtung.Hoeher)
        {
            ParalleleSpieleIntern(datum, plan, nummer, Suchrichtung.Hoeher, ref anzahl);
        }
    }

    /// <summary>Original <c>loadHomeGame</c>.</summary>
    private void HeimspielLaden(Heimspieltermin termin)
    {
        double datum = DelphiDatum.Wert(termin.Zeitpunkt);
        var wunsch = new RefWunschtermin
        {
            Datum = datum,
            ParalleleSpiele = termin.MaxParalleleSpiele,
            Spiellokal = termin.Spiellokal.Length > 0 ? termin.Spiellokal : StandardLokal,
        };
        Wunschtermine.Add(wunsch);

        double zweitzeit = DelphiDatum.Trunc(datum) + (termin.ZweiteUhrzeit is TimeOnly z ? DelphiDatum.EncodeTime(z.Hour, z.Minute) : 0.0);
        Wunschterminoptionen optionen = default;
        if (termin.Koppelung == Terminkoppelung.Koppeltermin)
        {
            optionen = termin.Prioritaet switch
            {
                Koppelprioritaet.Weich => Wunschterminoptionen.KoppelWeich,
                Koppelprioritaet.Hart => Wunschterminoptionen.KoppelHart,
                _ => Wunschterminoptionen.KoppelMoeglich,
            };
            wunsch.KoppelZweitzeit = zweitzeit;
        }
        else if (termin.Koppelung == Terminkoppelung.Doppelspieltag)
        {
            optionen = termin.Prioritaet switch
            {
                Koppelprioritaet.Weich => Wunschterminoptionen.DoppelWeich,
                Koppelprioritaet.Hart => Wunschterminoptionen.DoppelHart,
                _ => Wunschterminoptionen.DoppelMoeglich,
            };
        }

        wunsch.Optionen = optionen;

        if (termin.Ausweichtermin)
        {
            Ausweichtermine.Add(DelphiDatum.Trunc(wunsch.Datum));
        }

        if (!Terminoptionen.IstKoppelAmTag(wunsch.Optionen) && termin.ZweiteUhrzeitFuerAuswaertskoppel)
        {
            wunsch.Optionen |= Wunschterminoptionen.AuswaertsKoppelHatZweitzeit;
            wunsch.KoppelZweitzeit = zweitzeit;
        }
    }

    /// <summary>Original <c>loadSisterTeam</c>.</summary>
    private void NachbarmannschaftLaden(Nachbarmannschaft nachbar)
    {
        foreach (Nachbarspiel spiel in nachbar.Spiele)
        {
            bool heimspiel = nachbar.Name == spiel.Heim;
            string lokal = heimspiel ? nachbar.Spiellokal : string.Empty;
            if (spiel.Spiellokal.Length > 0)
            {
                lokal = spiel.Spiellokal;
            }

            var eintrag = new RefNachbarspiel
            {
                Datum = DelphiDatum.Wert(spiel.Zeitpunkt),
                Nummer = nachbar.Nummer,
                Mannschaftsname = nachbar.Name,
                Geschlecht = nachbar.Geschlecht,
                IstHeimspiel = heimspiel,
                Heim = spiel.Heim,
                Gast = spiel.Gast,
                Lokal = lokal,
                KeineParallelenSpiele = nachbar.KeineParallelenSpiele,
                ParalleleHeimspiele = nachbar.ParalleleHeimspiele,
            };

            int tag = DelphiDatum.Trunc(eintrag.Datum);
            if (!Nachbarspiele.TryGetValue(tag, out List<RefNachbarspiel>? liste))
            {
                liste = [];
                Nachbarspiele.Add(tag, liste);
            }

            liste.Add(eintrag);
        }
    }
}
