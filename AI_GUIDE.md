# AI Guide - ActionFit Match Rival

This guide is shipped with the package so an AI assistant can preserve MatchRival state,
catalog, and reward-safety contracts in consuming projects.

## Package Identity

- Package ID: `com.actionfit.match-rival`
- Display name: ActionFit Match Rival
- Repository: `https://github.com/ActionFit-Editor/MatchRival.git`
- Repository visibility: Public
- Current package version at generation time: `0.1.3`
- Unity version: `6000.2`
- Runtime dependency: `com.actionfit.content-core@0.2.1`

## Purpose

The package owns the scheduled event window, stage 1-10 state, Easy/Hard transition, rival
deadline and curve progress calculation, bean clamping, schema-versioned persistence, pinned
catalog identity, and idempotent round/box reward recovery.

It does not own project event buses, `DataStore`, `DatabaseManager`, CSV/SO loading,
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
  calculation. An empty policy is the kill switch.
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
