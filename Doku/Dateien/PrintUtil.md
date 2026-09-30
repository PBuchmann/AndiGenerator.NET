# PrintUtil (PrintUtil.pas)

**Kategorie:** Druck (plus Zeichen-Hilfsschicht für die Bildschirmanzeige)
**Umfang:** 527 Zeilen .pas / keine .dfm
**Abhängigkeiten (uses):** keine projekt-eigenen Units; RTL/VCL/Win: `Windows` (`GetDeviceCaps`, `SetMapMode`, `SetViewportOrgEx`, `GetBkMode`/`SetBkMode`, `MAXLONG`), `Graphics` (`TCanvas`, `TFont`, `TColor`), `Printers` (`Printer`), `Dialogs` (`TPrintDialog`), `Forms` (`Application`, `Screen.PixelsPerInch`), `System.Generics.Collections`, `System.UITypes`, `Classes`, `SysUtils`, `Math`, `Controls`.
**Verwendet von:** `PlanPanel` (alle Zeichenfunktionen, ActionRects, Druck); `AndiGeneratorMain` hat die Unit in `uses`, verwendet aber keine ihrer Typen (per grep geprüft).

## Zweck
Stellt eine **zoom- und DPI-unabhängige Zeichenschicht** (`TZoomCanvas`) über einem VCL-`TCanvas` bereit, sodass derselbe Zeichencode (in `PlanPanel`) sowohl auf dem Bildschirm (`TPaintBox`) als auch auf dem **Drucker** ausgegeben werden kann. Alle Aufrufer zeichnen in logischen Koordinaten (Basis 96 dpi); `TZoomCanvas` rechnet auf Geräte-Pixel um, verwaltet einen vertikalen Seiten-Offset und blendet Elemente außerhalb der aktuellen Seite aus. `TSimplePrinter` kapselt den Druckvorgang (Druckerauswahl, Orientierung, Ränder, Kopfzeile, einfacher vertikaler Seitenumbruch). `TActionRect` modelliert klickbare Bereiche auf dem Bildschirm.

## Inhalt / Struktur

### `TActionRect` (Interface) / `TActionRectBase` (TInterfacedObject, abstrakt)
- `DoAction(ScreenPosition: TPoint)` – abstrakt, in `PlanPanel` implementiert (Gewichtungsdialoge).
- `getRect/setRect(x1, y1, width, height)` – Rechteck (Left/Top/Right/Bottom).
- `getPaintBoxOwner/setPaintBoxOwner` – welcher PaintBox der Bereich gehört.
- Referenzgezählt (Interface), wird in `TList<TActionRect>` gehalten → automatische Freigabe.

### `TZoomCanvas`
- Felder: `FCanvas` (Ziel-`TCanvas`), `FDpiX/FDpiY`, `FZoom`, `FMinYVisible/FMaxYVisible` (sichtbarer logischer Y-Bereich = aktuelle Seite), `Offset` (logischer Y-Offset der aktuellen Seite), `FFontSize` (Default −12), `FFontRotation` (Grad), `FFontBold`, public `ActionRects: TList<TActionRect>`.
- Konstruktoren: `Create(Canvas, DpiX, DpiY, Zoom)` (Drucker) und `Create(Canvas, Zoom)` → DPI = `Screen.PixelsPerInch` (Bildschirm). `FMaxYVisible = MAXLONG` (auf dem Schirm alles sichtbar).
- Koordinatenumrechnung:
  - `ConvertXCoord(v) = Round(v · DpiX / 96 · Zoom)`
  - `ConvertYCoord(v) = Round((v − Offset) · DpiY / 96 · Zoom)` (über `ZoomYCoord`)
  - `DeConvertXCoord(v) = Round(v / DpiX · 96 / Zoom)`; `DeConvertYCoord(v) = UnZoomY(v) + Offset`.
