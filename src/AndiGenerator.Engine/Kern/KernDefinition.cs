// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Unveränderliche Stammdaten der schnellen Engine (MIGRATIONSPLAN E7): Mannschaften, Wunschtermine, Runden und alle
/// Wünsche als Arrays über Indizes. Wird aus dem geladenen Referenzmodell aufgebaut, damit Laden und Optionen exakt
/// dem Original folgen; von allen Workern gemeinsam gelesen.
/// </summary>
internal sealed class KernDefinition
{
    public const int AnzahlArten = 16;

    private readonly Dictionary<string, int> lokale = new(StringComparer.Ordinal) { [string.Empty] = 0 };
    private readonly int[][] sollJeTag;

    public KernDefinition(RefPlan plan)
    {
        Optionen = plan.Optionen;
        N = plan.Mannschaften.Count;
        Name = plan.Mannschaften.Select(m => m.TeamName).ToArray();
        Nummer = plan.Mannschaften.Select(m => m.TeamNummer).ToArray();
        HeimspieleRundeEins = plan.Mannschaften.Select(m => m.HeimspieleRundeEins).ToArray();
        Rundenplanung = Optionen.Rundenplanung;
        Doppelrunde = Optionen.Doppelrunde;
        FreitagZaehltZumWochenende = Optionen.FreitagZaehltZumWochenende;

        // Gleichheitsklassen der click-TT-Ids (das Original vergleicht Spiele und Paarungen über die Id).
        var idKlassen = new Dictionary<string, int>(StringComparer.Ordinal);
        IdKlasse = new int[N];
        for (int t = 0; t < N; t++)
        {
            IdKlasse[t] = Schluessel(idKlassen, plan.Mannschaften[t].TeamId);
        }

        Runden = plan.Runden.ToArray();
        RundeZuPlanen = Runden.Select(plan.IstZuPlanendeRunde).ToArray();

        // Wunschtermine
        var start = new int[N + 1];
        var termine = new List<RefWunschtermin>();
        for (int t = 0; t < N; t++)
        {
            start[t] = termine.Count;
            termine.AddRange(plan.Mannschaften[t].Wunschtermine);
        }

        start[N] = termine.Count;
        WunschStart = start;
        WunschDatum = termine.Select(w => w.Datum).ToArray();
        WunschTag = termine.Select(w => DelphiDatum.Trunc(w.Datum)).ToArray();
        WunschParallele = termine.Select(w => w.ParalleleSpiele).ToArray();
        WunschOptionen = termine.Select(w => w.Optionen).ToArray();
        WunschLokal = termine.Select(w => LokalId(w.Spiellokal)).ToArray();
        WunschRef = termine.ToArray();

        Sperrtage = plan.Mannschaften.Select(m => m.Sperrtermine.ToArray()).ToArray();
        Ausweichtage = plan.Mannschaften.Select(m => m.Ausweichtermine.ToArray()).ToArray();
        KeinWochenspielGegen = new bool[N * N];
        KeinWochenspielAnzahl = plan.Mannschaften.Select(m => m.KeinWochenspiel.Count).ToArray();
        HeimrechtGegen = new HeimrechtWert[N * N];
        for (int t = 0; t < N; t++)
        {
            for (int h = 0; h < N; h++)
            {
                KeinWochenspielGegen[(t * N) + h] = plan.Mannschaften[t].KeinWochenspiel.Contains(Name[h]);
                HeimrechtGegen[(t * N) + h] = plan.Mannschaften[t].HeimrechtGegen(Name[h]);
            }
        }

        HatHeimrechte = plan.Mannschaften.Select(m => m.Heimrechte.Count > 0).ToArray();

        // Auswärtskoppel
        Auswaertskoppeln = plan.Mannschaften.Select(m => m.Auswaertskoppeln
            .Select(k => (A: plan.Mannschaften.FindIndex(x => x.TeamName == k.TeamA), B: plan.Mannschaften.FindIndex(x => x.TeamName == k.TeamB), k.Art))
            .ToArray()).ToArray();
        TeamsAmSelbenTag = new bool[N * N];
        for (int t = 0; t < N; t++)
        {
            for (int h = 0; h < N; h++)
            {
                TeamsAmSelbenTag[(t * N) + h] = plan.Mannschaften[t].HatAuswaertskoppelAmSelbenTagMit(plan, plan.Mannschaften[h]);
            }
        }

        AuswaertskoppelSpiele = new int[N * N][];
        AuswaertskoppelSpieleVorhanden = new bool[N * N];
        for (int t = 0; t < N; t++)
        {
            for (int h = 0; h < N; h++)
            {
                List<RefSpiel>? spiele = plan.Mannschaften[t].AuswaertskoppelAbhaengigkeiten(plan, plan.Mannschaften[h]);
                AuswaertskoppelSpiele[(t * N) + h] = spiele?.Select(s => plan.SpieleErlaubt.IndexOf(s)).ToArray() ?? [];
                AuswaertskoppelSpieleVorhanden[(t * N) + h] = spiele is not null;
            }
        }

        Vereinsteams = plan.Mannschaften.Select(m => m.VereinsteamsImPlan.Select(v => v.Index).ToArray()).ToArray();
        GleicherVerein = new bool[N * N];
        for (int t = 0; t < N; t++)
        {
            foreach (int v in Vereinsteams[t])
            {
                GleicherVerein[(t * N) + v] = true;
            }
        }

        // Nachbarspiele je Tag
        Nachbarspiele = new Dictionary<int, Nachbarspiel[]>[N];
        var zwangsSchluessel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        GleicheHeimtageTeams = new int[N][];
        for (int t = 0; t < N; t++)
        {
            RefMannschaft m = plan.Mannschaften[t];
            Nachbarspiele[t] = m.Nachbarspiele.ToDictionary(
                e => e.Key,
                e => e.Value.Select(s => new Nachbarspiel(
                    s.Datum,
                    DelphiDatum.Trunc(s.Datum),
                    s.IstHeimspiel,
                    LokalId(s.Lokal),
                    s.Nummer,
                    s.Geschlecht == plan.Geschlecht,
                    s.KeineParallelenSpiele,
                    s.ParalleleHeimspiele,
                    Schluessel(zwangsSchluessel, s.Geschlecht + "\n" + s.Mannschaftsname))).ToArray());
            GleicheHeimtageTeams[t] = m.TeamsFuerGleicheHeimspieltage().Select(k => Schluessel(zwangsSchluessel, k)).ToArray();
        }

        AnzahlZwangsSchluessel = zwangsSchluessel.Count;

        // Spiele (erlaubte, in Erzeugungsreihenfolge des Originals) und Paarungen
        SpielHeim = plan.SpieleErlaubt.Select(s => s.Heim.Index).ToArray();
        SpielGast = plan.SpieleErlaubt.Select(s => s.Gast.Index).ToArray();
        Paarung = new int[N * N][];
        for (int h = 0; h < N; h++)
        {
            for (int g = 0; g < N; g++)
            {
                List<RefSpiel>? liste = h == g ? null : plan.SpieleDerPaarung(plan.Mannschaften[h].TeamId, plan.Mannschaften[g].TeamId);
                Paarung[(h * N) + g] = liste?.Select(s => plan.SpieleErlaubt.IndexOf(s)).ToArray() ?? [];
            }
        }

        // Rang des Textes "Heim Gast" für die Sortierung bei gleichem Datum (Original compareStr).
        string[] schluessel = new string[N * N];
        for (int h = 0; h < N; h++)
        {
            for (int g = 0; g < N; g++)
            {
                schluessel[(h * N) + g] = Name[h] + " " + Name[g];
            }
        }

        int[] reihenfolge = Enumerable.Range(0, N * N).OrderBy(i => schluessel[i], StringComparer.Ordinal).ToArray();
        PaarungsRang = new int[N * N];
        for (int r = 0; r < reihenfolge.Length; r++)
        {
            int i = reihenfolge[r];
            PaarungsRang[i] = r > 0 && schluessel[reihenfolge[r - 1]] == schluessel[i] ? PaarungsRang[reihenfolge[r - 1]] : r;
        }

        Vorgegeben = plan.VorgegebeneSpiele.Select(s => new FesterTermin(s.Heim.Index, s.Gast.Index, s.Datum)).ToArray();
        Bestehend = plan.BestehenderSpielplan.Select(s => new FesterTermin(s.Heim.Index, s.Gast.Index, s.Datum)).ToArray();
        SpielfreieTage = [.. plan.SpielfreieTage];
        Pflichtzeiten = plan.Pflichtzeiten.ToArray();
        SetzlistenRang = Enumerable.Range(0, N).Select(i => plan.Setzlistenplaetze.IndexOf(i)).ToArray();
        HatSetzliste = plan.HatSetzliste();

        HatKoppeltermine = plan.HatKoppeltermine();
        HatAuswaertsKoppeltermine = plan.HatAuswaertsKoppeltermine();
        HatSechzigKilometer = plan.HatSechzigKilometerWerte();
        HatAusweichtermine = plan.HatAusweichtermine();
        AnzahlWunschtage = plan.AnzahlWunschtage();
        ErstesEchtesDatum = Enumerable.Range(0, Runden.Length).Select(plan.ErstesEchtesDatumInRunde).ToArray();
        LetztesEchtesDatum = Enumerable.Range(0, Runden.Length).Select(plan.LetztesEchtesDatumInRunde).ToArray();

        Faktor = new double[N * AnzahlArten];
        for (int t = 0; t < N; t++)
        {
            foreach (MannschaftsKostenart art in Enum.GetValues<MannschaftsKostenart>())
            {
                Faktor[(t * AnzahlArten) + (int)art] = plan.MannschaftKostenFaktor(plan.Mannschaften[t], art);
            }
        }

        WunschMontag = WunschDatum.Select(DelphiDatum.MontagDavor).ToArray();

        // Erster Wunschtermin derselben Mannschaft mit gleichem Zeitpunkt (Original: Suche per Datum in SpielIstGueltig).
        WunschErster = new int[WunschDatum.Length];
        for (int t = 0; t < N; t++)
        {
            for (int w = WunschStart[t]; w < WunschStart[t + 1]; w++)
            {
                int erster = w;
                for (int v = WunschStart[t]; v < w; v++)
                {
                    if (WunschDatum[v] == WunschDatum[w])
                    {
                        erster = v;
                        break;
                    }
                }

                WunschErster[w] = erster;
            }
        }

        WunschRunde = WunschDatum.Select(RundeZuDatum).ToArray();
        WunschInPlanung = WunschDatum.Select(IstInZuPlanenderRunde).ToArray();
        WunschSpielfrei = WunschTag.Select(t => SpielfreieTage.Count > 0 && SpielfreieTage.Contains(t)).ToArray();

        // Tagesbereich der Wunschtermine für die Belegungszähler je Mannschaft und Tag.
        ErsterTag = WunschTag.Length == 0 ? 0 : WunschTag.Min();
        AnzahlTage = WunschTag.Length == 0 ? 0 : WunschTag.Max() - ErsterTag + 1;
        RundenMontage = Runden
            .Select(r => WunschDatum.Where(d => d >= r.Von && d <= r.Bis).Select(DelphiDatum.MontagDavor).Distinct().Order().ToArray())
            .ToArray();

        // Sollspiele (Original getNumSollGamesIntern): je Runde (−1 … R−1) Präfixsummen der Termin-Gewichte je Tag,
        // Index i = Summe der Gewichte aller Wunschtage vor ErsterTag + i.
        sollJeTag = new int[Runden.Length + 1][];
        for (int r = -1; r < Runden.Length; r++)
        {
            int[] jeTag = new int[AnzahlTage + 1];
            for (int w = 0; w < WunschDatum.Length; w++)
            {
                if (r == RundeZuDatum(WunschDatum[w]) && !IstSpielfreierTag(WunschDatum[w]))
                {
                    jeTag[WunschTag[w] - ErsterTag + 1] += Terminoptionen.IstKoppel(WunschOptionen[w]) ? 2 : 1;
                }
            }

            for (int i = 1; i <= AnzahlTage; i++)
            {
                jeTag[i] += jeTag[i - 1];
            }

            sollJeTag[r + 1] = jeTag;
        }
    }

