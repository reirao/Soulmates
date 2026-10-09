# Soulmates 0.24.0: Learning Together

Local development candidate, October 9, 2026. Based on published 0.23.2 at
`058d27c1c372ea36f75d9cd3a06c2944fcf589f6`.
Preparation included no normal-client installation, public upload or graphical
playtest. Subsequently installed at the user's request, October 9 at 14:26:56
CEST; see [installation receipt](CLIENT-INSTALL-0.24.0-2026-10-09.md). The creator
subsequently reports a positive first playtest and requests publication. This
player report is not an agent-observed exhaustive graphical test. Endurance
and connected multiplayer acceptance remain open; publication is tracked
separately from this preparation and installation record.

## Scope

One small preference model joins existing observations, requests, outcomes and
the existing question wheel. It changes relevance in the shared attention
selector, not the task executor. No parallel AI framework or new window.

- Contexts: general, building, mining, forestry, exploring, social, underground.
- Accepted explicit work provides a small bounded preference cue. Rejected
  orders do not. Request cues have a three-second rate limit.
- Existing owner observations provide weaker bounded cues, never completed
  work or player approval. Construction-item use is sampled through the existing
  owner-observation path. It remembers up to twelve stable material-item keys;
  familiar supplies receive a small distance-score advantage while building.
- Actual completed automatic work, explicit jobs and critter visits record an
  outcome once. Successful work may queue one immutable action/context result
  for an occasional "Was that helpful?" question.
- Four native-symbol answers: helpful, another idea, less of this, later.
  Preference changes are +8, -4, -8 and 0, clamped to [-32,32]. Outcomes and
  observation/request cues remain bounded separately; combined relevance is
  clamped to [-32,40]. Attempts saturate at 32. Untried eligible actions have a
  small novelty bias. Existing attention aging still prevents starvation.
- Later, unanswered questions, changed targets and player cancellations do
  not supply negative player feedback. Genuine unsuccessful attempts supply
  modest outcome evidence, not mood punishment.
- No XP is awarded for evaluation. Approval can use the existing bounded
  positive-interaction bond/mood reward; other evaluation answers do not.
- Evaluation uses the existing personal question interval: two minutes Chatty,
  five minutes Calm. The first completed result can ask earlier. Quiet disables
  evaluations. A queued result expires after five active minutes if it cannot
  be discussed. Pending questions retain the existing one-minute answer window.
- Defense, explicit work, Pause/Stay, Ask/Always/Never, capability, cargo,
  tool strength and progression remain authoritative. Preference is not consent.
- Disabling autonomy/resetting automatic rules does not erase a still-active
  explicit job's intention. Abort also clears unshown evaluations. Recall clears
  temporary intentions and evaluations without deleting learned preferences.
- Learned state is independent per Sigil; clone, save and bounded transport
  retain it. Old Sigils start neutral. Mixed 0.23/0.24 multiplayer is unsupported:
  all peers must use 0.24.0.
- Existing Diagnostics exposes context, active/result intentions, attempts,
  scores and evaluation values. Optional local Field Notes record outcomes and
  answers; no new recording switch or upload was added.

Construction-item use is **not** successful placement proof. This version does
not infer room shapes, copy buildings, own construction sites, invent new
abilities, craft arbitrary items or build/hammer the world autonomously.
The learned action vocabulary is still the six existing registered abilities.

## Implementation

- `Common/CompanionIntentLearning.cs`: bounded estimates, material hints,
  persistence, independent cloning and validated profile extension.
- `Content/NPCs/SoulboundCompanion.Intentions.cs`: transient observation/intent
  snapshots, completion, queued evaluation and contextual feedback.
- Existing Autonomy, Work, Nature and Social partials provide actual lifecycle
  events; Conversation and InitiativePromptSystem reuse the answer interface.
- Existing profile/network/diagnostic paths carry and inspect the same model.
- Sixteen new text keys are present in all nine catalogs. New translations are
  AI-authored; native-speaker and rendered-client review remain open.

## Verification

Final candidate built with native tModLoader 2026.8.3.0, bundled .NET 8.0.425.

| Check | Result | Scope |
| --- | --- | --- |
| Production native compiler | 0 errors, 0 warnings | Production assembly |
| Engine regression probe | 94,235 assertions, 0 failures | Isolated native engine fixtures |
| Independent audit probe | 198 expectations, 0 failures | Server AI, packet/UI safety fixtures |
| Static/localization suite | 55,855 assertions passed | 949 keys, 9 languages, 501 source references |
| Native package verification | Passed | Payload hash, 83 sources, 111 entries, private-content exclusion |
| Description byte limits | Passed | Main 7,888; Workshop EN 7,840; DE 7,952 UTF-8 bytes in checkout |
| `git diff --check` | Passed | Whitespace |

