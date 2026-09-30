# FastMM4.pas

**Kategorie:** Drittbibliothek (Speichermanager)

## Zweck / Inhalt
**FastMM4** von Pierre le Riche (Open Source, MPL 1.1 / LGPL 2.1), ~11.700 Zeilen, ersetzt den Delphi-Standardspeichermanager. In `AndiGenerator.dpr` als erste Unit eingebunden (außer bei `DEBUG_MULTITHREAD`). Bietet schnellere, multithread-taugliche Allokation und Leck-Erkennung.

## Threading / Performance
Wichtig für das Original: Die Optimierung erzeugt/zerstört pro Durchlauf viele temporäre Listen (`TGameList.Create(False)` u.ä.) in bis zu ~120 Threads. Laut `FastMM4Options.inc` sind `AssumeMultiThreaded` und `NeverSleepOnThreadContention` gesetzt – d.h. die Allokations-Performance unter Thread-Konkurrenz war ein bekanntes Thema.

## Migrationshinweise für C#
- **Entfällt vollständig** – .NET hat einen verwalteten Heap mit GC.
- Konsequenz: Der C#-Hot-Path muss **allokationsfrei** sein (vorallokierte Arrays pro Worker, `Span<T>`, `stackalloc`, `ArrayPool<T>`), sonst dominiert der GC. Empfehlung: `<ServerGarbageCollector>true</ServerGarbageCollector>` + `<ConcurrentGarbageCollection>` testen, Allokationen mit BenchmarkDotNet `[MemoryDiagnoser]` überwachen.

**Aufwand:** – (entfällt); indirekt relevant für Engine-Design.
