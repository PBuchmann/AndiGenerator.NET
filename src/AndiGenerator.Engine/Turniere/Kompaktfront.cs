// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Die besten Pläne aller Suchen (threadsicher): zuerst zählen wenige Mannschaften ohne Heimspieltag, wenige
/// Verstöße und wenige 3er-Spieltage (geht es nur mit 4er-Spieltagen, dann so); unter den Plänen mit den wenigsten bleiben die, die kein anderer in allen Kennzahlen übertrifft
/// (Kilometer, Stand, gemeinsamer Spieltag, Verteilung der Heimspiele, gleich weite Fahrten).
/// </summary>
internal sealed class Kompaktfront
{
    private const int Hoechstzahl = 400;

    private readonly Lock sperre = new();
    private readonly bool mitGruppen;
    private readonly List<Kompaktloesung> loesungen = [];

    /// <summary>Initialisiert eine Front.</summary>
    public Kompaktfront()
        : this(false)
    {
    }

    /// <summary>Initialisiert eine Front.</summary>
    /// <param name="mitGruppen">Auch die Zahl der Gruppenspieltage zählt (für Pläne mit 3er-Spieltagen).</param>
    public Kompaktfront(bool mitGruppen)
    {
        this.mitGruppen = mitGruppen;
    }

    /// <summary>
    /// Holt oder setzt, ob Pläne nur aus 4er-Spieltagen vorgehen (wenn sie rechnerisch aufgehen, z. B. 9 oder 10
    /// Mannschaften). Sonst ist die Zahl der 3er-Spieltage nur eines der Ziele, damit verschiedene Pläne zur Wahl bleiben.
    /// </summary>
    public bool ViererVorrang { get; set; }

    /// <summary>Holt einen Wert, der angibt, ob noch kein Plan gefunden wurde.</summary>
    public bool Leer
    {
        get
        {
            lock (sperre)
            {
                return loesungen.Count == 0;
            }
        }
    }

    /// <summary>Nimmt einen Plan auf, wenn ihn keiner übertrifft.</summary>
    /// <returns><c>true</c>, wenn der Plan aufgenommen wurde.</returns>
    public bool Hinzufuegen(Kompaktloesung neu)
    {
        lock (sperre)
        {
            if (loesungen.Count > 0)
            {
                int vergleich = Grundwertung(neu).CompareTo(Grundwertung(loesungen[0]));
                if (vergleich > 0)
                {
                    return false;
                }

                if (vergleich < 0)
                {
                    loesungen.Clear();
                }
            }

            if (loesungen.Count >= Hoechstzahl || loesungen.Exists(l => Uebertrifft(l, neu) || l.Schluessel == neu.Schluessel))
            {
                return false;
            }

            loesungen.RemoveAll(l => Uebertrifft(neu, l));
            loesungen.Add(neu);
            return true;
        }
    }

    /// <summary>Alle Pläne.</summary>
    public List<Kompaktloesung> Alle()
    {
        lock (sperre)
        {
            return loesungen.ToList();
        }
    }

    /// <summary>Was vor allem anderen zählt: erst keine Verstöße (Peter, 13.10.2026), dann Mannschaften ohne Heimspieltag, ggf. reine 4er-Pläne.</summary>
    private (int Verstoesse, int OhneHeimspieltag, int MitDreiern) Grundwertung(Kompaktloesung l) =>
        (l.Verstoesse, l.OhneHeimspieltag, ViererVorrang && l.Dreier > 0 ? 1 : 0);

    /// <summary>Ob a in keiner Kennzahl schlechter und in mindestens einer besser ist als b.</summary>
    private bool Uebertrifft(Kompaktloesung a, Kompaktloesung b)
    {
        bool gruppenGleich = !mitGruppen || a.GruppenTage == b.GruppenTage;
        bool nichtSchlechter = a.Bewertungskm <= b.Bewertungskm && a.Abstand <= b.Abstand && a.Gemeinsam >= b.Gemeinsam && a.Heimspiele <= b.Heimspiele && a.KmSpanne <= b.KmSpanne
            && a.Dreier <= b.Dreier && a.Einzel <= b.Einzel && (!mitGruppen || a.GruppenTage >= b.GruppenTage);
        bool besser = a.Bewertungskm < b.Bewertungskm || a.Abstand < b.Abstand || a.Gemeinsam > b.Gemeinsam || a.Heimspiele < b.Heimspiele || a.KmSpanne < b.KmSpanne
            || a.Dreier < b.Dreier || a.Einzel < b.Einzel || !gruppenGleich;
        return nichtSchlechter && besser;
    }
}