New fixtures exercise owner construction-item observation, real directed and
automatic pickup with conserved cargo, native NPC.AI completion, company and
catch visits, context-stable delayed answers, all four actual production
UI-handler button paths, token replay rejection, independent Sigils,
clone/save/load, malformed wire estimates, shared question cadence, Quiet,
client authority rejection, Never/Pause, Abort and automatic-rule resets.
Existing regression coverage also runs; counts are assertions, not distinct
features or proof of exhaustive correctness.

The regression probe retains 33 pre-existing nullable/Hjson reference warnings.
Expected malformed-packet and blocked-write diagnostics are deliberate tests.
Restricted registry-association access is unrelated to the mod logic.

During development a headless UI fixture initially lacked a GameViewMatrix and
viewport; those fixture dependencies were supplied rather than skipping the
button checks. Audit fixture compilation also exposed a read-only talkNPC
property and missing namespace import; it now uses SetTalkNPC and the correct
namespace. A too-long description failed the byte-limit gate and was shortened.
Critical rereading found automatic cancellation erasing direct-job intentions
and Abort retaining an unshown evaluation; both now have regression coverage.

These are headless native tests, not a new played world, rendered screenshots,
physical keypress proof or a connected multiplayer session. V is still not
promised fixed. Long-session behavior and whether learning feels natural remain
real-client acceptance work.

## Local Artifact

`outputs/intent-learning-0.24.0/Soulmates.tmod`

SHA256: `43F5C21289E0008D5A672BFCBA127407D128D93BF4927BE675FA90DE8A639E58`

Receipt: `outputs/intent-learning-0.24.0/package-verification.json`.
Native regression/audit reports are under `work/pet-inventory-0.24.0-regression/`
and `work/pet-inventory-0.24.0-audit/`. These directories are not packaged.
At the original preparation check published 0.23.2 remained unchanged. This
historical test receipt is not a live publication indicator; current publication
is recorded separately.

## Kurzer Spieltest

1. Nach einer separaten Installation unter Mods wirklich 0.24.0 pruefen. Eine
   Testwelt und ein bestehendes Sigil nutzen; Identitaet, Pets und Fracht muessen
   erhalten bleiben. Details/Diagnose ueber ihr Rad oeffnen, nicht V voraussetzen.
2. Autonomie einschalten, Folgen und normale Energie sicherstellen. Gathering
   auf Immer, andere Faehigkeiten zunaechst auf Nie setzen. Kurz Holz als
   Baumaterial benutzen. Diagnose soll Building und den beobachteten Item-Key
   zeigen, aber keine angeblich erfolgreich platzierten Bloecke.
3. Einen echten Drop ausserhalb der eigenen Pickup-Reichweite anbieten. Nach
   abgeschlossener Hilfe und abgeklungener Rede soll gelegentlich die Bewertung
   erscheinen. Bei Bedarf den persoenlichen Frageabstand abwarten. Keine Frage
   waehrend Kampf, Pause, Stay, anderem Auftrag oder gesperrter Eingabe erwarten.
4. Die vier Antwortsymbole mit Hover-Text pruefen. Zustimmung erhoeht den Wert
   fuer diese Aktion/Situation, weniger davon senkt ihn, Spaeter veraendert ihn
   nicht. Erlaubnisse muessen unveraendert bleiben. Nie darf auch nach Lob keine
   automatische Aktion erlauben.
5. Einen Auftrag pausieren/fortsetzen und einen anderen abbrechen. Abbruch darf
   keine negative Bewertung und keine alte Nachfrage nach Fortsetzen erzeugen.
   Autonomie ausschalten darf einen direkten laufenden Auftrag nicht loeschen.
6. Ein zweites Sigil verwenden und speichern/neustarten. Gelernte Werte muessen
   pro Companion bleiben; offene Bewertungen nicht. Danach mehrere erlaubte
   Hilfen parallel beobachten: andere Prioritaet, kein Zwang zum Gehorsam und
   keine ungefragten Welt-Umbauten. Fuer MP alle Peers auf dieselbe Version bringen.

Bitte Aktion, erwartetes/tatsaechliches Ergebnis, Diagnose und Solo/MP festhalten.
Besonders wichtig: Fragefrequenz, lesbare Texte, keine Phantom-Fracht und ob die
veraenderte Auswahl ueber mehrere Minuten tatsaechlich individuell wirkt.
