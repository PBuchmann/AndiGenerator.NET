# PlanDataObjects (PlanDataObjects.pas)

**Kategorie:** Persistenz/IO (generisches Datenmodell)
**Umfang:** 2327 Zeilen .pas / keine .dfm
**Abhängigkeiten (uses):** Projekt: `PlanUtils` (`FixDateTime`). RTL: `System.Generics.Collections`, `System.Generics.Defaults`, `Xml.XMLDoc`/`Xml.XMLIntf` (MSXML-DOM über `LoadXMLDocument`/`TXMLDocument`), `System.TypInfo` (Enum↔String), `Dialogs` (`ShowMessage`), `Forms`, `Windows`.
**Verwendet von:** `PlanTypes`, `AndiGeneratorMain` sowie praktisch alle Dialog-/Panel-Units (`DialogEditOne*`, `DialogEditSisterTeam`, `DialogEditTeamName`, `DialogFirstStart`, `DialogForMultiplePanel`, `DialogForSinglePanel`, `DialogPanel*`, `DialogTeamPanel`).

## Zweck
Die Unit enthält ein **generisches, schemaloses Baum-Datenmodell** (`TDataObject`: Name + Attribut-Dictionary + Kinder + Status) für alle Eingangsdaten einer Staffel. Dazu kommen der **Import der click-TT-XML** (Root `TT`), das **eigene Speicherformat** (`<andigenerator><plan …>`) und eine **Diff/Merge-Logik**: Aus Basisdaten (click-TT) und Benutzerdaten entsteht eine Delta-Datei (`.modifications`), die beim nächsten Laden wieder auf die frischen Basisdaten angewendet wird. Außerdem gibt es `THomeDay` mit einem kompakten Textformat für Heimspieltermine, das in Grid-Editoren verwendet wird.

## Inhalt / Struktur

### Konstanten: Knoten- (`n…`) und Attributnamen (`a…`)
| Konstante | Wert | Konstante | Wert |
|---|---|---|---|
| `aState` | `state` | `nPlan` | `plan` |
| `aGender` | `gender` | `aMid` | `mid` |
| `aFrom` | `from` | `aUntil` | `until` |
| `aName` | `name` | `aID` | `id` |
| `nTeam` | `team` | `aClubID` | `clubid` |
| `aTeamId` | `teamid` | `aTeamNumber` | `teamnumber` |
| `aTeamName` | `teamname` | `aOrgTeamName` | `orgteamname` |
| `aLocation` | `location` | `aHomeRights` | `homerights` (Attribut am team) |
| `nHomeGameDay` | `homegameday` | `aDateTime` | `datetime` |
| `aParallelGames` | `parallelgames` | `aAusweichTermin` | `ausweichtermin` |
| `aCoupleGameDay` | `couplegameday` | `aDoubleGameDay` | `doublegameday` |
| `aCoupleAuswaertsSecondTime` | `coupleauswaertssecondtime` | `aCouplePrio` | `coupleprio` |
| `aCoupleSecondTime` | `couplesecondtime` | `nNoGameDay` | `nogameday` |
| `aDate` | `date` | `nRoadCouple` | `roadcouple` |
| `aTeamNameA` / `aTeamNameB` | `teamnamea` / `teamnameb` | `aSameDay` | `sameday` |
| `nNoWeekGames` | `noweekgames` | `nSisterTeam` | `sisterteam` |
| `aNoParallelGames` | `noparallelgames` | `aParallelHomeGames` | `parallelhomegames` |
| `nSisterGame` | `sistergame` | `aHomeTeamName` / `aGuestTeamName` | `hometeamname` / `guestteamname` |
| `nPredefinedGames` | `predefinedgames` | `nGame` | `game` |
| `nExistingSchedule` | `existingschedule` | `nSchedule` | `schedule` |
| `nMandatoryGames` | `mandatorygames` | `aDateFrom`/`aDateTo` | `datefrom`/`dateto` |
| `aNumberGames` | `numbergames` | `nRanking` | `ranking` |
| `aActive` | `active` | `aRankingIndex` | `rankingindex` |
| `nHomeRights` | `homerights` (Kindknoten!) | `aHomeRight` | `homeright` |

