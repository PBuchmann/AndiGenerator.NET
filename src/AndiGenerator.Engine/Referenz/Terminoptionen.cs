// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>OptionIsKoppel</c> usw.</summary>
internal static class Terminoptionen
{
    private const Wunschterminoptionen AlleKoppel =
        Wunschterminoptionen.KoppelMoeglich | Wunschterminoptionen.KoppelWeich | Wunschterminoptionen.KoppelHart
        | Wunschterminoptionen.DoppelMoeglich | Wunschterminoptionen.DoppelWeich | Wunschterminoptionen.DoppelHart;

    public static bool IstKoppel(Wunschterminoptionen o) => (o & AlleKoppel) != 0;

    public static bool IstKoppelHart(Wunschterminoptionen o) => (o & (Wunschterminoptionen.KoppelHart | Wunschterminoptionen.DoppelHart)) != 0;

    public static bool IstKoppelWeich(Wunschterminoptionen o) => (o & (Wunschterminoptionen.KoppelWeich | Wunschterminoptionen.DoppelWeich)) != 0;

    public static bool IstKoppelAmTag(Wunschterminoptionen o) =>
        (o & (Wunschterminoptionen.KoppelMoeglich | Wunschterminoptionen.KoppelWeich | Wunschterminoptionen.KoppelHart)) != 0;

    public static bool Hat(Wunschterminoptionen o, Wunschterminoptionen flag) => (o & flag) != 0;
}
