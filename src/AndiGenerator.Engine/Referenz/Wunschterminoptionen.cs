// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>TWunschterminOptions</c> (Menge von <c>TWunschterminOption</c>).</summary>
[Flags]
internal enum Wunschterminoptionen
{
    KoppelMoeglich = 1 << 0,
    KoppelWeich = 1 << 1,
    KoppelHart = 1 << 2,
    DoppelMoeglich = 1 << 3,
    DoppelWeich = 1 << 4,
    DoppelHart = 1 << 5,
    KoppelZweitzeit = 1 << 6,
    AuswaertsKoppelZweitzeit = 1 << 7,
    AuswaertsKoppelHatZweitzeit = 1 << 8,
}