Hinweis: `homerights` ist gleichzeitig ein Attributname am `team` (`aHomeRights`, Standard −1) und ein Kindknotenname (`nHomeRights`). `aLocationInt` wird nur in auskommentiertem Code referenziert und existiert nicht.

### Typen
- `TDataObjectState = (dosNormal, dosNew, dosModified, dosDelete)`: Status für Diff/Merge. Wird als Attribut `state="dosNew"` usw. gespeichert (per RTTI-Enumname, nur wenn ≠ dosNormal).
- `TDataObjectKey`: Name + Liste (Attributname, Wert). Das ist der fachliche Schlüssel eines Knotens.
- `TDataObject`:
  - Felder: `State`, `Name`, `attributes: TDictionary<String,String>` (**ungeordnet**), `childs: TObjectList<TDataObject>` (besitzend, geordnet), `parent`.
  - Konstruktor `Create(Name, parent)`: **wirft eine Exception, wenn für `Name` kein Schlüssel definiert ist** (`getKeysForDataType`). Hängt sich selbst an `parent.childs` an.
  - Getter/Setter: `SetAsString` (trimmt immer!), `SetAsDate` (`dd.mm.yyyy`), `SetAsDateTime` (`dd.mm.yyyy hh:mm`), `SetAsTime` (`hh:mm`), `SetAsBool` (`true`/`false`), `SetAsInt`. Entsprechende `GetAs…`: `GetAsBool` = (Wert = `'true'`), `GetAsInt` = `StrToIntDef(…,0)`, `GetAsDate`/`GetAsDateTime`/`GetAsTime` liefern 0 bei leerem Wert. `GetAsDateTime` parst Datum aus Zeichen 1–10 und Zeit aus Zeichen 12–16.
  - Navigation: `NamedChilds(name)` (Interface-Liste für `for…in`), `FindChild` (erstes Kind mit Namen), `getOrCreateChild`, `deleteChilds(name)`, `RemoveChild`.
  - Schlüssel: `getKey(DataKey)`, `findChildIndexByKey(DataKey)`. Wirft „Doppelter Schlüssel in Datei: <name>“, wenn zwei Kinder denselben Schlüssel haben.
  - `assign` (tiefe Kopie, **State wird nicht kopiert**), `assignAttributes`, `Clear`.
  - `Merge(Source)`, `Diff(After, Before)`: siehe unten.
  - `isTheSame`/`isTheSameOneDirection` (rekursiver Vergleich in beide Richtungen über Schlüssel), Klassenmethoden `NodesAreTheSame`, `ChildNodesAreTheSame(Node1, Node2, ChildNodeName)`. Werden von Dialogen genutzt, um „geändert gegenüber click-TT“ anzuzeigen.
  - `Save(parentElem: IXMLNode)` / `Load(Elem: IXMLNode)`: rekursive 1:1-Abbildung (Knotenname = `Name`, alle Attribute, `state` separat).
- `THomeDay`: ein Tag in der Terminmeldung einer Mannschaft (Heimtermin **oder** Sperrtermin **oder** leer). Felder `NoDate, Date, KoppelDate, DoubleDate, KoppelPrio, KoppelAuswaertsSecondTime, KoppelSecondTime, MaxParallelGame, SperrTermin, AusweichTermin, Location`. Methoden `AsShortString`, `FromShortString`, `IsHomeDay`, `isTheSame`, `assign`.
- `THomeDayList = TObjectList<THomeDay>` mit Comparer nach `Date`.
- `TPlanData`: Wrapper um den Root-Knoten `plan`. Methoden: `Clear`, `assign`, `LoadFromClickTTFile`, `SaveToXML`, `LoadFromXML`, `Merge`, `Diff`, `root()`, Helfer `getTeamNames` (sortiert), `getAllPossibleDates` (alle Tage mit Heimspielterminen, sortiert, eindeutig), `getTeamLocationsInt` (fest `'', '1'..'5'`), `isValidLocation`, `FixLocation` (ungültig → `''`), `GetHomeDays`/`SetHomeDays(TeamName, THomeDayList)`.
- Freie Funktionen: `DateFromString` (`dd.mm.yyyy`, positionsbasiert), `TimeFromString` (`hh:mm`, 0 bei Fehler), `IsClickTTFile` (Root-Element `TT`), `DateToString`, `TimeToString`, `FormatTimeForExport` (`hh:mm`), `GetGenderList` (Herren, Damen, Jungen, Jungen U18, Mädchen, Mädchen U18, Schüler, Schülerinnen, Bambini, Senioren 40/50/60/70, Seniorinnen 40/50/60/70).

