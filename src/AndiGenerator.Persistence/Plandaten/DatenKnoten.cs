// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>
/// Generischer Datenknoten – 1:1-Portierung von <c>TDataObject</c> (PlanDataObjects). Trägt die Eingangsdaten einer Staffel
/// (click-TT-Import, <c>.modifications</c>, eigenes Planformat). Die Diff/Merge-Semantik entspricht exakt dem Original
/// (Doku PlanDataObjects, „Merge/Diff-Semantik“). Die Attributreihenfolge bleibt erhalten, damit Dateien byte-gleich
/// zurückgeschrieben werden können.
/// </summary>
public sealed class DatenKnoten
{
    private readonly OrderedDictionary<string, string> attribute = new(StringComparer.Ordinal);

    /// <summary>Erzeugt einen Knoten und hängt ihn (falls angegeben) als letztes Kind an <paramref name="eltern"/> an.</summary>
    /// <param name="name">Knotenname, z. B. <c>team</c>; für ihn muss ein <see cref="KnotenSchluessel"/> definiert sein.</param>
    /// <param name="eltern">Elternknoten oder <c>null</c> für einen freistehenden Knoten.</param>
    /// <exception cref="PlanDatenFormatException">Für den Knotennamen ist kein Schlüssel definiert.</exception>
    public DatenKnoten(string name, DatenKnoten? eltern = null)
    {
        if (!KnotenSchluessel.IstBekannt(name))
        {
            throw new PlanDatenFormatException($"Kein Schlüssel für den Knotentyp '{name}' definiert.");
        }

        Name = name;
        Eltern = eltern;
        eltern?.Kinder.Add(this);
    }

    /// <summary>Holt den Knotennamen (XML-Elementname).</summary>
    public string Name { get; }

    /// <summary>Holt oder setzt den Status in einer Differenz (<c>state</c>-Attribut).</summary>
    public KnotenStatus Status { get; set; }

    /// <summary>Holt den Elternknoten oder <c>null</c> für die Wurzel.</summary>
    public DatenKnoten? Eltern { get; private set; }

    /// <summary>Holt die Kindknoten in Dokumentreihenfolge.</summary>
    public List<DatenKnoten> Kinder { get; } = [];

    /// <summary>Holt die Attribute in der Reihenfolge, in der sie gesetzt wurden.</summary>
    public IEnumerable<KeyValuePair<string, string>> Attribute => attribute;

    /// <summary>Erzeugt die Differenz „nachher gegenüber vorher“ als neuen Wurzelknoten.</summary>
    /// <param name="nachher">Neuer Stand.</param>
    /// <param name="vorher">Ausgangsstand.</param>
    /// <returns>Die Differenz; siehe <see cref="DifferenzBilden"/>.</returns>
    public static DatenKnoten Differenz(DatenKnoten nachher, DatenKnoten vorher)
    {
        ArgumentNullException.ThrowIfNull(nachher);
        var ergebnis = new DatenKnoten(nachher.Name);
        ergebnis.DifferenzBilden(nachher, vorher);
        return ergebnis;
    }

    /// <summary>Liest ein Attribut (Original <c>GetAsString</c>).</summary>
    /// <param name="name">Attributname.</param>
    /// <returns>Der Wert oder ein leerer Text, wenn das Attribut fehlt.</returns>
    public string Lesen(string name) => attribute.TryGetValue(name, out string? wert) ? wert : string.Empty;

    /// <summary>Prüft, ob das Attribut vorhanden ist (auch mit leerem Wert).</summary>
    /// <param name="name">Attributname.</param>
    /// <returns><c>true</c>, wenn das Attribut gesetzt ist.</returns>
    public bool Hat(string name) => attribute.ContainsKey(name);

    /// <summary>Setzt ein Attribut (Original <c>SetAsString</c>); der Wert wird immer getrimmt.</summary>
    /// <param name="name">Attributname.</param>
    /// <param name="wert">Neuer Wert; <c>null</c> wird zu einem leeren Text.</param>
    public void Setzen(string name, string? wert) => attribute[name] = DelphiKompatibel.Trim(wert);

    /// <summary>Setzt ein Wahrheitswert-Attribut als <c>true</c>/<c>false</c> (Original <c>SetAsBool</c>).</summary>
    /// <param name="name">Attributname.</param>
    /// <param name="wert">Neuer Wert.</param>
    public void Setzen(string name, bool wert) => Setzen(name, wert ? "true" : "false");

