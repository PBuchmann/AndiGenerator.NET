// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Allgemeine Daten der Staffel (Original <c>TDialogPanelMainData</c>).</summary>
/// <param name="Name">Name der Liga.</param>
/// <param name="Id">Liganummer.</param>
/// <param name="Art">Art, z. B. <c>Herren</c> (Freitext möglich).</param>
/// <param name="Beginn">Start der Vorrunde.</param>
/// <param name="Rueckrundenbeginn">Start der Rückrunde (bei Halbrunden ohne Bedeutung).</param>
/// <param name="Ende">Ende der Rückrunde.</param>
public sealed record Ligadaten(string Name, string Id, string Art, DateOnly Beginn, DateOnly Rueckrundenbeginn, DateOnly Ende)
{
    /// <summary>Holt die vorgeschlagenen Arten (Original <c>GetGenderList</c>).</summary>
    public static IReadOnlyList<string> Arten { get; } =
    [
        "Herren", "Damen", "Jungen", "Jungen U18", "Mädchen", "Mädchen U18", "Schüler", "Schülerinnen", "Bambini",
        "Senioren 40", "Senioren 50", "Senioren 60", "Senioren 70", "Seniorinnen 40", "Seniorinnen 50", "Seniorinnen 60",
        "Seniorinnen 70",
    ];

    /// <summary>Prüft die Eingaben wie das Original (<c>CheckData</c>); die erste Meldung gewinnt.</summary>
    /// <returns>Die Fehlermeldung oder <c>null</c>, wenn alles gültig ist.</returns>
    public string? Pruefen()
    {
        if (Name.Trim().Length == 0)
        {
            return "Der Liganame darf nicht leer sein";
        }

        if (Id.Trim().Length == 0)
        {
            return "Die Liganummer darf nicht leer sein";
        }

        if (Art.Trim().Length == 0)
        {
            return "Die Art (Herren/Damen) darf nicht leer sein";
        }

        if (Ende <= Rueckrundenbeginn)
        {
            return "Das Ende der Rückrunde muss nach dem Start der Rückrunde sein";
        }

        return Rueckrundenbeginn <= Beginn ? "Der Start der Rückrunde muss nach dem Start der Vorrunde sein" : null;
    }
}
