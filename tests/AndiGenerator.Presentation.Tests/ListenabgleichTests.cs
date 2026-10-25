// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Abgleich angezeigter Listen: Gleiches bleibt stehen, nur Geändertes wird ersetzt.</summary>
public sealed class ListenabgleichTests
{
    [Fact]
    public void Nur_geaenderte_Eintraege_werden_ersetzt()
    {
        var liste = new ObservableCollection<string>(["a", "b", "c"]);
        var aenderungen = new List<NotifyCollectionChangedAction>();
        liste.CollectionChanged += (_, e) => aenderungen.Add(e.Action);

        Assert.False(Listenabgleich.Abgleichen(liste, ["a", "b", "c"]));
        Assert.Empty(aenderungen);

        Assert.True(Listenabgleich.Abgleichen(liste, ["a", "x", "c", "d"]));
        Assert.Equal(["a", "x", "c", "d"], liste);
        Assert.Equal([NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Add], aenderungen);

        Assert.True(Listenabgleich.Abgleichen(liste, ["a"]));
        Assert.Equal(["a"], liste);
    }
}