- Zeichenprimitive (alle mit Sichtbarkeitsprüfung `IsVisible(y)`: `FMinYVisible ≤ y ≤ FMaxYVisible`, geprüft wird **nur ein Y-Wert**):
  - `FillRect(Rect)` – prüft `Rect.Top`.
  - `TextOut(x, y, S, ignoreVisibilityCheck=false)` – Hintergrund transparent (`SetBkMode TRANSPARENT`), Schrifthöhe `ZoomYCoord(FontHeight)`, fett optional, Rotation über `Font.Orientation := −FontRotation·10` (Zehntelgrad).
  - `MoveTo(x, y)` (immer), `LineTo(x, y, ignore…)` – bei unsichtbarem Endpunkt nur `MoveTo` (Linie wird weggelassen).
  - `DrawArc(x, y, rad)` → `Ellipse` (gefüllt mit Brush, Rand Pen), `DrawQuadrat(x, y, rad)` → `Rectangle`.
  - `TextExtent(S)` – misst mit gezoomter Schrift und rechnet zurück in logische Einheiten.
  - `AddActionRect` – sammelt klickbare Bereiche (logische Koordinaten; `PlanPanel.AddActionRects` rechnet sie danach um).
- Properties: `BrushColor`, `PenColor`, `FontColor` (direkt auf `FCanvas`), `FontHeight`, `FontBold`, `FontRotation` (nur intern gemerkt, wirken erst bei `TextOut`/`TextExtent`).

### `TPrintFunction = function(Canvas: TZoomCanvas; ForPrint: boolean): TSize of object`
Signatur der Zeichenfunktionen (`PlanPanel.PaintKosten` usw.); Rückgabe = logische Gesamtgröße des Inhalts.

### `TSimplePrinter` (TComponent)
- Felder: `FFont` (TFont), `FDpiX/FDpiY`, `MaxX/MaxY` (bedruckbare Fläche in Geräte-Pixel), `OffsetX/OffsetY` (physischer, nicht bedruckbarer Rand), `RandX`, `RandTop`, `RandBottom`, `FZoom` (Default 1.0), `Canvas: TZoomCanvas`.
- `BeginPrint(Titel, LandScape): boolean`:
  1. `TPrintDialog.Execute` (Druckerauswahl); Abbruch → false.
  2. `Printer.Title := Titel`, `Printer.Orientation := poLandscape/poPortrait` (überschreibt die Wahl im Druckdialog).
  3. `Printer.BeginDoc`; DPI per `GetDeviceCaps(LOGPIXELSX/Y)`, `PHYSICALOFFSETX/Y`.
  4. Druckerfont = `Font.Name`/`Font.Height`.
  5. `TZoomCanvas.Create(Printer.Canvas, DpiX, DpiY, Zoom)`.
  6. Ränder (logisch, 96-dpi-Basis, gezoomt): `RandX = Convert(50)`, `RandTop = Convert(100)`, `RandBottom = Convert(50)`.
  7. `MaxX = PHYSICALWIDTH − 2·RandX`, `MaxY = PHYSICALHEIGHT − (RandTop + RandBottom)`.
  8. `SetMapMode(MM_TEXT)`, `SetViewportOrgEx(−OffsetX + RandX, −OffsetY + RandTop)` → Ursprung auf die linke obere Ecke des Inhaltsbereichs.
- `NewPage()` – `Printer.NewPage` + Viewport-Ursprung erneut setzen.
- `EndPrint()` – Canvas freigeben, `Printer.EndDoc`.
- `PrintHeader(Title)` (privat) – Titel mit Schrifthöhe 30 bei `y = Offset − 60` (also im oberen Rand) und waagerechte Linie bei `Offset − 20` über die volle Breite; ignoriert Sichtbarkeitsprüfung.
- `print(PrintFunction, NewPageAtStart, Title)` – **Seitenumbruch-Algorithmus**:
  ```
  MinYVisible := 0; Offset := 0; MaxSize := DeConvertY(MaxY)  (logische Seitenhöhe)
  MaxYVisible := MaxSize
  if NewPageAtStart then NewPage
  loop:
     PrintHeader(Title)
     Size := PrintFunction(Canvas, True)          // zeichnet den GESAMTEN Inhalt,
                                                  // TZoomCanvas lässt nur Elemente mit y in [Min,Max] durch
     if Size.cy < MaxYVisible then break
     NewPage; Offset += MaxSize; Min := Max; Max += MaxSize
  ```
  → Für jede Seite wird die komplette Zeichenfunktion erneut ausgeführt (O(Seiten × Inhalt)). Nur vertikaler Umbruch; zu breite Inhalte werden rechts abgeschnitten (Zoom 0,45/0,60 wird in `PlanPanel` so gewählt, dass die Breite meist passt).

