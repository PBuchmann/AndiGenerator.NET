# PlanPanel (PlanPanel.pas + PlanPanel.dfm)

**Kategorie:** UI-Panel (eingebettet)
**Umfang:** 3422 Zeilen .pas / 381 Zeilen .dfm (davon ~140 Zeilen Bitmap-Daten einer ImageList)
**Abhängigkeiten (uses):** projekt-eigen: `PlanTypes`, `PlanUtils`, `PrintUtil`, `DialogPrintSelect`, `DialogForSingleGewichtungsOptions`, (implementation) `AndiGeneratorMain` (zyklisch: `getMainPlan`, `getCurrentViewedPlan`, `DoAfterMainPlanChanged`); RTL/VCL: `Windows` (`RGB`, `PtInRect`), `Graphics` (`TColor`, `TCanvas`), `Controls`, `Forms`, `ExtCtrls` (`TPaintBox`, `TPanel`), `ComCtrls` (`TPageControl`, `TTabSheet`, `TToolBar`), `Vcl.ImgList`, `System.DateUtils` (`WeekOf`, `DayOfWeek`), `System.Generics.Collections`, `StrUtils`, `Math`.
**Verwendet von:** `AndiGeneratorMain` (erzeugt `TFormPlanPanel`, hängt `PanelMain` per `SetPanel(PanelPlan)` in das Hauptfenster ein, ruft `SetPlan`, `SetZoom`, `Print`, setzt `pgcMain.ActivePageIndex`).

## Zweck
`TFormPlanPanel` ist die **zentrale, read-only Hauptanzeige** eines Plans im Hauptfenster: Terminwünsche, Nachbarmannschafts-Termine, Kostenaufstellung, Spielplan/Mannschaftspläne und diverse Diagramme. Das Formular selbst wird nie angezeigt; nur sein `PanelMain` wird in ein Panel des Hauptfensters umgehängt. **Alle Inhalte sind vollständig selbst gezeichnet** (5 × `TPaintBox` in `TScrollBox`, Zeichnen über `TZoomCanvas` aus `PrintUtil` mit Zoom-Faktor). Derselbe Zeichencode dient auch zum **Drucken** (Parameter `ForPrint`). Die einzige Interaktion ist das Anklicken von Kostenzellen im Kosten-Tab, das einen Dialog zum Ändern der **Gewichtung** öffnet. Es gibt **keine** manuelle Terminänderung, kein Drag&Drop und kein Kontextmenü in dieser Unit.

## Inhalt / Struktur

### UI-Aufbau (aus .dfm)
`FormPlanPanel` (1126×617, Font MS Sans Serif) → `PanelMain` (alClient) → `pgcMain: TPageControl` mit 5 Tabs (Reihenfolge = `ActivePageIndex`):

| Idx | TabSheet | Caption | Inhalt | Paint-Funktion |
|---|---|---|---|---|
| 0 | `tsTerminMeldung` | „Terminwünsche" | `ScrollBoxTerminMeldung` + `PaintBoxTerminMeldung` | `PaintTerminMeldung` |
| 1 | `tsSisterTeams` | „Termine der Nachbarmannschaften" | `ScrollBoxSisterTeams` + `PaintBoxSisterTeams` | `PaintSisterTeams` |
| 2 | `tsKosten` | „Kosten" | `ScrollBoxKosten` + `PaintBoxKosten` | `PaintKosten` |
| 3 | `tsPlanView` | „Terminplan" | `ToolBarTerminAuswahl` (3 gruppierte Text-Buttons) + `ScrollBoxTermine` + `PaintBoxTermine` | `PaintTermineMain` |
| 4 | `tsDiagrams` | „Diagramme" | `ScrollBoxVerteilung` + `PaintBoxVerteilung` | `PaintVerteilung` |

- Alle ScrollBoxen: `DoubleBuffered = True`, `Color = clWhite`. Alle PaintBoxen: Font Tahoma, Height −12; Events `OnPaint = PaintBoxPaint`, `OnMouseMove = PaintBoxMouseMove`, `OnMouseUp = PaintBoxMouseUp` (gemeinsame Handler, Unterscheidung über `Sender`).
- Toolbar im Tab „Terminplan": `ToolButtonSpielplan` („Spielplan", Down=True), `ToolButtonMannschaftsplaene` („Mannschaftspläne"), `ToolButtonMannschaftsplaeneMitNachbarn` („Mannschaftspläne mit Nachbarmannschaften") – verhalten sich wie Radio-Buttons (manuelles Down-Setzen in den Click-Handlern), danach `InvalidateAll`.
- `ImageListToolbar`: 1 Icon 16×16 (Tabellen-/Listen-Symbol, orange Rahmen, grüne Zeilen) für alle drei Buttons.
- DFM `ActivePage = tsDiagrams`, wird aber in `FormCreate` auf Index 0 gesetzt. Das Hauptfenster setzt Index 0 beim Laden eines Plans und Index 2 (Kosten) beim Start der Optimierung.

