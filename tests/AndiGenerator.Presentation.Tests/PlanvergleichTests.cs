// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Tests für den Vergleich im Dialog „Plan sichern“ im Kompaktmodus.</summary>
public sealed class PlanvergleichTests
{
    [Fact]
    public void Kompaktvergleich_zeigt_Kennzahlen_der_Kompaktspieltage_und_empfiehlt_den_kuerzeren_Plan()
    {
        Kompaktauswertung kurz = Auswertung(100, 200);
        Kompaktauswertung lang = Auswertung(200, 300);

        Planvergleich v = Planvergleich.Kompakt("Die Staffel wird geschlossen.", kurz, lang, [], null, new DateTime(2026, 10, 12, 9, 30, 0, DateTimeKind.Local));

        Assert.True(v.AktuellBesser);
        Assert.Contains(v.Zeilen, z => z.Bezeichnung == "Fahrten gesamt (Luftlinie)" && z.AktuellBesser);
        Assert.Contains(v.Zeilen, z => z.Bezeichnung == "Mannschaften ohne Heimspieltag" && !z.AktuellBesser && !z.GesichertBesser);
        Assert.DoesNotContain(v.Zeilen, z => z.Bezeichnung.StartsWith("Stufe", StringComparison.Ordinal));
        Assert.False(v.HatUnterschiede);
        Assert.StartsWith("Bewertet wie das beste Gesamtpaket", v.Bewertungshinweis, StringComparison.Ordinal);
    }

    private static Kompaktauswertung Auswertung(double kmA, double kmB) =>
        new([], [Mannschaft("A", kmA), Mannschaft("B", kmB)], [], [], 2, 2);

    private static Kompaktmannschaft Mannschaft(string name, double km) => new(name, null, string.Empty, null, string.Empty, km, 1);
}