### Schlüsseldefinition (`getKeysForDataType`)
| Knoten | Schlüsselattribute |
|---|---|
| `plan` | – (Singleton) |
| `team` | `teamname` |
| `homegameday` | `datetime` |
| `nogameday` | `date` |
| `roadcouple` | `teamnamea`, `teamnameb` |
| `noweekgames` | `teamname` |
| `homerights` | `teamname` |
| `sisterteam` | `teamname`, `gender` |
| `sistergame` | `datetime` |
| `predefinedgames` | – (Singleton) |
| `game` | `datetime`, `hometeamname`, `guestteamname` |
| `existingschedule` | – |
| `schedule` | – |
| `mandatorygames` | `datefrom`, `dateto` |
| `ranking` | – |
| **jeder andere Name** | **Exception** „Kein Key für den Typ“ (auch beim Laden unbekannter XML-Knoten!) |

Schlüssel ohne Attribute bedeuten, dass alle Kinder mit diesem Namen gleich sind. Deshalb wirft `findChildIndexByKey` bei mehr als einem solchen Kind „Doppelter Schlüssel“.

## XML-Formate

### 1. click-TT-Import (`LoadFromClickTTFile`)
Erkennung: `DocumentElement.NodeName = 'TT'`. Alle Werte werden getrimmt. Datumsformat `dd.mm.yyyy`, Zeitformat `hh:mm`.
```xml
<TT gender="Herren" mid="dd.mm.yyyy" from="dd.mm.yyyy" until="dd.mm.yyyy" name="Staffelname" id="…">
  <teams>
    <team clubId="…" id="…" number="1" noDoubleDays="true|false">
      <name>Mannschaftsname</name>
      <gameDays>
        <homeGame day="dd.mm.yyyy" time="hh:mm" secondary="true|false"
                  maxParallelGames="n" coupleGame="soft|…"/>
        <noGame from="dd.mm.yyyy" until="dd.mm.yyyy"/>
        <roadCouple teamA="…" teamB="…" onSameDay="true|false"/>
        <noWeekGame team="…"/>
      </gameDays>
      <sisterTeams>
        <team gender="…" number="n">
          <name>…</name>
          <game day="dd.mm.yyyy" time="hh:mm" homeTeam="…" guestTeam="…"/>
        </team>
      </sisterTeams>
    </team>
  </teams>
  <predefinedGames>  <game day time homeTeam guestTeam/> … </predefinedGames>
  <noGameDays>       <noGame from until/> … </noGameDays>
  <mandatoryGameDays><mandatoryDay day="dd.mm.yyyy"/> … </mandatoryGameDays>
  <existingSchedule> <game day time homeTeam guestTeam/> … </existingSchedule>
</TT>
```
Abbildung auf das interne Modell:
- Root `plan`: `gender`, `mid` (Beginn der Rückrunde), `from`, `until` (jeweils über `DateFromString` → **Pflicht**, sonst Exception bei `StrToInt('')`), `name`, `id`.
- `team` → `team`: `clubid`, `teamid` (aus `id`), `location=''`, `homerights=-1`, `teamnumber` (aus `number`, Standard 1), `teamname` (aus `<name>`). `noDoubleDays` hat den Standard `'true'`. Doppelspieltage werden nur gebildet, wenn `noDoubleDays="false"` gesetzt ist.
- `homeGame` → `homegameday` (nur wenn `day` und `time` vorhanden sind und der Schlüssel `datetime` noch nicht existiert): `datetime`, `couplegameday=false`, `coupleauswaertssecondtime=false`, `doublegameday=false`, `ausweichtermin=(secondary=='true')`, `location=''`, `parallelgames=maxParallelGames|0`. Bei gesetztem `coupleGame`: `couplegameday=true`, `coupleprio = 1` bei `soft`, sonst `2`. `couplesecondtime` = Zeit − 4 h, wenn die Zeit > 16:59 ist, sonst Zeit + 4 h (jeweils mit `FixDateTime`).
- **Doppelspieltag-Ableitung** (nur bei `noDoubleDays="false"`): Zwei `homegameday` derselben Mannschaft ohne Koppel- und Doppelmarkierung, deren Kalendertage genau 1 Tag auseinanderliegen, werden beide zu `doublegameday=true`, `coupleprio=1`. Der Vergleich läuft über alle Paare. Ein Tag kann mehrfach markiert werden.
- `noGame from/until` (pro Mannschaft) → pro Kalendertag ein `nogameday date=…` unter `team` (ohne Dubletten).
- `roadCouple` → `roadcouple teamnamea, teamnameb, sameday`, mit `sameday = 0` bei `onSameDay="true"`, sonst `1`. Die Werte bedeuten (siehe PlanTypes/Dialog): 0 = „am gleichen Tag“ (`ktSameDay`), 1 = „mit Übernachtung“ (`ktDiffDay`), 2 bzw. sonst = „am gleichen Tag oder mit Übernachtung“ (`ktAllDay`, nur per Dialog setzbar).
- `noWeekGame team` → `noweekgames teamname` (Dubletten werden über eine `TStringList.IndexOf` **ohne Beachtung der Groß-/Kleinschreibung** gefiltert). Fachlich: Gegner, gegen die nicht unter der Woche gespielt werden soll (60-km-Regel).
- `sisterTeams/team` → `sisterteam teamname, gender, location='', teamnumber, parallelhomegames=false, noparallelgames=false`. Gleiches Geschlecht wie die Staffel und `|teamnumber − eigene| = 1` („Nachbarmannschaft“) ergibt `noparallelgames=true`. Dessen `game` → `sistergame datetime, hometeamname, guestteamname`. Spiele mit `spielfrei` werden übersprungen, **Duplikate derselben datetime verworfen** (Kommentar „Fehler mit den Bambinis“).
- `predefinedGames` (nur wenn nicht leer) → `predefinedgames` mit `game datetime, hometeamname, guestteamname, location=''`.
- `noGameDays/noGame` → globale `nogameday date` **direkt unter `plan`** (ohne Dublettenprüfung; `from`/`until` sind Pflicht).
- `mandatoryGameDays/mandatoryDay` → sortiert. Aufeinanderfolgende Tage (Abstand 1) werden zu einem Bereich `mandatorygames datefrom, dateto, numbergames=1` zusammengefasst.
- `existingSchedule` (nur wenn nicht leer) → `existingschedule` mit `game …` (der in click-TT bereits vorhandene Plan).