    public Berechnungsoptionen Optionen { get; }

    public int N { get; }

    public string[] Name { get; }

    public int[] Nummer { get; }

    public int[] IdKlasse { get; }

    public int[] HeimspieleRundeEins { get; }

    public Rundenplanung Rundenplanung { get; }

    public bool Doppelrunde { get; }

    public bool FreitagZaehltZumWochenende { get; }

    public RefRunde[] Runden { get; }

    public bool[] RundeZuPlanen { get; }

    /// <summary>Spieltag-Montage je Runde (Original <c>getSpieltagMondays(Von, Bis)</c>).</summary>
    public double[][] RundenMontage { get; }

    public int[] WunschStart { get; }

    public double[] WunschDatum { get; }

    public int[] WunschTag { get; }

    public int[] WunschParallele { get; }

    public Wunschterminoptionen[] WunschOptionen { get; }

    public int[] WunschLokal { get; }

    public RefWunschtermin[] WunschRef { get; }

    public double[] WunschMontag { get; }

    /// <summary>Runde je Wunschtermin (<see cref="RundeZuDatum"/>).</summary>
    public int[] WunschRunde { get; }

    /// <summary>Wunschtermin liegt in einer zu planenden Runde (<see cref="IstInZuPlanenderRunde"/>).</summary>
    public bool[] WunschInPlanung { get; }

