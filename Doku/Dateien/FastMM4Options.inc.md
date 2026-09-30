# FastMM4Options.inc

**Kategorie:** Drittbibliothek (Speichermanager-Konfiguration)

## Zweck / Inhalt
Include-Datei mit Compiler-Schaltern für FastMM4. Aktiv u.a.: `ASMVersion`, `AssumeMultiThreaded`, `NeverSleepOnThreadContention` (+ `UseSwitchToThread`), `EnableMMX`, `EnableMemoryLeakReporting` mit `RequireDebuggerPresenceForLeakReporting`, `DetectMMOperationsAfterUninstall`, `NoDebugInfo`.

## Migrationshinweise
Entfällt. Die Intention (kein Schlafen bei Lock-Konkurrenz, Multithread-Allokation) ist für die C#-Engine als Design-Regel zu übernehmen: keine Allokationen und keine Locks im inneren Optimierungs-Loop.

**Aufwand:** –