### 2. Eigenes Speicherformat (`SaveToXML` / `LoadFromXML`)
UTF-8, XML 1.0, Root `andigenerator`, darunter genau ein `plan`. `LoadFromXML` lädt nur das erste Kind namens `plan`. Alle weiteren Knoten sind die rekursive Abbildung des `TDataObject`-Baums, die Attributreihenfolge ist beliebig (Dictionary).
```xml
<andigenerator>
  <plan gender="" mid="dd.mm.yyyy" from="dd.mm.yyyy" until="dd.mm.yyyy" name="" id="">
    <team clubid="" teamid="" location="" homerights="-1|0|1" teamnumber="1"
          teamname="" orgteamname="">
      <homegameday datetime="dd.mm.yyyy hh:mm" couplegameday="true|false"
                   coupleauswaertssecondtime="true|false" doublegameday="true|false"
                   ausweichtermin="true|false" location="" parallelgames="0"
                   coupleprio="0..2" couplesecondtime="hh:mm"/>
      <nogameday date="dd.mm.yyyy"/>
      <roadcouple teamnamea="" teamnameb="" sameday="0|1|2"/>
      <noweekgames teamname=""/>
      <homerights teamname="" homeright="0|1|2"/>
      <sisterteam teamname="" gender="" location="" teamnumber=""
                  parallelhomegames="true|false" noparallelgames="true|false">
        <sistergame datetime="dd.mm.yyyy hh:mm" hometeamname="" guestteamname=""/>
      </sisterteam>
    </team>
    <nogameday date="dd.mm.yyyy"/>                               <!-- global -->
    <mandatorygames datefrom="" dateto="" numbergames="1"/>
    <predefinedgames><game datetime="" hometeamname="" guestteamname="" location=""/></predefinedgames>
    <existingschedule><game …/></existingschedule>
    <ranking active="true|false"><team teamname="" rankingindex="n"/></ranking>
    <schedule><game datetime="" hometeamname="" guestteamname=""/></schedule>
  </plan>
</andigenerator>
```
- `orgteamname`: gesetzt, wenn eine Mannschaft im Dialog umbenannt wurde (Originalname aus click-TT, siehe `DialogPanelTeams`).
- `homerights` am team: Index für das Heimrecht (−1 = keins, Dialog `DialogPanelHomeRightDetail`: `ItemIndex = Wert+1`). Kindknoten `homerights teamname homeright`: Heimrecht gegen einen bestimmten Gegner (1 = `hrRound1`, 2 = `hrRound2`, sonst keins).
- `ranking` und `team` mit `rankingindex`: Setzliste (`DialogPanelRanking`, `TPlan.LoadRanking`).
- `schedule`: nur in den **gemerkten Plänen** (`TPlan.SaveScheduleToXML`/`LoadScheduleFromXml`). Die Datei enthält dann nur `<plan><schedule>…`.
- In `.modifications`-Dateien kommt an jedem Knoten optional `state="dosNew|dosModified|dosDelete"` hinzu.
- Die Optionen stehen **nicht** hier, sondern in `AndiGenerator.options` (PlanTypes, `TCalculateOptions`).

