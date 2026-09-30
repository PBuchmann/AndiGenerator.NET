// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Hinweis zu einem Spiel als farbiger Chip.</summary>
/// <param name="Text">Der Hinweis wie im Original, z. B. „Ausweichtermin“.</param>
/// <param name="Art">Die Einordnung (Farbe).</param>
public sealed record Hinweischip(string Text, Hinweisart Art)
{
    private static readonly string[] Probleme = ["spielfreier Tag", "kein Wunschtermin", "doppeltes Spiel", "ungültiger Termin"];
    private static readonly string[] Warnungen = ["Ausweichtermin", "wegen Koppeltermin geänderte Uhrzeit"];

    /// <summary>Holt eine Erklärung für die Details.</summary>
    public string Erklaerung => Text switch
    {
        "spielfreier Tag" => "Der Termin liegt an einem spielfreien Tag.",
        "kein Wunschtermin" => "Die Heimmannschaft hat an diesem Tag keinen Wunschtermin gemeldet.",
        "doppeltes Spiel" => "Diese Begegnung ist mehrfach angesetzt.",
        "ungültiger Termin" => "Der Termin verletzt eine harte Regel (z. B. Sperrtermin oder Abstand).",
        "Ausweichtermin" => "Der Termin ist nur als Ausweichtermin gemeldet.",
        "wegen Koppeltermin geänderte Uhrzeit" => "Die Anfangszeit wurde wegen eines Koppeltermins geändert.",
        "Heimkoppelspiel/Doppelspieltag" => "Die Heimmannschaft spielt an diesem Tag gekoppelt (Doppelspieltag).",
        "Auswärtskoppelspiel" => "Die Gastmannschaft spielt an diesem Tag ein Koppelspiel.",
        "manuell festgelegter Termin" => "Der Termin ist als Vorgabespiel fest eingetragen.",
        _ => string.Empty,
    };

    /// <summary>
    /// Zerlegt den Hinweistext eines Spiels (durch Komma getrennt wie im Original) in Chips; ein Spiellokal mit Komma im
    /// Namen bleibt zusammen.
    /// </summary>
    /// <param name="hinweis">Der Hinweistext.</param>
    /// <returns>Die Chips.</returns>
    public static IReadOnlyList<Hinweischip> Aus(string hinweis)
    {
        ArgumentNullException.ThrowIfNull(hinweis);
        var teile = new List<string>();
        foreach (string teil in hinweis.Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            if (teile.Count > 0 && teile[^1].StartsWith("Spiellokal: ", StringComparison.Ordinal) && !Bekannt(teil))
            {
                teile[^1] += ", " + teil;
            }
            else
            {
                teile.Add(teil);
            }
        }

        return teile.Select(t => new Hinweischip(t, Einordnen(t))).ToList();
    }

    private static bool Bekannt(string teil) =>
        Probleme.Contains(teil) || Warnungen.Contains(teil) || teil.StartsWith("Spiellokal: ", StringComparison.Ordinal)
        || teil is "Heimkoppelspiel/Doppelspieltag" or "Auswärtskoppelspiel" or "manuell festgelegter Termin";

    private static Hinweisart Einordnen(string teil)
    {
        if (Probleme.Contains(teil))
        {
            return Hinweisart.Problem;
        }

        if (Warnungen.Contains(teil))
        {
            return Hinweisart.Warnung;
        }

        return teil.StartsWith("Spiellokal: ", StringComparison.Ordinal) ? Hinweisart.Neutral : Hinweisart.Info;
    }
}
