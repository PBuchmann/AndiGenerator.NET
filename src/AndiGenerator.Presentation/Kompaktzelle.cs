// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Zelle im Raster der Kompaktspieltage: Meldung einer Mannschaft an einem Sonntag und ihre Rolle im Plan.</summary>
/// <param name="Meldung">Art der Meldung: <c>heim</c>, <c>auswaerts</c>, <c>beides</c>, <c>nicht</c> oder <c>unbekannt</c>.</param>
/// <param name="Zeichen">Inhalt: ★ Ausrichter, ● Gast, ? keine Meldung, sonst leer (auch bei „kann nicht“).</param>
/// <param name="Verstoss">Die Rolle im Plan widerspricht der Meldung.</param>
/// <param name="Gedimmt">Gehört nicht zur gewählten Mannschaft.</param>
/// <param name="Hinweis">Tooltip.</param>
public sealed record Kompaktzelle(string Meldung, string Zeichen, bool Verstoss, bool Gedimmt, string Hinweis) : INotifyPropertyChanged
{
    private bool istCursor;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Holt, zu welchem Spieltag des Sonntags die Mannschaft gehört, wenn an diesem Sonntag mehrere stattfinden (0 = erster
    /// oder einziger, 1 = zweiter, 2 = dritter); ★ und ● erscheinen dann in verschiedenen Farben.
    /// </summary>
    public int Spieltag { get; init; }

    /// <summary>Holt einen Wert, der angibt, ob die Begegnung zum Spitzenspieltag gehört (schwarzer Ring um ★/●).</summary>
    public bool Spitzenspieltag { get; init; }

    /// <summary>Holt einen Wert, der angibt, ob die Begegnung zum Abstiegsspieltag gehört (blauer Ring um ★/●).</summary>
    public bool Abstiegsspieltag { get; init; }

    /// <summary>Holt den Namen der Mannschaft (Spalte).</summary>
    public string Mannschaft { get; init; } = string.Empty;

    /// <summary>Holt den Tag (Zeile).</summary>
    public DateOnly Tag { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob der Rahmen zum Bearbeiten auf dieser Zelle steht (ändert sich beim Bewegen
    /// des Rahmens, ohne dass das Raster neu aufgebaut wird).
    /// </summary>
    public bool IstCursor
    {
        get => istCursor;
        set
        {
            if (istCursor != value)
            {
                istCursor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IstCursor)));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft an diesem Sonntag eine Einzelbegegnung spielt (eigene Farbe).</summary>
    public bool Einzel { get; init; }

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft zum zweiten Spieltag des Sonntags gehört.</summary>
    public bool ZweiterSpieltag => !Einzel && Spieltag == 1;

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft zum dritten (oder weiteren) Spieltag des Sonntags gehört.</summary>
    public bool DritterSpieltag => !Einzel && Spieltag >= 2;

    /// <summary>Holt einen Wert, der angibt, ob nur Heim gemeldet ist.</summary>
    public bool IstHeim => Meldung == "heim";

    /// <summary>Holt einen Wert, der angibt, ob nur auswärts gemeldet ist.</summary>
    public bool IstAuswaerts => Meldung == "auswaerts";

    /// <summary>Holt einen Wert, der angibt, ob Heim und auswärts gemeldet sind.</summary>
    public bool IstBeides => Meldung == "beides";

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft nicht kann.</summary>
    public bool IstNicht => Meldung == "nicht";

    /// <summary>Holt einen Wert, der angibt, ob keine Meldung vorliegt.</summary>
    public bool IstUnbekannt => Meldung == "unbekannt";

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft an diesem Tag ausrichtet (★).</summary>
    public bool IstAusrichter => Zeichen == "★";

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft an diesem Tag Gast ist (●).</summary>
    public bool IstGast => Zeichen == "●";

    /// <summary>Holt einen Wert, der angibt, ob für die Mannschaft keine Meldung vorliegt (?).</summary>
    public bool IstUnbekanntZeichen => Zeichen == "?";

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft an diesem Tag spielt (★ oder ●).</summary>
    public bool Spielt => Zeichen is "★" or "●";
}
