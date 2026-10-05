# Soulmates 0.20.0: Testliste

Am 5. Oktober 2026 im normalen Client installiert. Dies ist der Beta-Kandidat,
noch keine vollständig abgenommene Beta. Automatische Prüfungen sind grün;
kein Spieltest gilt allein durch die Installation als bestanden.

## Vorbereitung

- [ ] Unter Mods muss **Soulmates 0.20.0** stehen. Falls noch 0.19.8 angezeigt
  wird, bitte vor dem Test melden.
- [ ] Zuerst Singleplayer. Eine Classic-Testwelt ist für Pickup gut: Bei
  Journey sammelt der Spieler die Drops sehr schnell selbst ein.
- [ ] Vorhandenes Sigil: Name, Aussehen, Level, Gepäck und Geld vorher notieren
  oder fotografieren. Erst mit unwichtigen Items testen. Lokale Spielerdateien
  und aktuelle Welten sind gesichert.
- [ ] Für automatische Arbeit: Autonomie an, Follow statt Stay, keine Pause,
  genügend Energie. Ask fragt um Erlaubnis, Always erlaubt Wiederholungen,
  Never verbietet diese automatische Fähigkeit.

## Erster Durchlauf

Die ersten sechs Punkte haben Priorität. Die übrigen brauchen passende Ziele
und dürfen später folgen. Englische Namen dienen als Orientierung im Menü.

1. **Erstellen und Wechseln**
   - [ ] Soulcore im Inventar rechtsklicken, Companion binden: echtes Sigil
     kommt ins Inventar, Soulcore bleibt erhalten.
   - [ ] Neues und altes Sigil abwechselnd benutzen: genau ein Companion,
     keine weiterlaufenden Arbeiten oder Angriffe des vorherigen.

2. **Rechtsklick und Wheels**
   - [ ] Truhe, NPC und Werkbank bedienen: normale Funktionen bleiben
     erreichbar; Werkbank öffnet Inventar/Craftingansicht.
   - [ ] Spieler und Companion anklicken: getrennte Wheels. Spieler hat
     Emotes, Companion hat Games und Items. Items zeigt Unterkategorien;
     Schere, Stein, Papier ist unter Games, nicht unter Bond.
   - [ ] Me/Soulmate-Kontextmodus auf Ressource, Drop und Critter testen:
     Aktionen passen zum Ziel. Beim Wechsel ins Wheel bleibt das gewählte
     Ziel erhalten. X/Escape stellt normale Bedienung wieder her.

3. **Pickup und Inventare**
   - [ ] Kleine gezählte Mengen Stein, Gel, Eicheln und Münzen hinlegen.
     Selbst Abstand halten, Gather erlauben: richtige Typen/Mengen landen
     in Resources beziehungsweise Wallet, ohne verschwundene oder erfundene Items.
   - [ ] Ressourcen und Geld zurücknehmen: Gesamtmenge aus Boden, Spieler
     und Companion bleibt gleich. Bei vollem Spielerinventar bleiben nicht
     übertragene Mengen erhalten. Equipment bleibt getrennt von Resources.

4. **Einheitliche Erlaubnisfragen**
   - [ ] Eine Arbeitsfrage mit Yes beantworten: erlaubt diese Aufgabe,
     stellt nicht automatisch auf dauerhaftes Always um.
   - [ ] Always und Never testen: spätere automatische Arbeit respektiert
     die Einstellung. Company und Pet Collect haben unabhängige Regeln;
     Begleiten erlaubt nicht automatisch das Fangen.

5. **Pause, Resume, Abort und Stay**
   - [ ] Längere Arbeit starten, Pause, dann Resume: Arbeit hält an und
     wird anschließend fortgesetzt.
   - [ ] Abort: Aufgabe wird verworfen; automatische Arbeit bleibt bis
     Resume oder einem neuen Auftrag angehalten.
   - [ ] Stay, selbst weglaufen: sie bleibt am Haltepunkt. Sprite,
     Schussursprung und Sprechtext kommen von ihrer wirklichen Position.

6. **Speichern und Wiederladen**
   - [ ] Mit Gepäck, Münzen und geänderten Regeln speichern, Welt verlassen,
     neu laden: Identität, Level, Mengen und Regeln bleiben erhalten.
   - [ ] Drei solche Zyklen durchführen. Ein gerichteter Weltauftrag muss
     nach Rückruf/Neuladen nicht sein altes exaktes Ziel wieder aufnehmen.