## Merge/Diff-Semantik (Basisdaten + Benutzeränderungen)
Beim Speichern gilt: `Diff := Diff(After = PlanData (aktuell), Before = PlanDataClickTTFile (Basis))` → `.modifications`.
Beim Laden gilt: `PlanData := Basis; PlanData.Merge(LoadFromXML(.modifications))`.

**`Diff(After, Before)`**, rekursiv, erzeugt den Knoten `Self`:
1. `Self` leeren. Die Schlüsselattribute von `After` werden immer geschrieben (damit der Knoten beim Merge gefunden wird).
2. Jedes Attribut, das sich zwischen After und Before unterscheidet (in beiden Richtungen geprüft), wird mit dem **After-Wert** gesetzt, und es gilt `Self.State := dosModified`. Ein in After fehlendes Attribut wird als `''` gespeichert (echtes Löschen gibt es nicht).
3. Jedes Kind aus Before ohne Schlüssel-Treffer in After wird als neues Kind mit allen Attributen (ohne Enkel) und `state=dosDelete` angelegt.
4. Jedes Kind aus After ohne Treffer in Before wird als **vollständige tiefe Kopie** mit `state=dosNew` angelegt.
5. Jedes Kind mit Treffer bekommt ein neues Kind mit rekursivem `Diff`. **Auch unveränderte Kinder erzeugen einen Knoten** (State dosNormal, nur Schlüsselattribute). Die Diff-Datei spiegelt damit die komplette Baumstruktur (sie ist nicht minimal).

**`Merge(Source)`**, rekursiv auf dem Zielbaum:
- `Source.State = dosModified` → **alle** Attribute der Quelle werden übernommen (Schlüssel und geänderte Werte).
- Für jedes Kind der Quelle wird der Zielindex per Schlüssel gesucht:
  - `dosNew`: Ein vorhandenes Kind mit gleichem Schlüssel wird gelöscht und durch eine tiefe Kopie ersetzt (die Kopie hat State dosNormal, weil `assign` den State nicht kopiert). Die neue Kopie wird **ans Ende** angehängt.
  - `dosDelete`: Das Zielkind wird gelöscht, falls vorhanden.
  - sonst (dosNormal/dosModified): Existiert das Zielkind, wird rekursiv gemergt. **Existiert es nicht (z.B. weil click-TT die Mannschaft inzwischen entfernt hat), wird die Änderung stillschweigend verworfen.**
