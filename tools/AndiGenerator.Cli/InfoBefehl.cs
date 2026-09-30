// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.ClickTt;

namespace AndiGenerator.Cli;

/// <summary><c>andigen info</c>: Überblick über eine click-TT-Exportdatei.</summary>
internal static class InfoBefehl
{
    public static int Ausfuehren(string datei)
    {
        ClickTtStaffel s = ClickTtLeser.Lesen(datei);
        Console.WriteLine($"Staffel:          {s.Name} (Id {s.Id}, {s.Geschlecht})");
        Console.WriteLine($"Zeitraum:         {s.Von:dd.MM.yyyy} – {s.Bis:dd.MM.yyyy}, Rückrunde ab {s.Rueckrundenbeginn:dd.MM.yyyy}");
        Console.WriteLine($"Mannschaften:     {s.Mannschaften.Count}");
        Console.WriteLine($"Heimspieltermine: {s.Mannschaften.Sum(m => m.Heimspieltermine.Count)}");
        Console.WriteLine($"Pflichtspieltage: {s.Pflichtspieltage.Count}");
        Console.WriteLine($"Bestehender Plan: {s.BestehenderSpielplan.Count} Spiele");
        foreach (ClickTtMannschaft m in s.Mannschaften)
        {
            Console.WriteLine($"  {m.Name,-45} Verein {m.VereinsId,-5} Termine {m.Heimspieltermine.Count,3}  Sperrzeiträume {m.Sperrzeitraeume.Count,3}");
        }

        return 0;
    }
}