    /// <summary>Wunschtermin liegt auf einem spielfreien Tag.</summary>
    public bool[] WunschSpielfrei { get; }

    /// <summary>Index des ersten Wunschtermins derselben Mannschaft mit demselben Zeitpunkt.</summary>
    public int[] WunschErster { get; }

    /// <summary>Kleinster Tag aller Wunschtermine (Delphi-Tagesnummer).</summary>
    public int ErsterTag { get; }

    /// <summary>Anzahl der Tage vom ersten bis zum letzten Wunschtermin.</summary>
    public int AnzahlTage { get; }

    public double[][] Sperrtage { get; }

    public double[][] Ausweichtage { get; }

    public bool[] KeinWochenspielGegen { get; }

    public int[] KeinWochenspielAnzahl { get; }

    public HeimrechtWert[] HeimrechtGegen { get; }

    public bool[] HatHeimrechte { get; }

    public (int A, int B, AuswaertsKoppelTyp Art)[][] Auswaertskoppeln { get; }

    public bool[] TeamsAmSelbenTag { get; }

    public int[][] AuswaertskoppelSpiele { get; }

    public bool[] AuswaertskoppelSpieleVorhanden { get; }

    public int[][] Vereinsteams { get; }

    /// <summary>Andere Mannschaft desselben Vereins (Index t·N + x).</summary>
    public bool[] GleicherVerein { get; }