- Folgen: Der Diff-Knoten enthält nur Schlüssel plus geänderte Attribute, der Merge überschreibt also nur diese. Ändert click-TT später einen Wert, den der Benutzer nicht angefasst hat, gewinnt click-TT. Hat der Benutzer genau dieses Attribut geändert, gewinnt der Benutzerwert dauerhaft. Hat der Benutzer ein Attribut entfernt, wird es als `''` gesetzt. Ausnahme: Knoten mit `dosNew` ersetzen den Basisknoten vollständig inkl. aller Unterknoten.
- Ändert sich ein Schlüsselattribut (z.B. `datetime` eines Heimtermins, Umbenennung einer Mannschaft), entsteht Delete + New.

## THomeDay – Kurztextformat (`AsShortString` / `FromShortString`)
Das Format dient zur Darstellung bzw. Eingabe eines Tages in der Terminmeldungs-Tabelle. Das Datum kommt von außen (Spalte bzw. Zeile), der Text enthält nur Zeit und Flags.

**AsShortString** (Token durch Leerzeichen getrennt, in genau dieser Reihenfolge):
- `NoDate` → `''`
- `SperrTermin` → `FREI`
- sonst:
  1. `hh:mm` (Anstoßzeit)
  2. `A`, wenn Ausweichtermin
  3. `K<prio>:<hh:mm>`, wenn Koppeltermin (Prio 0–2, zweite Zeit), **sonst** `T2:<hh:mm>`, wenn Auswärtskoppel-Zweitzeit
  4. `D<prio>`, wenn Doppelspieltag
  5. `Max:<n>`, wenn `MaxParallelGame > 0`
  6. `L:<Ort>`: `"` im Ort wird durch `„` ersetzt, enthält der Ort Leerzeichen, wird er in `"…"` eingeschlossen.

Beispiel: `19:30 A K2:15:30 Max:2 L:"Halle 2"`

**FromShortString(Date, Value)** (Groß-/Kleinschreibung egal):
- Zerlegung an Leerzeichen außerhalb von `"…"` (`SplitString`).
- Leerer Text → `NoDate = True`, Rückgabe False.
- Token `FREI` → Sperrtermin. Am Ende wird alles andere gelöscht, `Date = Trunc(Date)`, Rückgabe True.
- Token mit `:` an Position 3 und zwei numerischen Teilen → Zeit, `Date := FixDateTime(Date + Zeit)`, Rückgabe True. Ohne gültige Zeit (und ohne FREI) ist die Rückgabe **False**.
- `A` → Ausweichtermin.
- `L:<x>` → `Location := DecodeLocation(x)` (Anführungszeichen entfernen, `„` → `"`).
- `K<p>:<hh:mm>` (Länge ≥ 8, p ∈ 0..2) → Koppeltermin. Wird nur ausgewertet, wenn bisher weder `D` noch `T2` erkannt wurde.
- `D<p>` (p ∈ 0..2) → Doppelspieltag, nur wenn kein Koppeltermin vorliegt.
- `T2:<hh:mm>` → Auswärtskoppel-Zweitzeit, nur wenn kein Koppeltermin vorliegt.
- `MAX:<n>` (n ≥ 0) → MaxParallelGame.
- Die Reihenfolge der Token ist beliebig, die Prioritätslogik hängt aber von der Reihenfolge ab (z.B. `D1 K1:15:00` → nur D).

**Abbildung THomeDay ↔ XML** (`GetHomeDays`/`SetHomeDays`):
- `GetHomeDays`: Alle `nogameday` des Teams → `SperrTermin`-Einträge. Alle `homegameday` → Heimtermine. `couplesecondtime` wird bei einem Koppeltermin gelesen, bei einem Nicht-Koppeltermin nur, wenn `coupleauswaertssecondtime` gesetzt ist. `coupleprio` wird nur bei Koppel- oder Doppeltermin gelesen. `location` wird per `FixLocation` validiert (nur `''`, `1`..`5` gültig!). **Ein Heimtermin an einem Sperrtag wird verworfen.** Danach wird nach Datum sortiert.
- `SetHomeDays`: Löscht alle `homegameday`/`nogameday` des Teams und schreibt sie neu. `NoDate` wird ignoriert, `SperrTermin` → `nogameday date`, sonst `homegameday` mit allen Attributen (`couplesecondtime` bei Koppel oder T2, `coupleprio` bei Koppel oder Doppel).

