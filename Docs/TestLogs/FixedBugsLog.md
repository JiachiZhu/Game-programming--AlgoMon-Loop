# Fixed bugs log

Notable bugs found through testing, their root causes, and what changed. Newest first. Day-to-day fixes live in commit history; this list keeps the ones that taught us something about the systems.

| Date | Bug | Root cause | Fix | Refs |
|---|---|---|---|---|
| 2026-06-12 | First standalone build would have lost all battle animations and styled menus | Animation profiles and many UI sprites were loaded through editor-only AssetDatabase calls; no runtime assets existed | Baked runtime catalogs in Resources + editor rebuild command; loaders fall back to catalogs in builds | branch `build/standalone-asset-loading`, TestLogs 2026-06-12 |
| 2026-06-12 | Counter-recast strike dealt damage with no attack animation/VFX/SFX | The `counterRecast` block resolved damage without publishing a `BattleActionEvent`, so the presentation layer never fired | Recast now publishes a fresh action event before damage resolve | BattleManager |
| 2026-06-12 | A counter could "eat" the presentation of a later normal attack | Counter suppression flag leaked when an action carried neither counter flag | Suppression is only consumed on actual counter events; plain actions clear stale suppression | BattlePresentationController |
| 2026-06-12 | Scene transition hitched visibly mid-animation | Synchronous `LoadScene` fired at ~83% of the progress bar | Bar completes to 100%, renders one frame, then loads — hitch hides behind the finished frame | GridLink/BattleLink transitions |
| 2026-06-12 | Grid→battle impact SFX inaudible | Clip had a 0.855 s silent lead-in, and BGM masked the hit | Re-sliced the clip (+gain, tail fade) and music now ducks 0.35 s on scene transitions | AudioManager |
| 2026-06-11 | Entire game was silent even though audio sources played | All gameplay scenes shipped with zero AudioListeners | AudioManager self-hosts a single listener on Awake | AudioManager |
| 2026-06-11 | Music started late and hitched during crossfades | Tracks imported as Streaming | Switched to CompressedInMemory with preload | audio import settings |
| 2026-04 (Sprint 1) | Slowest unit acted first in battle | TurnQueue priority comparison was inverted | Fixed the min-heap ordering in PriorityQueue/TurnQueue | issue #7 |
