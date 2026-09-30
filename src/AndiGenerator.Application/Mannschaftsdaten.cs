// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Stammdaten einer Mannschaft (Original <c>TFormEditTeamName</c>).</summary>
/// <param name="Name">Mannschaftsname.</param>
/// <param name="Id">Mannschafts-ID.</param>
/// <param name="VereinsId">Vereins-ID.</param>
/// <param name="Nummer">Mannschaftsnummer (1., 2., 3. Mannschaft).</param>
/// <param name="Spiellokal">Spiellokal; leer, falls der Verein nur in einem Spiellokal spielt.</param>
public sealed record Mannschaftsdaten(string Name, string Id, string VereinsId, int Nummer, string Spiellokal)
{
    /// <summary>Holt eine leere Mannschaft für „Neu“ (Nummer 1 wie im Original).</summary>
    public static Mannschaftsdaten Neu { get; } = new(string.Empty, string.Empty, string.Empty, 1, string.Empty);

    /// <summary>Holt die wählbaren Spiellokale (Original <c>getTeamLocationsInt</c>: leer und 1 bis 5).</summary>
    public static IReadOnlyList<string> Spiellokale { get; } = [string.Empty, "1", "2", "3", "4", "5"];

    /// <summary>Prüft die Eingaben wie der Dialog des Originals (<c>ButtonOKClick</c>).</summary>
    /// <returns>Die erste Fehlermeldung oder <c>null</c>.</returns>
    public string? Pruefen()
    {
        if (Name.Trim().Length == 0)
        {
            return "Der Mannschaftsname darf nicht leer sein";
        }

        if (Id.Trim().Length == 0)
        {
            return "Die Mannschafts-ID darf nicht leer sein";
        }

        return VereinsId.Trim().Length == 0 ? "Die Vereins-ID darf nicht leer sein" : null;
    }

    /// <summary>Werte ohne umgebende Leerzeichen (Original <c>GetValues</c> mit <c>Trim</c>).</summary>
    /// <returns>Die bereinigten Daten.</returns>
    public Mannschaftsdaten Bereinigt() => this with
    {
        Name = Name.Trim(),
        Id = Id.Trim(),
        VereinsId = VereinsId.Trim(),
        Spiellokal = Spiellokal.Trim(),
    };
}