## Fachliche Logik / Regeln
- Koppeltermin-Zweitzeit beim Import: ±4 h, Schwelle 16:59.
- `coupleGame="soft"` → Prio 1, jeder andere nicht-leere Wert → Prio 2.
- Doppelspieltage aus Heimterminen an zwei aufeinanderfolgenden Tagen (nur bei `noDoubleDays="false"`), Prio 1.
- Nachbarmannschaft (gleiches Geschlecht, Mannschaftsnummer ±1) → `noparallelgames=true`.
- Spiellokale: click-TT kennt nur numerische Spiellokale, deshalb gilt fest die Menge `''`, `1`..`5`.
- Pflichtspieltage: aufeinanderfolgende Tage werden zu Bereichen zusammengefasst, je 1 Spiel.
- Sperrtermine (Zeiträume) werden pro Tag expandiert.

## Daten & Persistenz
- Lesen: click-TT-XML (beliebige Kodierung, MSXML), eigene XML (`plan`-Format, `.modifications`, gemerkte Pläne).
- Schreiben: `SaveToXML` in UTF-8. Bei einem Fehler zeigt `ShowMessage` („Datei … kann nicht geschrieben werden“) die Meldung an, die Exception wird geschluckt.
- Datums- und Zeitformatierung über `DateTimeToString`: `:` in `hh:mm` ist in Delphi der **lokale Zeittrenner** (`FormatSettings.TimeSeparator`). Auf Systemen mit anderem Trenner würden Dateien geschrieben, die der eigene Parser (fest `:` an Position 3) nicht mehr liest.

## Threading / Performance
Keine Nebenläufigkeit. Performance ist unkritisch (nur beim Laden und Speichern bzw. in Dialogen). `findChildIndexByKey` ist O(n) pro Suche → Diff/Merge O(n²) pro Ebene. Bei einigen hundert Knoten reicht das.

