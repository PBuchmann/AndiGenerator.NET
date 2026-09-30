// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Stellt beim Beenden den vorherigen Pausenzustand wieder her (Original: <c>WasPaused</c>-Muster um Dialoge).</summary>
internal sealed class Pausenbereich(Optimierungsdienst dienst, bool warPausiert) : IDisposable
{
    private bool beendet;

    public void Dispose()
    {
        if (!beendet)
        {
            beendet = true;
            dienst.Pausiert = warPausiert;
        }
    }
}