    public Dictionary<int, Nachbarspiel[]>[] Nachbarspiele { get; }

    public int[][] GleicheHeimtageTeams { get; }

    public int AnzahlZwangsSchluessel { get; }

    public int[] SpielHeim { get; }

    public int[] SpielGast { get; }

    public int[][] Paarung { get; }

    public int[] PaarungsRang { get; }

    public FesterTermin[] Vorgegeben { get; }

    public FesterTermin[] Bestehend { get; }

    public HashSet<int> SpielfreieTage { get; }

    public RefPflichtzeit[] Pflichtzeiten { get; }

    public int[] SetzlistenRang { get; }

    public bool HatSetzliste { get; }

    public bool HatKoppeltermine { get; }

    public bool HatAuswaertsKoppeltermine { get; }

    public bool HatSechzigKilometer { get; }

    public bool HatAusweichtermine { get; }

    public int AnzahlWunschtage { get; }

    public double[] ErstesEchtesDatum { get; }

    public double[] LetztesEchtesDatum { get; }

    public double[] Faktor { get; }

    /// <summary>Original <c>getNumSollGamesIntern</c> (über Präfixsummen je Tag, ohne Speicheranforderung).</summary>
    public double SollSpiele(double von, double bis) => SollSpiele(RundeZuDatum(von), von, bis);