7. **Critter**
   - [ ] Bei natürlich gespawntem gewöhnlichem Critter Watch wählen:
     beobachten/begrüßen, nicht automatisch fangen.
   - [ ] Company erlauben: echter Critter darf folgen; AETHER kann bis zu
     drei begleiten. Off/Rückruf gibt sie frei, ohne Kopien zu erzeugen.
   - [ ] Pet Collect erlauben: gewöhnliche Critter können mit eingebauter
     Basisnetz-Fähigkeit gefangen werden, ohne eigenes Netz. Item landet
     im Gepäck. Lava-Critter brauchen ein besseres Netz.
   - [ ] Stirbt im normalen Spiel ein naher sichtbarer Critter, auf ihre
     Reaktion achten. Laufendes Gespräch kann sie verzögern. Fehlende Reaktion
     mit Entfernung und laufender Tätigkeit melden. Netzfang ist kein Tod.
     Gold-, Statuen- und freigelassene Tiere sind bewusst geschützt.

8. **Mining**
   - [ ] Work > Mining Mode > automatische Mining-Regeln: ein Erz erlauben,
     ein anderes verbieten. Automatik respektiert Auswahl; normales Gelände
     braucht ausdrücklich Erlaubnis. Zuerst einfache Erze testen.
   - [ ] Setzlinge/Pflanzen/Dekoration: deren tragender Boden bleibt stehen.
     Ungeeignete Spitzhacke oder fehlende Progression darf sie nicht umgehen.

9. **Forester/Bäume**
   - [ ] Forester oder AETHER bei normalem Baum und Schneebaum: Kontextmenü,
     erlaubtes Schütteln, wirkliche Drops. Falls verfügbar auch Palme,
     Edelstein- und Pilzbaum anklicken; nicht jede Art erlaubt jede Aktion.
   - [ ] Unterstützten trockenen Ast mit vorhandener Axt versuchen. Axt im
     Spielerinventar oder Companion-Gepäck reicht. Lebende Krone/Stamm darf
     nicht als toter Ast verschwinden. Nachpflanzen benötigt echte Eicheln.

10. **Kampf und Energie**
    - [ ] Gewöhnlicher Gegner: erkennbare Angriffe, passende Blickrichtung,
      keine Angriffe auf harmlose Critter.
    - [ ] Kampf während einer Arbeit: Verteidigung hat Vorrang; danach kann
      erlaubte Arbeit weitergehen. Ausdrückliche Pause bleibt bestehen.
    - [ ] Follow/Leerlauf: Energie regeneriert ohne erzwungene Care-Pause.
      Nach Erschöpfung baut sie erst eine Reserve auf, nicht sofort weiterarbeiten.

11. **Sprache, Antworten und Games**
    - [ ] Englisch/Deutsch, Itemthemen und Games: lesbare Texte statt
      Mods.Soulmates-Schlüsseln, ruhige Sprechbox nahe beim Companion,
      verständliche Emotes und korrektes Ergebnis bei Schere, Stein, Papier.
    - [ ] Persönliche Frage: auswählbare Antworten und Rückmeldung.
      Chatty fragt etwa alle zwei Minuten, Calm seltener; nicht sofort.

12. **Briefkasten**
    - [ ] Mailbox: Hinweis tippen, lokal senden, Speicherbestätigung sehen.
      Nichts wird hochgeladen. Optional /soulfeedback on für lokale Feldnotizen.

## Später, Nicht Alles Heute

- [ ] Eine Stunde Singleplayer mit Kampf, Arbeit, Gesprächen und Speichern.
  Auf allmähliches Verstummen/Stehenbleiben achten.
- [ ] Ressourcenlimit bei Level 1/10/20: 50/500/1000 pro Itemtyp, soweit
  native Stapel es erlauben. Kein spielerisches Wallet-Limit für Münzen.
- [ ] Frühe/spätere Spitzhacken und Hardmode-Fortschritt testen.
- [ ] Zwei echte Clients mit gleicher 0.20.0: gemeinsamer Pickup,
  Companionwechsel, Ein-/Auslagern, Verbindungsabbruch und Wiederbeitritt.
  Danach dedizierter Server und eine Stunde Multiplayer. Noch nicht durch
  Singleplayer oder die automatischen Serverprüfungen abgedeckt.

## Fehler Melden

Version/Spielmodus, dann **Aktion -> erwartet -> tatsächlich**. Bei Verhalten
auch Modus/Regel, Energie und laufende Aufgabe nennen. Screenshot hilft bei
Wheels, Text und Mengen; kurzer Briefkasten-Hinweis reicht. Bei Verlust oder
Vervielfachung den Test stoppen und betroffene Mengen melden. Sicherung behalten.

[Installationsnachweis](CLIENT-INSTALL-0.20.0-2026-10-05.md) und
[offene Beta-Gates](../BETA.md).