## Fachliche Logik / Regeln
Keine fachlichen Spielplanregeln. Technische Regeln:
- Logisches Koordinatensystem = 96 dpi; Zoom multiplikativ.
- Seitenränder: links/rechts 50, oben 100 (Platz für Kopfzeile), unten 50 logische Einheiten (× Zoom).
- Ein Element gehört zu der Seite, auf der seine (Ober-)Kante bzw. sein Endpunkt liegt; es wird nicht geteilt.

## Daten & Persistenz
Keine. Kein Speichern von Druckeinstellungen.

## Threading / Performance
Keine Nebenläufigkeit (UI-Thread). `TextOut`/`TextExtent` setzen bei jedem Aufruf Font-Höhe/-Stil neu (GDI-Font-Neuerzeugung) – bei vielen Textausgaben spürbar, aber unkritisch. Druck: gesamter Inhalt wird pro Seite neu „gezeichnet“ (inkl. evtl. teurer Kostenberechnungen in `PlanPanel.PaintKosten`).

## Plattformabhängigkeiten
- Vollständig Windows/VCL-gebunden: `Printers.Printer` (globales VCL-Druckerobjekt), `TPrintDialog`, GDI-Funktionen `GetDeviceCaps` (LOGPIXELSX/Y, PHYSICALOFFSETX/Y, PHYSICALWIDTH/HEIGHT), `SetMapMode(MM_TEXT)`, `SetViewportOrgEx`, `Get/SetBkMode(TRANSPARENT)`.
- `TFont.Orientation` (rotierter Text – GDI, nur TrueType).
- `Screen.PixelsPerInch` für Bildschirm-DPI.
- `TColor` (BGR-Integer).

## Migrationshinweise für C#
- **Ziel**: `AndiGenerator.UI.Rendering` (plattformneutral).
  - `TZoomCanvas` → Interface `IPlanCanvas` (DrawLine, FillRect, DrawEllipse, DrawRect, DrawText(rotation), MeasureText, Brush/Pen/Font-Zustand) mit Implementierungen:
    - `SkiaPlanCanvas` (SkiaSharp `SKCanvas`) – funktioniert für Bildschirm (Avalonia `ICustomDrawOperation`/`SKCanvas`), PNG und **PDF** (`SKDocument.CreatePdf`, mehrseitig, Vektorgrafik, eingebettete Fonts).
    - optional `AvaloniaPlanCanvas` (`DrawingContext`).
  - Zoom/DPI: in Skia per `canvas.Scale(zoom)` bzw. Avalonia `RenderScaling` statt manueller Umrechnung jeder Koordinate; `ConvertX/Y` entfallen weitgehend. Logische Einheit 1/96 Zoll beibehalten (Avalonia DIP = 1/96 Zoll → passt 1:1).
  - Seiten-Offset/Sichtbarkeitsprüfung: in Skia per `canvas.Translate(0, −pageOffset)` + `canvas.ClipRect` → Elemente an der Seitengrenze werden sauber abgeschnitten statt ganz weggelassen bzw. überstehend gezeichnet. Besser noch: Layout einmal berechnen (Liste von Zeilen/Blöcken mit Höhe) und **blockweise umbrechen** (keine Zeile über die Seitengrenze teilen) – z. B. mit **QuestPDF** für die textlastigen Ansichten (Spielplan, Mannschaftspläne, Terminwünsche, Kosten), das Kopfzeile, Ränder, Hoch/Querformat und Seitenumbruch nativ unterstützt; Diagramme als Skia-Canvas-Element darin.
  - `TSimplePrinter` → `PdfExportService` (Parameter: Titel, Querformat, Liste der Abschnitte) + „Öffnen mit Standardprogramm“ (`Process.Start` mit `UseShellExecute=true` / `xdg-open` / `open`). Direkt-Druck plattformübergreifend nur über das PDF (Windows optional später via `System.Drawing.Printing`, nur Windows).
  - `TActionRect` → `record HitRegion(Rect Bounds, ICommand Command)`; bei Controls (DataGrid) entfällt es komplett.
  - `TPrintFunction` → `Func<IPlanCanvas, bool, Size>` bzw. Interface `IPrintableSection { string Title; Size Render(IPlanCanvas c, bool forPrint); }`.