### Klassen
- `TFormPlanPanel` (TForm)
  - Felder: `Plan: TPlan` (eigene Kopie, in `FormCreate` erzeugt), `FZoomValue: MyDouble` (Default 1.0), `ActionRects: TList<TActionRect>` (klickbare Bereiche aller PaintBoxen, in Bildschirmkoordinaten).
  - Public: `SetPanel(ParentPanel)` (PanelMain umhängen), `SetPlan(Plan)` (→ `Self.Plan.Assign(Plan)` + `CalculateKosten(-1)` + `InvalidateAll`), `SetZoom(Value)`, `Print()`.
- `TActionRectKosten = class(TActionRectBase)` (Basis, leer) und vier Ableitungen, jeweils mit `DoAction` → öffnet `TDialogForSingleGewichtungsOptions`:
  - `TActionRectKostenPlan(KostenType: TPlanKostenType)` – Plan-weite Gewichtung (`getGewichtungPlan`/`setGewichtungPlan`).
  - `TActionRectKostenTypeMannschaft(KostenType: TMannschaftsKostenType)` – globale Gewichtung einer Kostenart (`GewichtungMainMannschaftsKosten[KostenType]`).
  - `TActionRectKostenMannschaftMain(teamId)` – Gesamtgewichtung einer Mannschaft (`GewichtungMannschaften.getMain/addMain`).
  - `TActionRectKostenMannschaftDetail(teamId, KostenType)` – Gewichtung einer Kostenart für eine Mannschaft (`getDetail/addDetail`); zeigt zusätzlich die **Detailmeldungen** (`CalculateKostenForType(..., tmDetailMessage)`) des aktuell angezeigten Plans im Dialog an (Fallback-Text: „Zu dieser Kostenart gibt es bei dieser Mannschaft keine Meldung").
  - Nach OK: Änderung am **Main-Plan** (`getMainPlan()`) und `DoAfterMainPlanChanged(True)` (speichert Optionen, triggert Neuberechnung/Neuzeichnung im Hauptfenster).

### Konstanten / Farben (initialization)
- `cFontBig = -18`, `cFontNormal = -12` (logische Pixel bei 96 dpi, negativ = Zeichenhöhe).
- `cColorBrushGray = RGB(240,240,240)` (Zebra-Zeilen), `cColorLineGray = RGB(200,200,200)` (Wochenraster), `cColorHomeGame = Rot`, `cColorAwayGame = Schwarz`, `cColorHomeGameBubble = RGB(255,128,128)`, `cColorAwayGameBubble = RGB(128,128,128)`, `cColorGameDayLine1 = RGB(255,215,0)` (Gold), `cColorGameDayLine2 = RGB(0,205,205)` (Türkis), `cColorNormalWunsch = Schwarz`, `cColorKoppelWunsch = Blau`, `cColorSperrWunsch = Rot`.
- Lokal: `AmplitudeX = AmplitudeY = 10`; Kosten-Tabelle `cColWidth = 110`, `cRowHeight = 30`, `cActionHeight = 20`.
- Weitere Farben inline: Nachbarmannschaft direkt = `RGB(0,0,255)`, Nachbarspiele grau `RGB(128,128,128)`, Terminwunsch-Auswertung Rot `RGB(255,0,0)`, Gelb/Orange `RGB(255,165,0)`, Grün `RGB(60,179,113)`.

### Zeichen-Pipeline (Screen)
`PaintBoxPaint(Sender)`:
1. `ClearActionRects(PaintBox)` – alle ActionRects dieser PaintBox entfernen.
2. `TZoomCanvas.Create(PaintBox.Canvas, FZoomValue)` (Screen-DPI), weiß füllen.
3. Je nach PaintBox die Paint-Funktion aufrufen → liefert logische Gesamtgröße `TSize`.
4. `PaintBox.Height := Max(100, ConvertYCoord(cy)+40)`, `PaintBox.Width := Max(500, ConvertXCoord(cx)+40)` → die ScrollBox bekommt dadurch ihre Scrollfläche.
5. `AddActionRects` – die beim Zeichnen gesammelten ActionRects (logische Koordinaten) werden auf Bildschirmkoordinaten (Zoom/DPI) umgerechnet und in `ActionRects` übernommen.

Alle Paint-Funktionen arbeiten in einem **logischen 96-dpi-Koordinatensystem** mit festen Spaltenpositionen (z. B. x = 0/100/150/400/420/720 im Spielplan) und liefern die Inhaltsgröße zurück. Es wird immer der **gesamte Inhalt** gezeichnet (keine Virtualisierung; Clipping nur durch GDI).

### Interaktion
- `PaintBoxMouseMove`: Cursor `crHandPoint`, wenn Maus über einem ActionRect der PaintBox, sonst `crDefault`.
- `PaintBoxMouseUp`: für jeden ActionRect der PaintBox, der den Punkt enthält → `DoAction` (egal welche Maustaste).
- ActionRects werden nur im Kosten-Tab erzeugt. Keine Tastatur-Bedienung, keine Tooltips, kein Kontextmenü, kein Drag.

### Ansichten im Detail

**1. Terminwünsche (`PaintTerminMeldung`)**
- Leer: „Kein Plan geladen".
- Titel „Terminwünsche:" (nur Screen).
- `PaintSpielPlanMeldungen`: Zeitachse (`PaintDateLine`: „KW" + Kalenderwochen-Nummern ab Montag vor `GetFirstDate`), Wochenraster + Zebra (`PaintDateRaster`, Zeilenhöhe 30), pro Mannschaft: Name + 2 px breite, 10 px hohe Striche je Wunschtermin (schwarz normal, blau Koppel; x auf 12:00 Uhr normiert) und je Sperrtermin (rot). Wunschtermine mit `woAuswaertsKoppelSecondTime` werden ausgelassen. Legende „Terminwunsch / Sperrtermin / Koppeltermin/Doppelspieltag" und Hinweise „Keine Wünsche für die 60km-Regel", „Keine Koppeltermine/Doppelspieltage gemeldet", „Keine Auswärtskoppelwünsche gemeldet".
- Danach pro Mannschaft ein Textblock (Überschrift groß):
  - je Wunschtermin (ohne `woAuswaertsKoppelSecondTime`): `ddd dd.mm.yyyy hh:mm`, ggf. Koppel-Option (`cWunschterminOptionName`), „maximale Heimspiele: n" (`ParallelGames`), „Ausweichtermin", „Spiellokal: …", Hallenbelegung (`GetHallenBelegung`) → gelb/orange „Halle ist belegt von: …" wenn `ParallelGames>0` und `ParallelGames <= Belegung`, parallele Spiele (`GetParallelGames`) „parallele Spiele (n): …", rot „spielfreier Tag" wenn `Plan.FreeGameDays` den Tag enthält.
  - „Sperrtermine (n Stück):" – nur die mit `Plan.IsInRoundToGenerate`.
  - „Auswärtskoppelwünsche:" `TeamA und TeamB am gleichen Tag | mit Übernachtung | mit optionaler Übernachtung` (`ktNone` übersprungen).
  - „Spiele gegen diese Mannschaften nur am Wochenende (60km Regel):" + Liste `noWeekGame`.
  - „Heimrecht:" (`HomeRightCountRoundOne`, `HomeRights[].AsString`).
  - „Auswertung:" – Termin-Statistik (siehe Fachliche Logik), grün/rot eingefärbt; bei Rückrunde zusätzlich rote Liste `getTermineNotPossible`.

**2. Termine der Nachbarmannschaften (`PaintSisterTeams`)**
- Legende blau: „Blau: Spiele direkter Nachbarmannschaften".
- Pro Mannschaft: `SisterGames.toSortedArray` (gruppiert nach Tag) → Datum (nur erste Zeile der Gruppe), Uhrzeit, `(gender) Heim - Gast Spiellokal: …`; blau wenn `PreventParallelGame`.

**3. Kosten (`PaintKosten`)** – die wichtigste interaktive Ansicht
- „Gesamtkosten:" `Plan.CalculateKosten(-1)`.
- Bedingte Zeilen (nur wenn > 0): „Spiele nicht terminiert" (`CalculateFehlterminKosten`), „ungültige Spiele" (`CalculateNotAllowedKosten`), „Spiele an spielfreien Tagen" (`CalculateFreeGameDateKosten`).
- Immer: „Überlappung Spieltage", „Länge Spieltage", „Überlappung letzer Spieltag", „Länge letzer Spieltag" (aus `CalculateSpielTagKosten`), bedingt „Vereinsinterne Spiele am Anfang" → jeweils klickbar (`TActionRectKostenPlan`), mit Gewichtungsanzeige.
- **Tabelle Mannschaft × Kostenart**: Spalten nur für Kostenarten mit `Plan.HasValuesForType(AktType)` (16 mögliche `TMannschaftsKostenType`, Spaltennamen aus `cMannschaftsKostenTypeNamen`), plus „Gesamt". Zebra-Hintergrund. Zelle = `PaintKostenPaintWithBackColor` mit `Anzahl /AllCount (Kosten)`. Klickbar: Spaltenkopf (Typ-Gewichtung), Mannschaftsname + Gesamtzelle (Mannschafts-Gewichtung), jede Zelle (Detail-Gewichtung + Detailmeldungen). Summenzeile „Summe:" (Spaltensummen klickbar → Typ-Gewichtung).
- Darunter pro Mannschaft mit Meldungen: Name (groß) + alle `Kosten.Messages` (`tmNormalMessage`) als Textzeilen.
- `PaintKostenPaintWithBackColor(X,Y,Length,Fehler,AllCount,Kosten,GesamtKosten,Gewichtung,…)`:
  - Text: `Fehler /AllCount (Kosten)` bzw. `Fehler (Kosten)` bzw. nur `Kosten` (Fehler < 0); Kostenformat `MyFormatFloatShort` (T/Mio-Kürzel, locale-abhängig `%n`).
  - Hintergrund-Heatmap: `R = clamp(255 − trunc(255·Kosten/GesamtKosten))`; wenn `R < 240` → Rechteck in `RGB(255,R,R)` hinter dem Text (je höher der Kostenanteil, desto röter).
  - Gewichtungsindikator rechts (Schrift 11, rechtsbündig bei `X+Length+20`): `≤ −1000` → schwarzes „X" (ignoriert), `<0` → rot „−n", `>0` → grün „+n", 0 → nichts. Werte aus `cCalculateOptionsDisplayInteger = (-1000,-2,-1,0,1,2,3)` bzw. `Plan.GetMannschaftKostenDisplayInteger` (Summe Haupt-, Mannschafts- und Detailgewichtung).

**4. Terminplan (`PaintTermineMain`)** – je nach Toolbar-Button:
- `PaintSpielplan`: alle Spiele (`getCloneOfAllGames`, `SortByDate`, ohne `NotNecessary`), Leerabstand 10 bei KW-Wechsel. Eine Zeile je Spiel (`PaintOneTermin`).
- `PaintMannschaftsPlaene` / `…WithSisterTeams` (`PaintMannschaftsPlaeneIntern`): pro Mannschaft Überschrift + ihre Spiele; 30 px Abstand bei Rundenwechsel (`GetRoundNumberByDate`). Mit Nachbarn: unter jedem Spiel graue Zeilen mit Spielen von Nachbarmannschaften am selben Tag (`PaintOneTerminSisterGames`), blau bei „echter" Nachbarmannschaft.
- `PaintOneTermin` Spalten: Datum (x=0, `ddd dd.mm.yyyy`, „------" wenn nicht terminiert), Uhrzeit (100), Heim (150), „-" (400), Gast (420), **Meldung** (720) als Komma-Liste aus: „spielfreier Tag", „manuell festgelegter Termin" (`predefinedGames.findSameGame`), „Heimkoppelspiel/Doppelspieltag", „Auswärtskoppelspiel", „Ausweichtermin", „wegen Koppeltermin geänderte Uhrzeit" (`woKoppelSecondTime`/`woAuswaertsKoppelSecondTime`), „Spiellokal: …" (Wunschtermin-Location oder `predefinedGame.OverrideLocation`), „kein Wunschtermin", „doppeltes Spiel" (`gamesNotAllowed`), „ungültiger Termin" (`GameIsValid` false). Zeilenhöhe 20.

**5. Diagramme (`PaintVerteilung`)** – vertikal gestapelt, Abstand 40:
1. `PaintSpielPlanVerteilung` „Spieltage": Zeitachse; Überlappungs-Grauflächen (`PaintSpielPlanOverlap`); zwischen benachbarten Mannschaftszeilen Verbindungslinien des i-ten Spiels (abwechselnd gold/türkis → „Spieltag-Zickzack"); pro Spiel 2 px Strich rot (Heim) / schwarz (Auswärts). Legende.
2. `PaintSpielWechselHeimAuswaerts` „Wechsel Heim/Auswärts": pro Mannschaft Zickzack-Linie, Schritt 10 px, Heim = unten (+10), Auswärts = oben.
3. `PaintSpielAbstaende` „Spielverteilung": Zeitachse + Raster (Zeilenhöhe 80), pro Spiel eine „Bubble" (`PaintGameBubble`): Stiel, um −90° rotiertes Datum `dd.mm`, Kreis (normal) bzw. Quadrat (Koppeltermin), Füllung rosa (Heim) / grau (Auswärts). Hinweistext bei Koppelterminen.
4. `PaintSpielAbstandHinRueck` „Abstand Heimspiel zu Auswärtsspiel" (nur wenn nicht `rpHalfRound/rpFirstOnly/rpCorona`): pro Mannschaft und Gegner eine Linie zwischen Hin- und Rückspiel, Farbe von Grün (ideal) nach Rot (schlecht), je Gegner 5 px tiefer.
5. `PaintGamesPerWeek` „Anzahl Spiele pro Woche" – Tabelle Hinrunde (+ Rückrunde bei voller Runde): Spalten = Spieltag-Wochen (`getSpieltagMondays`), Kopf Nr. + `dd.mm - dd.mm`, Zellen „Spiele die Woche / Spiele gesamt", Fußzeile „maximale Differenz absolvierter Spiele" (rot bei ≥ 3). Spaltenbreite 70, Start x=300.
6. `PaintSpielRanking` „Setzliste" (nur wenn `Plan.HasRanking`): pro Mannschaft Balken je Spiel, Höhe `RankingDiff·5`, Farbe grün/gelb/orange/rot relativ zu einem absteigenden Zielwert; silberne Ziel-Linien; 50 px Lücke je Runde.

### Druck (`Print`)
`TFormPrintSelect` (Checkboxen: Terminwünsche, Nachbarmannschaften, Kosten, Spielplan, Mannschaftspläne, Mannschaftspläne mit Nachbarn, Diagramme; `ComboBoxFormat` Hochformat/Querformat) → `TSimplePrinter` (Font Tahoma −12, Zoom 0,45 Hochformat / 0,60 Querformat) → `BeginPrint(Plan.PlanName, Querformat)` → für jede gewählte Ansicht `Printer.Print(PaintXxx, NeedPage, Titel)` (neue Seite ab der 2. Ansicht) → `EndPrint`. Der Terminplan-Druck nutzt direkt `PaintSpielplan`/`PaintMannschaftsPlaene…` (unabhängig vom Toolbar-Zustand). Details zu Seitenumbruch siehe `PrintUtil.md`. Das Hauptfenster pausiert den Optimizer während des Drucks.

### Hilfsfunktionen (unit-lokal)
- `RemoveTeamNumber(teamName)`: entfernt ein angehängtes römisches Suffix `" I" … " XXX"` (`DecToRom(1..30)`) + TrimRight → Vereinsname.
- `MannschaftMatchVereinStr(AText, AValues)`: true, wenn der Vereinsname von `AText` mit dem eines Elements aus `AValues` übereinstimmt (Filter der Nachbarspiele in Mannschaftsplänen).
- `xPosByDate(Date, FirstDate) = 300 + Trunc((Date − FirstDate) · 4)` → **4 logische Pixel pro Tag**, Zeitachse beginnt bei x = 300 (links davon Mannschaftsname).
- `getXBySpieltageMondays(_, index) = 300 + index·70`.
- `PaintTableBackground` (Zebra: gerade Zeilen grau), `PaintDateRaster` (Zebra + senkrechte Wochenlinien je Zeile), `PaintDateLine` (KW-Achse).

## Fachliche Logik / Regeln
- **Termin-Auswertung je Mannschaft** (`PaintTerminMeldung`):
  - Vorrunde/Rückrunde: bei `rpHalfRound` alles Vorrunde, sonst `Date < Plan.DateBeginRueckrundeInXML` → Vorrunde.
  - „belegt" zählt nur, wenn `ParallelGames > 0` und `ParallelGames <= Hallenbelegung` und Tag nicht spielfrei.
  - Effektive Termine = Wünsche − belegt − spielfrei (parallele Spiele reduzieren **nicht**).
  - Mindestanzahl: `Min = (N−1)/2` (N = Anzahl Mannschaften), bei `DoubleRound` ×2, dann `Trunc(Min + 0.75) + 2`. Effektiv < Min → rot, sonst grün. Rückrunde gleiche Schwelle.
  - Rückrunden-Zeile + „nicht mögliche Termine" nur, wenn `RoundPlaning` nicht in `[rpHalfRound, rpFirstOnly, rpCorona]`.
  - Heimrecht-Anzeige: Rückrunde = `N − HomeRightCountRoundOne − 1`.
- **Spieltag-Überlappung** (`PaintSpielPlanOverlap`): Spieltag i = das i-te Spiel in `gamesRef` jeder Mannschaft; pro Spieltag Min/Max-Datum über alle Mannschaften. Spieltag i und i+Abstand überlappen, wenn `Trunc(Max_i) >= Trunc(Min_{i+Abstand})`. Grauwert `255 − min(128, Abstand·25)` (128 div 5 = 25) – größere Abstände (schlimmere Überlappung) dunkler, werden zuletzt gezeichnet.
- **Abstand Hin-/Rückspiel** (`PaintSpielAbstandMannschaft`): Soll-Abstand = `gameList.Count / Plan.Rounds.Count` (in Spielen, nicht Tagen); `Value = |Abstand − Soll| / Soll`, bei Abstand > Soll `Value /= 1.25`; `G = max(0, 255 − Trunc(Value·255))`, `R = 255 − G`, dann die größere Komponente auf 255 gesetzt → Grün…Gelb…Rot.
- **Spiele pro Woche**: Wochen = Montage mit Wunschterminen (`getSpieltagMondays`), Wochenende = nächster Montag − 1 s; Differenz max−min der kumulierten Spielanzahl über alle Mannschaften, rot ab **3**.
- **Setzliste** (`PaintSpielRankingMannschaft`): Zielwert startet je Runde bei N−1 und sinkt pro Spiel um 1; Farbe: grün, gelb wenn `Diff > Ziel`, orange `RGB(255,153,102)` wenn `> 2·Ziel`, rot wenn `> 5·Ziel`.
- **Kosten-Heatmap**: Anteil an Gesamtkosten, siehe oben; Gewichtungsanzeige über `cCalculateOptionsDisplayInteger`.
- **Nachbarmannschaft „echt"**: `SisterGame.PreventParallelGame` bzw. bei Mannschaften im Plan `|teamNumber − SisterMannschaft.teamNumber| = 1` → blau.
- Spielfreie Tage: `Plan.FreeGameDays.ContainsKey(Trunc(Date))`.

## Daten & Persistenz
Keine Dateien. Liest ausschließlich aus der lokalen `TPlan`-Kopie (`Assign` des aktuell angezeigten Plans). Schreibzugriffe nur über die Gewichtungsdialoge auf `getMainPlan().getOptions` (Gewichtungen), Persistenz erfolgt über `DoAfterMainPlanChanged(True)` im Hauptfenster.

## Threading / Performance
- Läuft vollständig im UI-Thread. Während der Optimierung ruft das Hauptfenster per Timer `SetPlan(GetCurrentViewPlan)` auf, sobald sich die Kosten ändern oder spätestens alle 10 s → `TPlan.Assign` + `CalculateKosten(-1)` + Neuzeichnen aller 5 PaintBoxen (Invalidate; tatsächlich gezeichnet werden nur sichtbare).
- **Hotspots beim Zeichnen**: `PaintKosten` berechnet bei **jedem Paint** (auch bei jedem Scrollen!) `Plan.CalculateKosten(-1)` und für jede Mannschaft **zweimal** `Mannschaft.CalculateKosten(..., tmNormalMessage)` (Tabelle + Meldungen) sowie mehrfach `CalculateFehlterminKosten`/`CalculateNotAllowedKosten`/`CalculateFreeGameDateKosten`. `PaintTerminMeldung` ruft je Wunschtermin `GetHallenBelegung` und `GetParallelGames`. `PaintSpielplan` klont alle Spiele (`getCloneOfAllGames`) je Paint. `PaintOneTermin` macht mehrere lineare `findSameGame`-Suchen pro Spiel.
- Es wird immer der gesamte Inhalt gezeichnet (keine Virtualisierung); bei großen Ligen ist die Fläche einige tausend Pixel hoch.
- Setzen von `PaintBox.Height/Width` innerhalb von `OnPaint` löst ggf. erneutes Paint aus.

## Plattformabhängigkeiten
- VCL-Formular/Controls: `TPageControl`, `TTabSheet`, `TScrollBox`, `TPaintBox`, `TToolBar`/`TToolButton` (Grouped/Down), `TImageList` (DFM-Bitmapblob), `TPanel`-Reparenting (`PanelMain.Parent := ParentPanel`).
- GDI-Canvas-Zeichnen über `TZoomCanvas` (PrintUtil): `FillRect`, `MoveTo/LineTo`, `Ellipse`, `Rectangle`, `TextOut` mit `SetBkMode(TRANSPARENT)`, rotierter Text via `Font.Orientation` (nur mit TrueType-Font unter GDI), `TextExtent`.
- `Windows.RGB`, `TColor`/`clXxx`, `PtInRect`, `crHandPoint`, `Screen.PixelsPerInch`.
- VCL-Drucksystem (`Printers`, `TPrintDialog`) indirekt über `PrintUtil`.
- `FixControls(Self)` (PlanUtils) – VCL-Hilfsfunktion.
- Modale VCL-Dialoge (`ShowModal`, `mrOK`).
- Datumsformatierung locale-abhängig (`DateTimeToString('ddd dd.mm.yyyy')` → deutsche Wochentagskürzel nur bei deutscher Locale), `Format('%n')` Tausendertrennzeichen.

## Migrationshinweise für C#
- **Ziel**: `AndiGenerator.UI` (Avalonia UI empfohlen – läuft auf Windows/macOS/Linux, experimentell Android/iOS; Rendering über Skia). Aufteilung:
  - `PlanViewModel` (Snapshot des Plans + vorberechnete Anzeigedaten) in `AndiGenerator.UI.ViewModels` bzw. ein UI-unabhängiges `AndiGenerator.Reports`-Modul, das die **Anzeigedaten** erzeugt (Listen von Zeilen/Meldungen/Kostenzellen), getrennt vom Rendering.
  - `PlanView` mit `TabControl` (5 Tabs).
- **Rendering-Strategie** (Empfehlung, pro Tab):
  - *Terminplan / Mannschaftspläne*: **virtualisiertes DataGrid** (Avalonia `DataGrid` oder `TreeDataGrid`) mit Spalten Datum, Zeit, Heim, Gast, Meldung; Gruppierung nach KW bzw. Mannschaft/Runde; Nachbarspiele als Unterzeilen (grau/blau). Vorteil: Sortieren, Kopieren, Suchen gratis.
  - *Kosten*: `DataGrid`/`Grid` mit dynamischen Spalten (nur `HasValuesForType`), Zellen-Template mit Heatmap-Hintergrund und Gewichtungs-Badge; **Klick/Doppelklick auf Zelle** → Gewichtungsdialog (Command im ViewModel statt ActionRect-Hit-Testing). Meldungsliste als `ItemsControl` mit Expandern pro Mannschaft.
  - *Terminwünsche / Nachbarmannschaften*: Textlisten → `ItemsControl`/`ListBox` mit Virtualisierung und farbigen `TextBlock`s; die Zeitachsen-Grafik oben als Custom-Control.
  - *Diagramme*: **Custom-Control mit eigenem Rendering** (`Control.Render(DrawingContext)` in Avalonia bzw. direkt SkiaSharp `SKCanvas`), das nur den sichtbaren Bereich zeichnet (Scroll-Offset berücksichtigen, `ScrollViewer` + `ILogicalScrollable` oder feste `Measure`-Größe). Ein gemeinsamer `IPlanRenderer`-Abstraktionslayer (Zeichenprimitive wie `TZoomCanvas`: Line, FillRect, Ellipse, Rect, Text, rotierter Text, MeasureText) erlaubt, denselben Code für **Bildschirm, PNG und PDF** zu nutzen.
- **Druck → PDF-Export**: Plattformübergreifendes Drucken ist in .NET schwach; empfohlen: Export als PDF (z. B. **QuestPDF** für Tabellen/Text-Seiten mit automatischem Seitenumbruch, Kopfzeile, Hoch/Querformat; Diagramme via SkiaSharp als Vektor-Canvas in QuestPDF einbetten oder `SKDocument.CreatePdf`). Anschließend PDF mit System-Viewer öffnen/drucken. Die Druckauswahl (7 Checkboxen + Format) bleibt als Dialog.
- **Zoom**: `LayoutTransform`/`ScaleTransform` auf den Inhalt oder Zoomfaktor im Renderer; Zoomstufen 25–200 % (Hauptfenster-ComboBox).
- **Mapping**:
  - `TColor`/`RGB()` → `Color.FromRgb` (Achtung: `TColor` ist BGR-codiert – bei direkter Übernahme von Integerwerten vertauschen; hier werden aber nur `RGB(r,g,b)`-Aufrufe und `clXxx` verwendet).
  - `TList<TActionRect>` + Hit-Test → entfällt bei Controls; im Custom-Renderer: Liste von `(Rect, ICommand)`.
  - `TDateTime` → `DateTime`; `Trunc(Date)` → `.Date`; `WeekOf` → `ISOWeek.GetWeekOfYear`; `DayOfWeek(x)=2` (Delphi: 1=Sonntag, 2=Montag) → `DayOfWeek.Monday`.
  - `MyFormatFloatShort`/`MyFormatDate` → zentrale Formatierungsklasse mit fester `CultureInfo("de-DE")`.
  - `SetPlan` → ViewModel erhält immutable Snapshot vom Optimizer (z. B. via `Channel`/Event, Dispatcher.UIThread.Post), **Kosten/Meldungen einmal pro Snapshot berechnen** (Hintergrund-Task), nicht beim Rendern.
- **Fallstricke**:
  - Delphi `DayOfWeek` ist 1-basiert ab Sonntag (≠ .NET).
  - `TDateTime`-Arithmetik (`Date − FirstDate` in Tagen als Double, `EncodeTime(0,0,1,0)` = 1 s).
  - `MannschaftMatchVereinStr`/`RemoveTeamNumber` nutzt römische Ziffern bis 30 – 1:1 übernehmen.
  - Zirkuläre Abhängigkeit zu `AndiGeneratorMain` (globale Funktionen) → im C# durch Services/Messenger (`IPlanService.MainPlan`, `CurrentViewedPlan`, `OnMainPlanChanged`) ersetzen.
  - Gewichtungsänderung schreibt in den **Main-Plan**, Detailmeldungen kommen aber vom **angezeigten Plan** – bewusst so beibehalten.
- **Aufwandsschätzung: L** – viele fachliche Anzeigeregeln und 6 individuelle Diagramme, die neu gerendert werden müssen; die Datenlogik ist aber überschaubar und gut isolierbar, Druck wird durch PDF-Export ersetzt.
- **Bugs/Auffälligkeiten im Original**:
  - Z. 1784 und Z. 1816: `width := …` in `PaintSpielRanking` / `PaintSpielWechselHeimAuswaerts` – es gibt keine lokale Variable `width`, daher wird **`TFormPlanPanel.Width`** (die Breite des unsichtbaren Formulars) beim Zeichnen gesetzt. Seiteneffekt harmlos, aber fehlerhaft; in C# lokale Variable verwenden.
  - Z. 2586–2587: `PaintBox.Height/Width` werden im `OnPaint` gesetzt → mögliche Doppel-Paints/Flackern.
  - Z. 2599–2605: `PaintBoxMouseUp` iteriert `ActionRects` per Enumerator und ruft `DoAction` (modaler Dialog → Message-Loop → Repaint → `ClearActionRects` verändert die Liste während der Iteration). Potenzielle „Collection modified"-Situation / Zugriff auf gelöschte Einträge.
  - Z. 877 u. 1013/1096: Kosten werden bei jedem Paint neu berechnet (doppelt pro Mannschaft) – Performance, kein Funktionsfehler.
  - Z. 2931–3038: Vorrunden-Auswertung existiert doppelt; der `else if RoundPlaning <> rpSecondOnly`-Zweig wird nur bei `rpCorona` erreicht und ist dann immer wahr → beide Zweige sind inhaltlich identisch. Bei `rpSecondOnly` wird trotzdem eine „Vorrunde"-Zeile angezeigt. Klären, ob gewollt.
  - `PaintSpielPlanOverlap` und `PaintSpielRankingMannschaft` setzen voraus, dass `gamesRef` nach Datum sortiert ist (Index i = i-ter Spieltag; `getRankingDiffs` iteriert `gamesRef`, die Anzeige iteriert die sortierte Kopie) – bei der Migration sicherstellen.
  - `xPosByDate` bezieht sich auf `Plan.GetFirstDate()` = frühester **Wunschtermin**, nicht frühestes Spiel; Spiele/Sperrtermine davor ergeben x < 300 (ragen in die Namensspalte).
  - `PaintVerteilungMannschaft` Overlay: bei nicht terminierten Spielen wird ein Offset übersprungen, aber die Schleife läuft bis `gameList.Count − 1` mit Bedingung – korrekt, nur unübersichtlich.
  - Hinweistext Z. 1947 „rot=Heimspiel schwarz=Auswärtsspiel", tatsächliche Bubble-Farben sind rosa `RGB(255,128,128)` und grau `RGB(128,128,128)`.
  - Tippfehler in UI-Texten: „letzer Spieltag", „Nachbarmanschaften" (Drucktitel Z. 1293).

## Offene Fragen
- Soll die Anzeige weiterhin rein lesend bleiben, oder ist in der C#-Version eine **manuelle Terminänderung** direkt im Plan (Drag&Drop in einer Zeitachse / Kontextmenü „Termin festlegen") gewünscht? Im Original geschieht das nicht hier (predefinedGames werden in anderen Dialogen gepflegt).
- Reicht **PDF-Export** statt direktem Drucken? Soll zusätzlich Export als CSV/Excel der Spielpläne erfolgen?
- Sollen die Diagramme 1:1 (Pixel-Layout, 4 px/Tag) nachgebaut oder modernisiert werden (Tooltips, Hover, interaktive Zeitachse)?
- Doppelter Vorrunden-Zweig bei `rpCorona`/`rpSecondOnly` (s. o.) – gewollt?
- Aktualisierungsfrequenz während der Optimierung (derzeit bei Kostenänderung bzw. alle 10 s) beibehalten?
- Mobile Plattformen: die breiten Tabellen (Kosten bis ~2000 px) sind auf Smartphones kaum nutzbar – eigenes Layout nötig?
