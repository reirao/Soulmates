# Soulmates 0.21.0: fokussierter Client-Test

Lokaler Kandidat, am 5. Oktober 2026 in deinen normalen Client installiert; Paket und Spielstände sind gesichert. Siehe [Installationsnachweis](CLIENT-INSTALL-0.21.0-2026-10-05.md). Nicht veröffentlicht. Nach dem Start muss im Loader **0.21.0** stehen. Alle MP-Teilnehmer brauchen dieselbe Version. Test-Probes niemals im Spielclient aktivieren.

## Arbeit und Navigation

1. Rechtsklick auf den Companion > Arbeit: genau **Einstellen** und **Zuletzt**. Einstellen entfaltet die bisherigen Aufträge. Zurück geht jeweils eine Ebene; X/Escape beendet den Modus. Spieler-Rad und native Rechtsklicks auf Werkbank, Truhe und NPC bleiben unabhängig.
2. Bereichsabbau starten; danach Zuletzt benutzen. Abgelehnte Aufträge, Ansehen und reine Einstellungen dürfen den letzten angenommenen Auftrag nicht ersetzen. Nach Rückruf/Speichern bleibt er erhalten.
3. Einen gezielten Sammel-/Abbau-/Forstauftrag annehmen. Zuletzt aktiviert anschließend die Zielauswahl neu, nicht den alten Block oder einen wiederverwendeten Drop-Slot.
4. Einstellen > Abbaumodus > Tunnel: Oben/Rechts/Unten/Links/Auto und Kurz/Bis Öffnung ausprobieren. Die Auswahl allein darf keinen Abbau starten. Kurz endet spätestens acht Schritte vom Ausgangspunkt entfernt; Bis Öffnung spätestens 24. Der Arbeitsradius kann beides verkürzen.
5. Kardinale Tunnel vor Pflanzen, Dekorationen, nicht abbaubaren Blöcken, fallendem Material und Flüssigkeit prüfen. Keine geschützte Stelle überspringen. Ein einzelnes Luftloch ist noch kein begehbarer Durchgang. Änderungen während des Abbaus müssen neu geprüft werden.

## Echte Critter

1. Autonomie ausschalten, einen natürlichen Hasen/Eichhörnchen/Vogel anklicken und Gesellschaft wählen. Der Companion soll den Besuch trotzdem ausführen. Die automatische Regel darf dadurch nicht auf Immer wechseln.
2. Das eingeladene Tier soll auf offenem Gelände folgen, ohne durch Wände zu gehen oder teleportiert zu werden. Ein kleines Hindernis prüfen. Große Wände/Höhenunterschiede sind keine zugesicherte Wegfindung.
3. Pause stoppt die geführte Bewegung; natives Wandern ist weiterhin möglich. Aus/Rückruf lässt das echte Tier lebend zurück. Normale Companions: ein Tier, AETHER: bis zu drei. Native Gesundheit und Despawning gelten weiter.
4. Gewöhnliches Tier gezielt fangen, auch mit Autonomie aus. Das Basisnetz ist eine Fähigkeit, kein neues Inventaritem. Genau ein echtes Fangitem muss im Pack landen. Goldene, freigelassene und Statuentiere bleiben geschützt; Lava-Tiere brauchen ein mitgetragenes lavafestes Netz.
5. Automatische Gesellschaft/Fang separat mit Fragen/Immer/Nie testen. Beobachten fängt nie. Energie, Aufträge, offene Fragen und Verteidigung können neue automatische Besuche aufhalten; Hover zeigt den Zustand.
6. In Sichtnähe einen echten Critter-Tod beobachten: passende Reaktion und später beantwortbare Fürsorgefrage. Fang darf keine Todesreaktion auslösen. Diese Live-Reaktion war zuvor unzuverlässig und ist weiterhin ein ausdrücklicher Abnahmepunkt.

## Item-Pets

1. Einen echten **Zephyr Fish** oder **Nectar** ins Ausrüstungspack geben: Item auswählen, Gespräch > Pack > Verstauen. Tiere > **Pet des Gefährten** zeigt beide nativen Item-Symbole. Ohne das Item bleibt die Auswahl ohne Beschwörung.
2. Item auswählen: genau ein eigenes harmloses Pet folgt ihr. Das Item bleibt im Pack und wird nicht verbraucht. Dein eigenes Terraria-Pet soll gleichzeitig weiter existieren, ohne neue Spieler-Buffs.
3. Zwischen Zephyrfisch und Baby-Hornisse wechseln, mehrfach auswählen und entlassen. Keine doppelten Pets. Animation, Blickrichtung, Lesbarkeit im Dunkeln und Position beim Bleiben/Arbeiten prüfen.
4. Das letzte zugehörige Item entnehmen: das eigene Pet verschwindet. Wieder verstauen erfordert eine neue Auswahl. Rückruf, Companion-Wechsel und Tod dürfen kein verwaistes Pet hinterlassen; Rückruf/Speichern dürfen das echte Item nicht verlieren.
5. Nach Speichern/Neuladen und erneutem Beschwören muss eine gültige Auswahl erhalten bleiben. In MP einem laufenden Spiel beitreten und Auswahl/Entnahme/Entlassen von einem zweiten Client beobachten.

Nur diese zwei Flugpets sind unterstützt. Das ist ein eigener harmloser Follower mit Terraria-Sprite-Frames, nicht deren ursprüngliche Spieler-Pet-KI. Boden-, Licht- und Mod-Pets sind noch nicht implementiert. Die Erscheinung des Companions, echte Gesellschaftstiere und AETHERs kosmetischer Insektenschwarm sind getrennt.

Bitte Fehler mit Aktion, Soll/Ist, Version, Sprache, UI-Skalierung und Solo/MP notieren. Gerenderte Screens und verbundenes MP sind durch die automatisierten Engine-Tests nicht abgedeckt.