- **Mapping**: `TColor`/`RGB()` → `SKColor`/`Color`; `TSize` → `Size`/`SKSize`; `TRect` (Right/Bottom exklusiv in GDI) → `SKRect`/`Rect` (Left, Top, Width, Height); `TList<TActionRect>` → `List<HitRegion>`; `Font.Height` negativ (= Zeichenhöhe ohne interne Führung) → Skia `TextSize` ≈ |Height| (Pixel), ggf. empirisch angleichen (Tahoma ist nicht auf allen Plattformen verfügbar → Font mitliefern oder z. B. „Inter"/„DejaVu Sans" als Fallback).
- **Fallstricke**:
  - GDI-`FillRect`/`Rectangle`/`Ellipse` sind rechts/unten exklusiv; Skia inklusiv/float → 1-px-Abweichungen.
  - `Font.Orientation` dreht gegen den Uhrzeigersinn in Zehntelgrad; `FontRotation = −90` ergibt `Orientation = 900` → Text läuft von unten nach oben. In Skia: `canvas.RotateDegrees(−90)`.
  - `FontHeight` positiv (30 in `PrintHeader`, 11 in `PaintKostenPaintWithBackColor`) bedeutet in GDI Zellenhöhe inkl. interner Führung, negativ = Zeichenhöhe → leicht unterschiedliche Größen.
  - Transparenter Texthintergrund (`TRANSPARENT`) ist in Skia Standard.
- **Aufwandsschätzung: M** – die Klasse ist klein, aber die Zeichenabstraktion wird zur Basis aller Diagramme/Exports und muss für Bildschirm und PDF sauber funktionieren (Fonts, Textmessung, Seitenumbruch).
- **Bugs/Auffälligkeiten im Original**:
  - Z. 386: `TextExtent` rechnet `Result.cy` mit `UnZoomXCoord` statt `UnZoomYCoord` zurück – nur bei `DpiX ≠ DpiY` falsch.
  - Z. 166–167: `Font.Height := Round(Font.Height * 7)` wird sofort überschrieben – toter Code.
  - Z. 312 ff.: Sichtbarkeit wird nur an einer Y-Koordinate geprüft: hohe `FillRect`s (z. B. Überlappungsflächen, Zebra-Tabellen) und Texte an der Seitenunterkante werden über den unteren Rand hinaus gezeichnet (keine Clip-Region gesetzt); Elemente, deren Top auf der Vorseite liegt, fehlen auf der Folgeseite.
  - Z. 318–334: `LineTo` prüft nur den Endpunkt; Linien, die eine Seitengrenze kreuzen, werden auf einer Seite komplett gezeichnet (über den Rand) oder ganz weggelassen.
  - Z. 244: Abbruchbedingung `Size.cy < FMaxYVisible` – bei `cy` exakt gleich der Seitengrenze wird eine leere Zusatzseite erzeugt (nur Kopfzeile).
  - Z. 145–157: Im `TPrintDialog` gewählte Orientierung/Kopien/Seitenbereich werden ignoriert bzw. überschrieben.
  - Kein horizontaler Umbruch – zu breite Inhalte werden abgeschnitten.
  - `TZoomCanvas.ActionRects` wird beim Druck befüllt, aber nie genutzt (harmlos).

## Offene Fragen
- Genügt PDF-Export (mit „Öffnen/Drucken“ über das System) als Ersatz für den direkten Druck?
- Sollen Kopfzeilen zusätzlich Datum/Seitenzahl/Planname enthalten (derzeit nur Abschnittstitel; Planname nur als Druckjob-Titel)?
- Soll Querformat-Zoom (0,60) / Hochformat-Zoom (0,45) beibehalten werden, oder automatische Einpassung auf Seitenbreite?