    /// <summary>Setzt ein Zahl-Attribut (Original <c>SetAsInt</c>).</summary>
    /// <param name="name">Attributname.</param>
    /// <param name="wert">Neuer Wert.</param>
    public void Setzen(string name, int wert) => Setzen(name, wert.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Liest ein Wahrheitswert-Attribut (Original <c>GetAsBool</c>).</summary>
    /// <param name="name">Attributname.</param>
    /// <returns><c>true</c> nur beim exakten Wert <c>true</c>.</returns>
    public bool LesenBool(string name) => Lesen(name) == "true";

    /// <summary>Liest ein Zahl-Attribut (Original <c>GetAsInt</c>).</summary>
    /// <param name="name">Attributname.</param>
    /// <returns>Der Wert oder 0, wenn er fehlt oder keine Zahl ist.</returns>
    public int LesenZahl(string name) => DelphiKompatibel.ZahlLesen(Lesen(name), 0);

    /// <summary>Liefert alle Kinder mit diesem Namen.</summary>
    /// <param name="name">Knotenname.</param>
    /// <returns>Die Kinder in Dokumentreihenfolge.</returns>
    public IEnumerable<DatenKnoten> KinderMitNamen(string name) => Kinder.Where(k => k.Name == name);

    /// <summary>Liefert das erste Kind mit diesem Namen.</summary>
    /// <param name="name">Knotenname.</param>
    /// <returns>Das Kind oder <c>null</c>.</returns>
    public DatenKnoten? ErstesKind(string name) => Kinder.FirstOrDefault(k => k.Name == name);

    /// <summary>Bildet den Schlüssel dieses Knotens (Original <c>getKey</c>).</summary>
    /// <returns>Name und Werte der Schlüsselattribute.</returns>
    public KnotenSchluessel Schluessel() =>
        new(Name, KnotenSchluessel.Attribute(Name).Select(a => new KeyValuePair<string, string>(a, Lesen(a))).ToList());

    /// <summary>Sucht das Kind mit diesem Schlüssel (Original <c>findChildIndexByKey</c>).</summary>
    /// <param name="schluessel">Gesuchter Schlüssel.</param>
    /// <returns>Der Index oder −1.</returns>
    /// <exception cref="PlanDatenFormatException">Mehrere Kinder haben denselben Schlüssel („Doppelter Schlüssel“).</exception>
    public int KindIndex(KnotenSchluessel schluessel)
    {
        ArgumentNullException.ThrowIfNull(schluessel);
        int ergebnis = -1;
        for (int i = 0; i < Kinder.Count; i++)
        {
            DatenKnoten kind = Kinder[i];
            if (kind.Name == schluessel.Name && schluessel.Werte.All(w => kind.Lesen(w.Key) == w.Value))
            {
                if (ergebnis >= 0)
                {
                    throw new PlanDatenFormatException("Doppelter Schlüssel in Datei: " + schluessel.Name);
                }

                ergebnis = i;
            }
        }

        return ergebnis;
    }

    /// <summary>Übernimmt Attribute und Kinder als tiefe Kopie; der Status wird NICHT kopiert (Original <c>assign</c>).</summary>
    /// <param name="quelle">Zu kopierender Knoten.</param>
    public void Zuweisen(DatenKnoten quelle)
    {
        ArgumentNullException.ThrowIfNull(quelle);
        AttributeZuweisen(quelle);
        Kinder.Clear();
        foreach (DatenKnoten kind in quelle.Kinder)
        {
            new DatenKnoten(kind.Name, this).Zuweisen(kind);
        }
    }

    /// <summary>Ersetzt alle Attribute durch die der Quelle (Original <c>assignAttributes</c>).</summary>
    /// <param name="quelle">Knoten, dessen Attribute übernommen werden.</param>
    public void AttributeZuweisen(DatenKnoten quelle)
    {
        ArgumentNullException.ThrowIfNull(quelle);
        attribute.Clear();
        foreach (KeyValuePair<string, string> a in quelle.attribute)
        {
            Setzen(a.Key, a.Value);
        }
    }

    /// <summary>Erzeugt eine tiefe Kopie als neuen, freistehenden Knoten.</summary>
    /// <returns>Die Kopie (ohne Status).</returns>
    public DatenKnoten Kopie()
    {
        var kopie = new DatenKnoten(Name);
        kopie.Zuweisen(this);
        return kopie;
    }

    /// <summary>Entfernt alle Kinder mit diesem Namen.</summary>
    /// <param name="name">Knotenname.</param>
    public void KinderEntfernen(string name) => Kinder.RemoveAll(k => k.Name == name);

    /// <summary>
    /// Führt eine Differenz in diesen Baum ein (Original <c>TDataObject.Merge</c>): <c>dosModified</c> übernimmt alle Attribute,
    /// <c>dosNew</c> ersetzt (Kopie am Ende), <c>dosDelete</c> entfernt; Änderungen an nicht vorhandenen Knoten werden verworfen.
    /// </summary>
    /// <param name="quelle">Die Differenz (z. B. aus einer <c>.modifications</c>-Datei).</param>
    public void Zusammenfuehren(DatenKnoten quelle)
    {
        ArgumentNullException.ThrowIfNull(quelle);
        if (quelle.Status == KnotenStatus.Geaendert)
        {
            foreach (KeyValuePair<string, string> a in quelle.attribute)
            {
                Setzen(a.Key, a.Value);
            }
        }

        foreach (DatenKnoten kind in quelle.Kinder)
        {
            int ziel = KindIndex(kind.Schluessel());
            switch (kind.Status)
            {
                case KnotenStatus.Neu:
                    if (ziel >= 0)
                    {
                        Kinder.RemoveAt(ziel);
                    }

                    new DatenKnoten(kind.Name, this).Zuweisen(kind);
                    break;
                case KnotenStatus.Geloescht:
                    if (ziel >= 0)
                    {
                        Kinder.RemoveAt(ziel);
                    }

                    break;
                default:
                    if (ziel >= 0)
                    {
                        Kinder[ziel].Zusammenfuehren(kind);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Macht diesen Knoten zur Differenz „nachher gegenüber vorher“ (Original <c>TDataObject.Diff</c>): Schlüsselattribute immer,
    /// geänderte Attribute mit dem Nachher-Wert (fehlend = leer), gelöschte Kinder zuerst, dann alle Nachher-Kinder als
    /// <c>dosNew</c>-Kopie bzw. rekursive Differenz (auch unveränderte Kinder erzeugen einen Knoten).
    /// </summary>
    /// <param name="nachher">Neuer Stand.</param>
    /// <param name="vorher">Ausgangsstand.</param>
    public void DifferenzBilden(DatenKnoten nachher, DatenKnoten vorher)
    {
        ArgumentNullException.ThrowIfNull(nachher);
        ArgumentNullException.ThrowIfNull(vorher);
        attribute.Clear();
        Kinder.Clear();

        foreach (KeyValuePair<string, string> wert in nachher.Schluessel().Werte)
        {
            Setzen(wert.Key, wert.Value);
        }

        foreach (string name in nachher.attribute.Keys.Concat(vorher.attribute.Keys).Where(n => nachher.Lesen(n) != vorher.Lesen(n)).ToList())
        {
            Setzen(name, nachher.Lesen(name));
            Status = KnotenStatus.Geaendert;
        }

        foreach (DatenKnoten kind in vorher.Kinder.Where(k => nachher.KindIndex(k.Schluessel()) < 0).ToList())
        {
            var geloescht = new DatenKnoten(kind.Name, this);
            geloescht.AttributeZuweisen(kind);
            geloescht.Status = KnotenStatus.Geloescht;
        }

        foreach (DatenKnoten kind in nachher.Kinder)
        {
            int index = vorher.KindIndex(kind.Schluessel());
            var neu = new DatenKnoten(kind.Name, this);
            if (index < 0)
            {
                neu.Zuweisen(kind);
                neu.Status = KnotenStatus.Neu;
            }
            else
            {
                neu.DifferenzBilden(kind, vorher.Kinder[index]);
            }
        }
    }

    /// <summary>Prüft die Inhaltsgleichheit in beiden Richtungen über Schlüssel (Original <c>isTheSame</c>); der Status zählt nicht.</summary>
    /// <param name="anderer">Vergleichsknoten.</param>
    /// <returns><c>true</c>, wenn Attribute und Kinder übereinstimmen.</returns>
    public bool IstGleich(DatenKnoten anderer)
    {
        ArgumentNullException.ThrowIfNull(anderer);
        return IstGleichEineRichtung(anderer) && anderer.IstGleichEineRichtung(this);
    }

    /// <summary>Erzeugt eine reihenfolgeunabhängige Textdarstellung inkl. Status (für Tests und Vergleiche).</summary>
    /// <returns>Der Text; gleiche Bäume ergeben den gleichen Text.</returns>
    public string Kanonisch()
    {
        var text = new StringBuilder();
        text.Append(Name).Append('[').Append(Status).Append(']');
        foreach (KeyValuePair<string, string> a in attribute.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            text.Append(' ').Append(a.Key).Append('=').Append(a.Value);
        }

        text.Append('{');
        foreach (string kind in Kinder.Select(k => k.Kanonisch()).Order(StringComparer.Ordinal))
        {
            text.Append(kind).Append(';');
        }

        return text.Append('}').ToString();
    }

    private bool IstGleichEineRichtung(DatenKnoten anderer)
    {
        bool attributeGleich = attribute.All(a => a.Value == anderer.Lesen(a.Key));

        // Wie im Original werden alle Kinder geprüft (auch nach einer Abweichung), damit doppelte Schlüssel auffallen.
        List<bool> kinderGleich = Kinder
            .Select(kind =>
            {
                int index = anderer.KindIndex(kind.Schluessel());
                return index >= 0 && kind.IstGleich(anderer.Kinder[index]);
            })
            .ToList();

        return attributeGleich && kinderGleich.TrueForAll(gleich => gleich);
    }
}