## Plattformabhängigkeiten
- `Xml.XMLDoc` mit MSXML-Vendor (Windows-COM). Plattformübergreifend in Delphi nur mit ADOM/Omni.
- `ShowMessage` in der Datenschicht (UI-Abhängigkeit).
- Locale-abhängige `DateTimeToString`-Formatierung.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.Persistence` mit
  - `DataNode` (Name, `Dictionary<string,string> Attributes` bzw. `List<KeyValuePair>` für stabile Reihenfolge, `List<DataNode> Children`, `DataNodeState State`, `Parent`)
  - `NodeKeys` (statische Tabelle Name → Schlüsselattribute)
  - `PlanDataDocument` (Load/Save, Merge, Diff)
  - `ClickTtImporter` (eigene Klasse)
  - `HomeDay` (record/class) + `HomeDayShortFormat` (Parser/Formatter)
- XML: `System.Xml.Linq` (`XDocument`/`XElement`), funktioniert überall inkl. Android/iOS/AOT. Encoding beim Lesen automatisch, beim Schreiben UTF-8 ohne BOM oder mit BOM wie Delphi (prüfen: Delphi `TXMLDocument.SaveToFile` mit Encoding UTF-8 schreibt **ohne** BOM).
- Format **bitgenau kompatibel** halten (Attributnamen, `dd.mm.yyyy hh:mm`, `true`/`false`, `dosNew`-Enum-Namen), damit vorhandene `.modifications` und gemerkte Pläne weiter geladen werden können. Datum/Zeit **immer** mit `CultureInfo.InvariantCulture` und festen Mustern `"dd.MM.yyyy"`, `"dd.MM.yyyy HH:mm"`, `"HH:mm"` formatieren. Achtung: `mm`/`MM` sind in .NET vertauscht, und `hh` bedeutet in .NET 12-Stunden-Format!
- Dieses Modell sollte man nicht durch ein typisiertes Modell ersetzen, ohne die Diff/Merge-Semantik exakt nachzubilden. Empfehlung: das generische `DataNode`-Modell 1:1 portieren (klein, gut testbar), typisierte Zugriffe als Extension-Methoden bzw. Wrapper darüberlegen.
- `SetAsString` trimmt immer. Unbekannte Knotennamen werfen eine Exception → in C# ggf. tolerant machen (Vorwärtskompatibilität), das aber bewusst entscheiden.
- `TDictionary` ist ungeordnet: Delphi schreibt die Attribute in Hash-Reihenfolge. Für C# eine stabile Reihenfolge wählen (für Tests und Diffs angenehmer). Die Kompatibilität bleibt gewahrt, weil XML-Attributreihenfolge irrelevant ist.
- `TDateTime`-Vergleich in `THomeDay.isTheSame` (Double-Gleichheit) → in C# `DateTime` (Ticks), nach Parsen immer auf Minuten runden.
- `DateFromString` ist positionsbasiert (`Copy`, 1-basiert). In C# `DateTime.ParseExact`, aber Achtung: Delphi akzeptiert z.B. `"1.2.2020"` nicht. Gleiches Verhalten ist unkritisch.
- `THomeDay.FromShortString`: Den Parser exakt inkl. der Reihenfolgeabhängigkeit portieren und mit Unit-Tests absichern (Roundtrip AsShortString ↔ FromShortString).
- Aufwand: **M**. Überschaubarer Code, aber Kompatibilität und Semantik (Diff/Merge, Kurztext) müssen exakt stimmen und brauchen umfangreiche Tests.

### Bugs/Auffälligkeiten
- Z. 1780–1789: `getAttributValue(mainNode, …)` wird **vor** der Prüfung `Assigned(mainNode)` aufgerufen. Fehlen `mid`/`from`/`until`, gibt es eine Exception in `DateFromString` (`StrToInt('')`).
- Z. 1571–1572: `findChildNode(sisterTeamNode,'name')` kann nil sein → Access Violation bei `nameNode.text`.
- Z. 1426–1427: Ein globales `noGame` ohne `from`/`until` führt zu einer Exception.
- Z. 1344/1596/1655: `StrToInt` ohne Fehlerbehandlung bei nicht-numerischem `maxParallelGames`/`number`.
- Z. 1464–1476: Dubletten-Prüfung bei `noWeekGame` ohne Beachtung der Groß-/Kleinschreibung (TStringList-Standard), die Schlüsselsuche sonst mit.
- Z. 1624: Mehrere Schwesterspiele zur exakt gleichen Zeit werden verworfen (Schlüssel nur `datetime`).
- Z. 1510–1537: Die Doppelspieltag-Ableitung markiert auch Ketten (Fr/Sa/So → alle drei als Doppel).
- Diff: Ein in After fehlendes Attribut wird als `''` statt als „gelöscht“ gespeichert. Unveränderte Knoten blähen die Diff-Datei auf.
- Merge: Änderungen an Knoten, die in den Basisdaten nicht mehr existieren, gehen **ohne Warnung** verloren. Die Reihenfolge ändert sich bei dosNew (ans Ende).
- Z. 2044/2056: Die Kommentare „Geschütztes Leerzeichen“ passen nicht zum Code (ersetzt wird `"` ↔ `„`).
- Z. 1857: `ShowMessage` in der Datenschicht, die Exception wird geschluckt → der Aufrufer merkt nicht, dass das Speichern fehlgeschlagen ist.
- `DateTimeToString('hh:mm')` hängt von der Locale ab (Zeittrenner).
- `getTeamLocationsInt`: Spiellokale fest auf 1–5 begrenzt, ein Spiellokal 6 wird beim Laden stillschweigend auf `''` gesetzt.

## Offene Fragen
- Gibt es eine Spezifikation bzw. ein XSD der click-TT-Exportdatei, oder sind weitere Attribute bekannt (z.B. mehr als 5 Spiellokale)?
- Soll die Merge-Semantik („Benutzerknoten überschreibt alle Attribute“, „verwaiste Änderungen verwerfen“) exakt so bleiben oder soll sie verbessert werden (z.B. Warnung bei verworfenen Änderungen)?
- Sollen unbekannte Knoten beim Laden toleriert werden?
- Müssen alte `.modifications`-Dateien und gemerkte Pläne der Delphi-Version garantiert lesbar bleiben (dann Formatkompatibilität als Pflicht)?
- Bedeutung von `coupleprio` 0 (möglich im Kurztext `K0`/`D0`)?
