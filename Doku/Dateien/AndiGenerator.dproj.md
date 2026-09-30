# AndiGenerator.dproj

**Kategorie:** Projekt/Build

## Zweck / Inhalt
MSBuild-Projektdatei von RAD Studio (ProjectVersion 19.2, FrameworkType **VCL**). Plattformen Win32 und Win64, Konfigurationen Debug (`Cfg_1`) und Release (`Cfg_2`).

## Relevanter Inhalt
- **Compiler-Defines (Basis):** `PROFILE_1;SPECIALTHREAD` → `SPECIALTHREAD` ist in **allen** Builds aktiv (Spezial-Insel im Optimizer), `PROFILE` ist durch das Suffix `_1` faktisch deaktiviert.
- **Debug:** `DEBUG;TRACE;DEBUG_MULTITHREAD_NO`; Win32-Debug zusätzlich `CACHE_HIT_TEST;CACHE_TEST` (Selbsttest der Kosten-Caches).
- **Release:** `RELEASE`; Win32-Release: `TRACE_1;CACHE_HIT_TEST_1;CACHE_TEST_1` (= deaktiviert).
- **Versionsinfo:** Release-Win32 `FileVersion=24.5.1.0`, ProductName `Andi-Generator`, Beschreibung „Andigenerator, Termingenerator für Tischtennisstaffeln", Locale 1031 (Deutsch). Ältere Stände (1.0.x, 22.6.2) noch in anderen PropertyGroups.
- **Manifeste/Icons:** je Plattform `andigenerator32.manifest` / `andigenerator64.manifest`, Icons `AndiGenerator_Icon1.ico`/`_Icon2.ico` (referenziert auch `_Icon.ico`, `_Icon3.ico`, die fehlen).

## Migrationshinweise für C#
- Ersetzt durch `.sln` + SDK-Style `.csproj` (`net9.0`/`net10.0`), `Directory.Build.props` für gemeinsame Einstellungen.
- Defines → `<DefineConstants>` bzw. besser Laufzeit-Konfiguration (Trace/Cache-Test als Diagnose-Schalter).
- `SPECIALTHREAD`-Verhalten als Engine-Option (Standard: an) übernehmen.
- Versionsschema (JJ.M.Build, z.B. 24.5.1) in `<Version>` übernehmen – relevant für den Updatecheck.
- Release-Build: `TieredPGO`, `ServerGarbageCollector`/`ConcurrentGarbageCollection` prüfen, ggf. `PublishAot`/ReadyToRun (siehe Migrationsplan).

**Aufwand:** S.
