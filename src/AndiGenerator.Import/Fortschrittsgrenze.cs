// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Import;

/// <summary>
/// Zeitgrenze nach Fortschritt für <see cref="HighsLoeser.LoesenMitSchranke"/>: mindestens <paramref name="Mindestens"/>
/// Sekunden, danach Schluss, sobald HiGHS <paramref name="Stillstand"/> Sekunden lang weder einen besseren Plan noch eine höhere
/// Untergrenze gefunden hat (die feste Zeitgrenze gilt als Obergrenze).
/// </summary>
/// <param name="Mindestens">Sekunden, die HiGHS auf jeden Fall rechnet.</param>
/// <param name="Stillstand">Sekunden ohne Fortschritt, nach denen HiGHS aufhört.</param>
public sealed record Fortschrittsgrenze(double Mindestens, double Stillstand);
