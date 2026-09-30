// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>TRoundInfo</c>.</summary>
internal sealed record RefRunde(double Von, double Bis)
{
    public bool Enthaelt(double datum) => datum >= Von && datum < Bis;
}