    /// <summary>Wie <see cref="SollSpiele(double, double)"/> mit bereits bekannter Runde von <paramref name="von"/>.</summary>
    public double SollSpiele(int rundeVon, double von, double bis)
    {
        int[] jeTag = sollJeTag[rundeVon + 1];
        int anzahlTermine = jeTag[AnzahlTage];
        if (anzahlTermine <= 0 || N <= 0)
        {
            return 0;
        }

        int tagVon = DelphiDatum.Trunc(von);
        int tagBis = DelphiDatum.Trunc(bis);
        int imIntervall = tagBis > tagVon ? jeTag[TagIndex(tagBis)] - jeTag[TagIndex(tagVon)] : 0;
        double faktor = (N - 1) / (1.0 * anzahlTermine);
        return faktor * imIntervall;
    }

    /// <summary>Original <c>IsFreeGameDate</c>.</summary>
    public bool IstSpielfreierTag(double datum) => SpielfreieTage.Contains(DelphiDatum.Trunc(datum));

    /// <summary>Original <c>DateIsAllowedForWeekendGames</c>.</summary>
    public bool IstWochenendeFuer60Km(double datum)
    {
        int tag = DelphiDatum.Wochentag(datum);
        return tag == 7 || tag == 1 || (tag == 6 && FreitagZaehltZumWochenende);
    }

    /// <summary>Nummer eines Spiellokals (0 = leer); nur beim Aufbau verwenden (nicht threadsicher).</summary>
    public int LokalId(string lokal)
    {
        if (!lokale.TryGetValue(lokal, out int id))
        {
            id = lokale.Count;
            lokale.Add(lokal, id);
        }

        return id;
    }

    /// <summary>Original <c>GetRoundNumberByDate</c>.</summary>
    public int RundeZuDatum(double datum)
    {
        for (int i = 0; i < Runden.Length; i++)
        {
            if (datum >= Runden[i].Von && datum < Runden[i].Bis)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Original <c>IsInRoundToGenerate</c>.</summary>
    public bool IstInZuPlanenderRunde(double datum) => Rundenplanung == Rundenplanung.Beide || IstRundeZuPlanen(RundeZuDatum(datum));

    /// <summary>Original <c>IsInRoundToGenerate</c> für eine bereits ermittelte Runde (−1 = außerhalb aller Runden).</summary>
    public bool IstRundeZuPlanen(int runde)
    {
        if (Rundenplanung == Rundenplanung.Beide)
        {
            return true;
        }

        if (runde < 0)
        {
            return false;
        }

        int grenze = Doppelrunde ? 1 : 0;
        return Rundenplanung switch
        {
            Rundenplanung.Halbrunde or Rundenplanung.NurVorrunde => runde <= grenze,
            Rundenplanung.NurRueckrunde or Rundenplanung.Corona => runde > grenze,
            _ => true,
        };
    }

    private static int Schluessel(Dictionary<string, int> tabelle, string text)
    {
        if (!tabelle.TryGetValue(text, out int id))
        {
            id = tabelle.Count;
            tabelle.Add(text, id);
        }

        return id;
    }

    /// <summary>Index in die Präfixsummen: Anzahl der Tage des Wunschbereichs vor <paramref name="tag"/>.</summary>
    private int TagIndex(int tag) => Math.Clamp(tag - ErsterTag, 0, AnzahlTage);
}
