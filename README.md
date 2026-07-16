# ActionFit Match Rival

Project-neutral engine for the scheduled MatchRival event. The package owns the event window,
stage and Easy/Hard transitions, rival deadline calculation, schema-versioned state, catalog
pins, and idempotent round/box reward transactions.

## Install

Add the public Git UPM package to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.actionfit.match-rival": "https://github.com/ActionFit-Editor/MatchRival.git#0.1.3"
  }
}
```

## Integration

Construct `MatchRivalEngine` with:

- an `IContentStateStore` and optional `IFlushableContentStateStore`;
- an atomic `IContentRewardService`;
- catalog, clock, random, schedule, unlock, progress-curve, opponent, and analytics adapters.

The engine is independent from Cat Merge `Main`, `GameEvents`, `DataStore`, Addressables,
analytics SDKs, popup queues, and project UI. A consuming project converts its tables into a
`MatchRivalCatalog` and keeps its UI and assets outside this package.

For a reusable UI Foundation presentation, install the optional public
`com.actionfit.match-rival.ui` package. It depends on this engine and never reverses the dependency.
Cat Merge production theme assets and popup compatibility wrappers remain project-owned.

## Durable rewards

Result and box claims persist the exact reward snapshot and a transaction ID containing the
event end ticks and stage before calling `GrantOnce`. On restore, an already-started transaction
is recovered before normal match or timeout handling.

## Validation

Run the EditMode assembly `com.actionfit.match-rival.Editor.Tests`. Publishing, repository
creation, Git tags, and catalog registration remain manual Custom Package Manager actions.
