# Profile reset / checkpoint integration (feature branch, not production verification)

Base: main 83d9085. No Supabase schema or aggregation changes.

## Integration API

- `SaveStore.CommitCompletedBattle(save, result)`: after rewards and DDA settlement; assigns battle UUID, commits progress atomically, then immutable snapshot. Abandoned battles have no snapshot. `false` may mean progress saved but snapshot failed; inspect LastError.
- EndBattle invokes this before `SaveStore.Log`. Logging remains independent on save failure (existing behavior); restore never invokes logging.
- `TryResetProfile(current, inBattle, out replacement)` / `TryRestoreProfile(current, snapshotId, inBattle, out replacement)`: only assign runtime save on true. Required backup must succeed before atomic active-save replacement. Caller supplies truthful battle guard and cleans transient gameplay state.
- `AllHistory(save)` combines archive metadata, current history and backups. `HasSnapshot` validates actual payload; legacy summary has no restoration.
- `SaveData.profileId`, `cloudPlayerId` are stable UUIDs. TutorialAssessment fields added by tutorial worker are preserved automatically through full serialization. Reset constructs new SaveData, so onboarding resets.
- CloudSync resolves captured identity from immutable payloads for every queued item and registers that ID before upload. A changed active ID does not alter in-flight requests. Corrupt or unknown owner items remain on disk; no substitution with current identity. Global old registered flag ignored.

## Storage

`progress.json` atomic commit + existing `.bak` and v1 migration backup retained. `ProfileArchive/backups/<guid>.json` retains pre-switch full state/history. `ProfileArchive/snapshots/<guid>.json` stores battle metadata plus full progress with history list emptied (archive does not recursively expand). Archives and local CSVs never pruned. Server historical data is not rolled back; restored profiles' next battles get new IDs.

## Tests and remaining limits

`python Tests/run-dda-history.py --suite profiles --dotnet <SDK>` runs isolated filesystem tests. Runtime uses actual JsonUtility; headless uses existing serializer shim. This is not Unity UI or network validation.

No runtime Supabase test performed; production config not read. Queue owner migration, per-ID registration and actual coroutine behavior need isolated test service verification. No concurrent multi-process save lock: UI guard prevents same-process switch overlap, not two independent game instances. Archives are append-only and grow; no retention deletion policy authorized. Legacy entries lacking UUID use serialized-summary deduplication, so byte-identical legacy summaries cannot be distinguished. Immutable snapshots are local data, not anti-cheat protection.

Team integration must validate tutorial mode blocks reset, replay state is not lost by profile switch, and native settings continues to use GameUI overlays. Actual Unity/Windows checks required before team merge.
