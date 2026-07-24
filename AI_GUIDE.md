# AI Guide - ActionFit Match Rival

This guide is shipped with the package so an AI assistant can preserve MatchRival state,
catalog, and reward-safety contracts in consuming projects.

## Package Identity

- Package ID: `com.actionfit.match-rival`
- Display name: ActionFit Match Rival
- Repository: `https://github.com/ActionFit-Editor/MatchRival.git`
- Repository visibility: Public
- Current package version at generation time: `0.2.5`
- Unity version: `6000.2`
- Runtime dependencies: `com.actionfit.content-core@0.2.3` and `com.actionfit.time@1.0.4`

## Purpose

The package owns the scheduled event window, stage 1-10 state, Easy/Hard transition, rival
deadline and curve progress calculation, bean clamping, schema-versioned persistence, pinned
catalog identity, and idempotent round/box reward recovery.

It does not own project event buses, `DataStore`, `DatabaseManager`, generated SO loading,
`TimeProvider`, project inventory mutation, analytics SDKs, UI, Addressables, localization,
audio, prefabs, or migration reads from Cat Merge legacy keys. Optional reusable presentation
lives in the separate `com.actionfit.match-rival.ui` package; never add UI Foundation,
ReferenceBinding, UGUI, or consuming-project assets to this engine.

## Project Router Registration

Requested router entry:

- `Packages/com.actionfit.match-rival/AI_GUIDE.md` - ActionFit Match Rival owns the reusable scheduled rival match state machine, pinned catalog state, and durable round/box reward recovery.

## Runtime Contracts

- `MatchRivalEngine` owns authoritative state transitions and exposes immutable state reads.
- `IMatchRivalCatalogResolver` must resolve the pinned version and balance revision for an
  active event. Unknown pins fail explicitly instead of silently switching balance.
- `IMatchRivalSchedulePolicy` preserves the consuming game's local tick epoch and active-window
  calculation. An empty policy is the kill switch. The engine consumes `ActionFit.Time.IClock`
  plus an explicit calendar and optional `calendarDayBoundaryOffset`. Clock source and calendar
  policy are independent. A signed offset strictly between -24 and +24 hours moves logical midnight by
  subtracting it for date evaluation and adding it before UTC deadline conversion.
- New deadlines use UTC ticks with schema/basis metadata. Imported active local ticks keep the
  configured legacy calendar until the active event ends and are never relabeled as UTC.
- `ConfigureCalendar` may replace the new-event zone and signed boundary after a device-zone refresh;
  it never rewrites active deadline ticks or their persisted basis.
- New-event availability, expected duration, and window end always use the constructor-injected
  new-event calendar even when an inactive imported snapshot still records a legacy time basis.
  Existing constructors use zero offset. A rejected start returns `false` without changing the
  snapshot basis or writing state, and active legacy numeric ticks never inherit the new offset.
- Canonical balance CSVs ship under `Data/CSV/`. `MatchRivalCatalogFactory` builds the complete
  standalone catalog and schedule for the default and `Reward` segments directly from
  caller-provided CSV text without `AssetDatabase`, CSV Importer, generated Row/Table types, or
  project Table SOs. Empty, malformed, duplicate, or unsupported input fails closed. Generated
  outputs remain under `Assets/_Data/_MatchRival/`; verify their parity with
  `Assets/_Project/Content/Tests/Editor/ContentCatalogImportedSoParityTests.cs`. Machine-readable
  source and test evidence lives in `Documentation~/StandaloneCatalogEvidence.json`.
- `IMatchRivalProgressCurveProvider` evaluates the project-owned rival curves without moving
  animation assets into the package.
- `IContentStateStore` stores one opaque schema-versioned JSON snapshot. Critical event, match,
  result, migration, and reward boundaries flush `IFlushableContentStateStore` when available.
- `IContentRewardService.IsAvailable` must be true before pending reward state is created.

## Durable Reward Order

Preserve this order:

1. Persist the pending result/box reward snapshot.
2. Persist the stable transaction ID before inventory mutation.
3. Call `HasGranted`, `GrantOnce`, then verify `HasGranted`.
4. Advance stage/claim state and clear pending transaction data.
5. Persist and flush the finalized state.

Transaction IDs include content ID, event end ticks, stage, and reward kind. Never reconstruct
an already-started pending transaction from a newer catalog.

## Migration And Compatibility

Project migration must restore valid package JSON first, preserve a corrupt backup before
deleting only the invalid runtime snapshot, import legacy fields only through
`MatchRivalImportState`, flush the package snapshot, and then write the migration marker. Keep
legacy values during the first rollout for rollback.

Package schema 1 snapshots are valid legacy input. Restore upgrades them to schema 2 with
`LegacyCalendarTicks` without changing `eventEndTicks` or `matchStartTicks`, validates the pinned
catalog, and durably saves the upgraded snapshot before pending reward recovery. Unknown older or
future schemas remain fail-closed and must not be overwritten.

Do not move or rewrite project prefabs, GUIDs, Addressable keys, analytics keys, or the separate
`match_rival_bot` root as part of package work.

`com.actionfit.match-rival.ui` may consume public engine reads and commands. Dependency direction
is project -> optional UI -> engine -> Content Core. The engine must not reference the UI package.

## Testing

Run `com.actionfit.match-rival.Editor.Tests`. Preserve coverage for schedule windows and kill
switches, stage and Easy/Hard transitions, bean clamping, rival timeout, catalog-pin failures,
legacy import, corrupt/future schema rejection, reward crash recovery, duplicate claims, and box
claim reset across event instances.

## Package Tools Menu

- Unity menu root: `Tools/Package/ActionFit Match Rival/`.
- `README` opens the installed package README.
- The package has no settings ScriptableObject.

## Release Notes

- Publishing is manual through Custom Package Manager.
- Remote tags are immutable; check them before reusing a version.
- Update `package.json`, this guide, README, tests, and PackageInfo together for behavior changes.
