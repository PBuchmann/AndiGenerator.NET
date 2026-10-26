// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Stand eines Schritts der gezielten Konstruktion: gefundene Kilometer, nötiges Minimum, Fortschritt und Stillstand.</summary>
public class SchrittverlaufTests
{
    [Fact]
    public void Nennt_gefundene_und_mindestens_noetige_Kilometer()
    {
        // 10 Punkte stehen für 2.400 km: 1 Punkt = 240 km.
        var verlauf = new Schrittverlauf("Kürzester Plan", true, false, 240, TimeSpan.FromSeconds(40));
        Assert.Equal("Kürzester Plan: noch kein Plan gefunden", verlauf.Zeile(null));

        verlauf.Zeit(TimeSpan.FromSeconds(5));
        verlauf.Aufnehmen(new Loeserstand(10.5, 10));
        verlauf.Zeit(TimeSpan.FromSeconds(17));

        Assert.Equal("Kürzester Plan: bisher 2.520 km · mindestens ≈ 2.400 km sind nötig · zuletzt vorangekommen vor 12 s", verlauf.Zeile(null));
        Assert.EndsWith("rechnet weiter, solange es vorangeht (höchstens noch 1:05 min)", verlauf.Zeile(TimeSpan.FromSeconds(65)), StringComparison.Ordinal);
        Assert.False(verlauf.Steht);

        verlauf.Aufnehmen(new Loeserstand(10.5, 10.5));
        Assert.StartsWith("Kürzester Plan: bisher 2.520 km · bestmöglich (bewiesen)", verlauf.Zeile(null), StringComparison.Ordinal);
    }

    [Fact]
    public void Steht_ohne_besseren_Plan_und_ohne_deutlich_kleinere_Luecke()
    {
        var verlauf = new Schrittverlauf("Plan mit den wenigsten Punkten", false, false, 240, TimeSpan.FromSeconds(40));
        verlauf.Zeit(TimeSpan.Zero);
        verlauf.Aufnehmen(new Loeserstand(12, 10));

        // Die Lücke schrumpft nur um 1 % – kein Fortschritt; nach 40 s steht der Schritt.
        verlauf.Zeit(TimeSpan.FromSeconds(30));
        verlauf.Aufnehmen(new Loeserstand(12, 10.02));
        verlauf.Zeit(TimeSpan.FromSeconds(41));
        Assert.True(verlauf.Steht);

        // Eine um mehr als 2 % kleinere Lücke ist Fortschritt.
        verlauf.Aufnehmen(new Loeserstand(12, 10.2));
        Assert.False(verlauf.Steht);
        Assert.StartsWith("Plan mit den wenigsten Punkten: bisher 2.880 km · noch nicht bewiesen, dass es kein besseres gibt", verlauf.Zeile(null), StringComparison.Ordinal);

        // Bewiesen: Wert und Schranke gleich.
        verlauf.Aufnehmen(new Loeserstand(12, 12));
        Assert.StartsWith("Plan mit den wenigsten Punkten: bisher 2.880 km · bestmöglich (bewiesen)", verlauf.Zeile(null), StringComparison.Ordinal);
    }

    [Fact]
    public void Weitere_Plaene_nennen_keine_Schranke()
    {
        var verlauf = new Schrittverlauf("Weiterer Plan mit anderen Spieltagen", false, true, 240, TimeSpan.FromSeconds(40));
        verlauf.Aufnehmen(new Loeserstand(12, 3));

        Assert.StartsWith("Weiterer Plan mit anderen Spieltagen: bisher 2.880 km · zuletzt", verlauf.Zeile(null), StringComparison.Ordinal);
    }
}
