// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Ein Kriterium auf der Seite „Planung“ des normalen Modus als Balken (Entwurf 2D): so lang wie die Verstöße des besten
/// Plans, der Teil bis zur bewiesenen Grenze schraffiert (unvermeidbar), dazu der Wert des gespeicherten Plans als Strich.
/// </summary>
/// <param name="Name">Das Kriterium.</param>
/// <param name="Anzahl">Verstöße des besten Plans.</param>
/// <param name="Grenze">So viele hat jeder Plan mindestens (bewiesen; 0, wenn nichts bewiesen ist).</param>
/// <param name="Vorher">Verstöße des gespeicherten Plans, oder <c>null</c>, wenn es keinen gibt.</param>
/// <param name="Maximum">Der größte Wert der Gruppe (Maßstab der Balken).</param>
/// <param name="Stufe">A (Muss), B (Soll) oder C (Feinschliff) – die Farbe.</param>
/// <param name="Urteil">Z. B. „bestmöglich“ oder „mind. 6 · 2 offen“.</param>
/// <param name="Bestmoeglich">Ob es nachweislich nicht besser geht.</param>
/// <param name="Kriterium">Das Kriterium, wenn seine Wichtigkeit einstellbar ist (Feinschliff), sonst <c>null</c>.</param>
/// <param name="Wichtigkeit">Die eingestellte Wichtigkeit.</param>
/// <param name="WichtigkeitCommand">Setzt die Wichtigkeit (Parameter „Kriterium:Stufe“).</param>
public sealed record Normalbalken(
    string Name,
    int Anzahl,
    int Grenze,
    int? Vorher,
    int Maximum,
    Stufe Stufe,
    string Urteil,
    bool Bestmoeglich,
    Kostenkriterium? Kriterium = null,
    Kriterienwichtigkeit Wichtigkeit = Kriterienwichtigkeit.Normal,
    IRelayCommand<string>? WichtigkeitCommand = null)
{
    /// <summary>Gets a value indicating whether die Wichtigkeit einstellbar ist.</summary>
    public bool Einstellbar => Kriterium is not null && WichtigkeitCommand is not null;

    /// <summary>Gets a value indicating whether „nicht so wichtig“ gewählt ist.</summary>
    public bool NiedrigAktiv => Wichtigkeit == Kriterienwichtigkeit.Niedrig;

    /// <summary>Gets a value indicating whether „wie vorgesehen“ gewählt ist.</summary>
    public bool NormalAktiv => Wichtigkeit == Kriterienwichtigkeit.Normal;

    /// <summary>Gets a value indicating whether „besonders wichtig“ gewählt ist.</summary>
    public bool HochAktiv => Wichtigkeit == Kriterienwichtigkeit.Hoch;

    /// <summary>Gets den Befehlsparameter für „nicht so wichtig“.</summary>
    public string NiedrigParameter => Parameter(Kriterienwichtigkeit.Niedrig);

    /// <summary>Gets den Befehlsparameter für „wie vorgesehen“.</summary>
    public string NormalParameter => Parameter(Kriterienwichtigkeit.Normal);

    /// <summary>Gets den Befehlsparameter für „besonders wichtig“.</summary>
    public string HochParameter => Parameter(Kriterienwichtigkeit.Hoch);

    /// <summary>Gets den Hinweistext für „nicht so wichtig“.</summary>
    public string NiedrigHinweis => $"{Name}: nicht so wichtig. Das Kriterium darf schlechter werden, wenn dafür andere Kriterien besser werden.";

    /// <summary>Gets den Hinweistext für „wie vorgesehen“.</summary>
    public string NormalHinweis => $"{Name}: wie vom Programm vorgesehen. Hebt ein starkes Optimieren oder Zurückstellen wieder auf.";

    /// <summary>Gets den Hinweistext für „besonders wichtig“.</summary>
    public string HochHinweis => $"{Name}: so gut wie möglich optimieren, auch wenn dafür andere Kriterien etwas schlechter werden.";

    /// <summary>Gets die Zahl als Text.</summary>
    public string AnzahlText => Anzahl.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string Parameter(Kriterienwichtigkeit stufe) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{(int)(Kriterium ?? 0)}:{(int)stufe}");
}
