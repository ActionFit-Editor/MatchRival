using System;
using System.Collections.Generic;
using ActionFit.Content;
using NUnit.Framework;

namespace ActionFit.MatchRival.Tests
{
    public sealed class MatchRivalEngineTests
    {
        private static readonly DateTime Saturday = new DateTime(2026, 7, 18, 12, 0, 0);

        [Test]
        public void EndEvent_PreservesWindowAndBlocksRestartUntilItExpires()
        {
            TestContext context = CreateContext();

            Assert.That(context.Engine.TryStartEvent(), Is.True);
            long eventEndTicks = context.Engine.State.EventEndTicks;
            context.Engine.EndEvent();

            Assert.That(context.Engine.IsEventStarted, Is.False);
            Assert.That(context.Engine.State.EventEndTicks, Is.EqualTo(eventEndTicks));
            Assert.That(context.Engine.IsEventActive, Is.True);
            Assert.That(context.Engine.TryStartEvent(), Is.False);

            context.Clock.Advance(TimeSpan.FromDays(3));
            Assert.That(context.Engine.TryStartEvent(), Is.True);
        }

        [Test]
        public void NewEventCalendar_DoesNotUseInactiveLegacyStateBasis()
        {
            TimeZoneInfo legacyCalendar = TimeZoneInfo.CreateCustomTimeZone(
                "MatchRival.Tests.Legacy+09",
                TimeSpan.FromHours(9),
                "MatchRival Tests Legacy +09",
                "MatchRival Tests Legacy +09");

            TestContext rejectedContext = CreateContext();
            rejectedContext.Schedule.WeekendOnly = true;
            var rejectedClock = new ActionFit.Time.ManualClock(
                new DateTime(2026, 7, 17, 20, 0, 0, DateTimeKind.Utc));
            MatchRivalEngine rejected = rejectedContext.CreateEngine(
                rejectedClock,
                TimeZoneInfo.Utc,
                legacyCalendar);
            Assert.That(rejected.ImportStateIfEmpty(new MatchRivalImportState()), Is.True);
            int rejectedSaveCount = rejectedContext.Store.SaveCount;

            Assert.That(rejected.IsEventDay, Is.False);
            Assert.That(rejected.ExpectedRemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(rejected.TryStartEvent(), Is.False);
            Assert.That(rejected.State.TimeBasis, Is.EqualTo(MatchRivalTimeBasis.LegacyCalendarTicks));
            Assert.That(rejectedContext.Store.SaveCount, Is.EqualTo(rejectedSaveCount));

            TestContext acceptedContext = CreateContext();
            acceptedContext.Schedule.WeekendOnly = true;
            var acceptedClock = new ActionFit.Time.ManualClock(
                new DateTime(2026, 7, 19, 20, 0, 0, DateTimeKind.Utc));
            MatchRivalEngine accepted = acceptedContext.CreateEngine(
                acceptedClock,
                TimeZoneInfo.Utc,
                legacyCalendar);
            Assert.That(accepted.ImportStateIfEmpty(new MatchRivalImportState()), Is.True);

            Assert.That(accepted.IsEventDay, Is.True);
            Assert.That(accepted.ExpectedRemainingTime, Is.EqualTo(TimeSpan.FromHours(4)));
            Assert.That(accepted.TryStartEvent(), Is.True);
            Assert.That(accepted.State.TimeBasis, Is.EqualTo(MatchRivalTimeBasis.UtcTicks));
            Assert.That(
                accepted.State.EventEndTicks,
                Is.EqualTo(new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc).Ticks));
        }

        [Test]
        public void DeviceLocalCalendar_WithNineHourBoundary_ChangesDayAtLocalNine()
        {
            TimeZoneInfo deviceLocalTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                "MatchRival.Tests.DeviceLocal.Boundary+09",
                TimeSpan.FromHours(9d),
                "Match Rival Tests Device Local Boundary +09",
                "Match Rival Tests Device Local Boundary +09");
            TimeSpan boundaryOffset = TimeSpan.FromHours(9d);
            TestContext beforeContext = CreateContext();
            beforeContext.Schedule.WeekendOnly = true;
            TestContext atContext = CreateContext();
            atContext.Schedule.WeekendOnly = true;
            MatchRivalEngine beforeBoundary = beforeContext.CreateEngine(
                new ActionFit.Time.ManualClock(
                    new DateTime(2026, 7, 17, 23, 59, 0, DateTimeKind.Utc)),
                deviceLocalTimeZone,
                TimeZoneInfo.Utc,
                boundaryOffset);
            MatchRivalEngine atBoundary = atContext.CreateEngine(
                new ActionFit.Time.ManualClock(
                    new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc)),
                deviceLocalTimeZone,
                TimeZoneInfo.Utc,
                boundaryOffset);

            Assert.That(beforeBoundary.IsEventDay, Is.False);
            Assert.That(beforeBoundary.TryStartEvent(), Is.False);
            Assert.That(atBoundary.IsEventDay, Is.True);
            Assert.That(atBoundary.TryStartEvent(), Is.True);
            Assert.That(
                atBoundary.State.EventEndTicks,
                Is.EqualTo(new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc).Ticks));
            Assert.That(atBoundary.EventRemainingTime, Is.EqualTo(TimeSpan.FromHours(48)));
        }

        [Test]
        public void UtcCalendar_WithNegativeNineBoundary_ChangesDayAtUtcFifteen()
        {
            TimeSpan boundaryOffset = TimeSpan.FromHours(-9d);
            TestContext beforeContext = CreateContext();
            beforeContext.Schedule.WeekendOnly = true;
            TestContext atContext = CreateContext();
            atContext.Schedule.WeekendOnly = true;
            MatchRivalEngine beforeBoundary = beforeContext.CreateEngine(
                new ActionFit.Time.ManualClock(
                    new DateTime(2026, 7, 17, 14, 59, 0, DateTimeKind.Utc)),
                TimeZoneInfo.Utc,
                TimeZoneInfo.Utc,
                boundaryOffset);
            MatchRivalEngine atBoundary = atContext.CreateEngine(
                new ActionFit.Time.ManualClock(
                    new DateTime(2026, 7, 17, 15, 0, 0, DateTimeKind.Utc)),
                TimeZoneInfo.Utc,
                TimeZoneInfo.Utc,
                boundaryOffset);

            Assert.That(beforeBoundary.IsEventDay, Is.False);
            Assert.That(beforeBoundary.TryStartEvent(), Is.False);
            Assert.That(atBoundary.IsEventDay, Is.True);
            Assert.That(atBoundary.TryStartEvent(), Is.True);
            Assert.That(
                atBoundary.State.EventEndTicks,
                Is.EqualTo(new DateTime(2026, 7, 19, 15, 0, 0, DateTimeKind.Utc).Ticks));
            Assert.That(atBoundary.EventRemainingTime, Is.EqualTo(TimeSpan.FromHours(48)));
        }

        [Test]
        public void ConfigureCalendar_RuntimeZoneChangeUsesNewPolicyForNewEvent()
        {
            TestContext context = CreateContext();
            context.Schedule.WeekendOnly = true;
            TimeZoneInfo positiveNine = TimeZoneInfo.CreateCustomTimeZone(
                "MatchRival.Tests.Runtime.Positive09",
                TimeSpan.FromHours(9),
                "Match Rival Tests Runtime Positive 09",
                "Match Rival Tests Runtime Positive 09");
            MatchRivalEngine engine = context.CreateEngine(
                new ActionFit.Time.ManualClock(
                    new DateTime(2026, 7, 17, 20, 0, 0, DateTimeKind.Utc)),
                TimeZoneInfo.Utc,
                TimeZoneInfo.Utc,
                TimeSpan.Zero);

            Assert.That(engine.IsEventDay, Is.False);

            engine.ConfigureCalendar(positiveNine, TimeSpan.Zero);

            Assert.That(engine.IsEventDay, Is.True);
            Assert.That(engine.TryStartEvent(), Is.True);
            long activeDeadline = engine.State.EventEndTicks;
            engine.ConfigureCalendar(TimeZoneInfo.Utc, TimeSpan.FromHours(-9));
            Assert.That(engine.State.EventEndTicks, Is.EqualTo(activeDeadline));
        }

        [Test]
        public void EndEvent_FinalizesPreparedRoundRewardBeforeReset()
        {
            TestContext context = CreateContext();
            StartMatch(context.Engine);
            context.Engine.AddBeans(10);
            context.Engine.PrepareResultReward(MatchRivalResult.Win);

            context.Engine.EndEvent();

            Assert.That(context.Rewards.Balance("Energy"), Is.EqualTo(10));
            Assert.That(context.Engine.IsEventStarted, Is.False);
            Assert.That(context.Engine.IsMatchActive, Is.False);
            Assert.That(context.Engine.PendingResult, Is.EqualTo(MatchRivalResult.None));
        }

        [Test]
        public void Win_ClampsBeansAndAdvancesToHardNextStage()
        {
            TestContext context = CreateContext();
            StartMatch(context.Engine);

            Assert.That(context.Engine.GetOrderBeans(new[] { 3, 3 }), Is.EqualTo(8));
            Assert.That(context.Engine.AddBeans(999), Is.True);
            Assert.That(context.Engine.CollectedBeans, Is.EqualTo(10));
            Assert.That(context.Engine.CurrentResult, Is.EqualTo(MatchRivalResult.Win));
            Assert.That(context.Engine.PrepareResultReward(MatchRivalResult.Win), Is.True);

            MatchRivalRoundClaimResult claim = context.Engine.ClaimPendingResultReward();

            Assert.That(claim.Succeeded, Is.True);
            Assert.That(context.Engine.Stage, Is.EqualTo(2));
            Assert.That(context.Engine.IsHard, Is.True);
            Assert.That(context.Engine.IsMatchActive, Is.False);
            Assert.That(context.Rewards.GrantCalls, Is.EqualTo(1));
            Assert.That(context.Rewards.Balance("Energy"), Is.EqualTo(10));
        }

        [Test]
        public void Lose_KeepsStageAndReturnsToEasy()
        {
            TestContext context = CreateContext();
            StartMatch(context.Engine);
            context.Clock.Advance(TimeSpan.FromSeconds(61));

            Assert.That(context.Engine.CurrentResult, Is.EqualTo(MatchRivalResult.Lose));
            Assert.That(context.Engine.PrepareResultReward(MatchRivalResult.Lose), Is.True);
            Assert.That(context.Engine.ClaimPendingResultReward().Succeeded, Is.True);
            Assert.That(context.Engine.Stage, Is.EqualTo(1));
            Assert.That(context.Engine.IsHard, Is.False);
            Assert.That(context.Rewards.Balance("Dia"), Is.EqualTo(1));
        }

        [Test]
        public void PendingRoundTransaction_RestoreFinalizesWithoutDuplicateGrant()
        {
            TestContext context = CreateContext();
            StartMatch(context.Engine);
            context.Engine.AddBeans(10);
            context.Engine.PrepareResultReward(MatchRivalResult.Win);
            context.Store.FailOnSaveNumber = context.Store.SaveCount + 2;

            Assert.Throws<InvalidOperationException>(() => context.Engine.ClaimPendingResultReward());
            Assert.That(context.Rewards.GrantCalls, Is.EqualTo(1));

            context.Store.FailOnSaveNumber = 0;
            MatchRivalEngine restored = context.CreateEngine();
            restored.Restore();

            Assert.That(restored.Stage, Is.EqualTo(2));
            Assert.That(restored.IsMatchActive, Is.False);
            Assert.That(context.Rewards.GrantCalls, Is.EqualTo(1));
        }

        [Test]
        public void BoxReward_IsGrantedOncePerEventInstance()
        {
            TestContext context = CreateContext();
            StartMatch(context.Engine);
            context.Engine.AddBeans(10);
            context.Engine.PrepareResultReward(MatchRivalResult.Win);
            context.Engine.ClaimPendingResultReward();
            Assert.That(context.Engine.StartMatch(), Is.True);
            context.Engine.AddBeans(20);

            Assert.That(context.Engine.ClaimBoxReward(2), Is.True);
            Assert.That(context.Engine.ClaimBoxReward(2), Is.False);
            Assert.That(context.Engine.IsBoxRewardClaimed(2), Is.True);
            Assert.That(context.Rewards.Balance("Gold"), Is.EqualTo(20));

            context.Engine.EndEvent();
            context.Clock.Advance(TimeSpan.FromDays(3));
            Assert.That(context.Engine.TryStartEvent(), Is.True);
            Assert.That(context.Engine.IsBoxRewardClaimed(2), Is.False);
        }

        [Test]
        public void DisabledSchedule_IsImmediateKillSwitch()
        {
            TestContext context = CreateContext();
            StartMatch(context.Engine);
            context.Schedule.Enabled = false;

            context.Engine.EvaluateTimeout();

            Assert.That(context.Engine.IsEventStarted, Is.False);
            Assert.That(context.Engine.IsMatchActive, Is.False);
            Assert.That(context.Engine.PendingEnd, Is.False);
            Assert.That(context.Engine.State.EventEndTicks, Is.Zero);
        }

        [Test]
        public void ImportAndRestore_RejectExistingStateFutureSchemaAndUnknownCatalog()
        {
            TestContext context = CreateContext();
            var import = new MatchRivalImportState
            {
                Stage = 3,
                IsHard = true,
                EventStarted = true,
                EventEndTicks = Saturday.AddDays(2).Ticks,
                ClaimedBoxStages = new[] { 2 },
                TutorialDone = true,
            };

            Assert.That(context.Engine.ImportStateIfEmpty(import), Is.True);
            Assert.That(context.Engine.ImportStateIfEmpty(import), Is.False);
            Assert.That(context.Engine.Stage, Is.EqualTo(3));
            Assert.That(context.Engine.TutorialDone, Is.True);

            context.Store.Json = "{\"schemaVersion\":99}";
            Assert.Throws<NotSupportedException>(() => context.CreateEngine().Restore());

            context.Store.Json = null;
            MatchRivalEngine engine = context.CreateEngine();
            engine.TryStartEvent();
            context.Store.Json = context.Store.Json.Replace("balance-v1", "future-v1");
            Assert.Throws<InvalidOperationException>(() => context.CreateEngine().Restore());
        }

        [Test]
        public void Restore_SchemaOne_PreservesLegacyTicksAndPersistsSchemaTwo()
        {
            TestContext context = CreateContext();
            StartMatch(context.Engine);
            long eventEndTicks = context.Engine.State.EventEndTicks;
            long matchStartTicks = context.Engine.MatchStartTicks;
            context.Store.Json = context.Store.Json.Replace(
                "\"schemaVersion\":2,\"timeBasis\":1,",
                "\"schemaVersion\":1,");
            context.Store.SaveCount = 0;
            context.Store.FlushCount = 0;

            MatchRivalEngine restored = context.CreateEngine();
            int stateChangedCount = 0;
            restored.StateChanged += _ => stateChangedCount++;

            restored.Restore();

            Assert.That(restored.State.SchemaVersion, Is.EqualTo(2));
            Assert.That(restored.State.TimeBasis, Is.EqualTo(MatchRivalTimeBasis.LegacyCalendarTicks));
            Assert.That(restored.State.EventEndTicks, Is.EqualTo(eventEndTicks));
            Assert.That(restored.MatchStartTicks, Is.EqualTo(matchStartTicks));
            Assert.That(context.Store.Json, Does.Contain("\"schemaVersion\":2"));
            Assert.That(context.Store.Json, Does.Contain("\"timeBasis\":0"));
            Assert.That(context.Store.SaveCount, Is.EqualTo(1));
            Assert.That(context.Store.FlushCount, Is.EqualTo(1));
            Assert.That(stateChangedCount, Is.EqualTo(1));
        }

        [Test]
        public void Restore_UnsupportedOldSchema_RejectsWithoutOverwritingState()
        {
            TestContext context = CreateContext();
            const string unsupportedJson = "{\"schemaVersion\":0}";
            context.Store.Json = unsupportedJson;

            Assert.Throws<NotSupportedException>(() => context.CreateEngine().Restore());

            Assert.That(context.Store.Json, Is.EqualTo(unsupportedJson));
            Assert.That(context.Store.SaveCount, Is.Zero);
            Assert.That(context.Store.FlushCount, Is.Zero);
        }

        [Test]
        public void Restore_SchemaOneMigrationSaveFails_DoesNotGrantPendingReward()
        {
            TestContext context = CreateContext();
            StartMatch(context.Engine);
            context.Engine.AddBeans(context.Engine.RequiredBeans);
            Assert.That(context.Engine.PrepareResultReward(MatchRivalResult.Win), Is.True);
            string transactionId =
                $"tests/match-rival/event/{context.Engine.State.EventEndTicks}/round/1/win";
            context.Store.Json = context.Store.Json
                .Replace("\"schemaVersion\":2,\"timeBasis\":1,", "\"schemaVersion\":1,")
                .Replace("\"pendingTransactionId\":\"\"", $"\"pendingTransactionId\":\"{transactionId}\"");
            context.Store.SaveCount = 0;
            context.Store.FlushCount = 0;
            context.Store.FailOnSaveNumber = 1;

            Assert.Throws<InvalidOperationException>(() => context.CreateEngine().Restore());

            Assert.That(context.Rewards.GrantCalls, Is.Zero);
            Assert.That(context.Store.Json, Does.Contain("\"schemaVersion\":1"));

            context.Store.FailOnSaveNumber = 0;
            context.CreateEngine().Restore();

            Assert.That(context.Rewards.GrantCalls, Is.EqualTo(1));
            Assert.That(context.Rewards.Balance("Energy"), Is.EqualTo(10));
            Assert.That(context.Store.Json, Does.Contain("\"schemaVersion\":2"));
            Assert.That(context.Store.Json, Does.Contain("\"timeBasis\":0"));
        }

        private static void StartMatch(MatchRivalEngine engine)
        {
            Assert.That(engine.TryStartEvent(), Is.True);
            Assert.That(engine.StartMatch(), Is.True);
        }

        private static TestContext CreateContext()
        {
            return new TestContext();
        }

        private sealed class TestContext
        {
            public readonly MemoryStore Store = new();
            public readonly MemoryRewards Rewards = new();
            public readonly ManualClock Clock = new(Saturday);
            public readonly TestSchedule Schedule = new();
            public readonly TestCatalogResolver Catalog = new();

            public TestContext()
            {
                Engine = CreateEngine();
            }

            public MatchRivalEngine Engine { get; }

            public MatchRivalEngine CreateEngine()
            {
                return new MatchRivalEngine(
                    Store,
                    Rewards,
                    Catalog,
                    Clock,
                    new MinimumRandom(),
                    new LinearCurve(),
                    new TestOpponentProvider(),
                    "tests/match-rival",
                    new TestAccess(),
                    Schedule);
            }

            public MatchRivalEngine CreateEngine(
                ActionFit.Time.IClock clock,
                TimeZoneInfo calendarTimeZone,
                TimeZoneInfo legacyCalendarTimeZone,
                TimeSpan? calendarDayBoundaryOffset = null)
            {
                return new MatchRivalEngine(
                    Store,
                    Rewards,
                    Catalog,
                    clock,
                    calendarTimeZone,
                    legacyCalendarTimeZone,
                    new MinimumRandom(),
                    new LinearCurve(),
                    new TestOpponentProvider(),
                    "tests/match-rival",
                    new TestAccess(),
                    Schedule,
                    null,
                    calendarDayBoundaryOffset ?? TimeSpan.Zero);
            }
        }

        private sealed class MemoryStore : IContentStateStore, IFlushableContentStateStore
        {
            public string Json;
            public int SaveCount;
            public int FailOnSaveNumber;
            public int FlushCount;

            public bool TryLoad(string contentId, out string json)
            {
                json = Json;
                return !string.IsNullOrWhiteSpace(json);
            }

            public void Save(string contentId, string json)
            {
                SaveCount++;
                if (SaveCount == FailOnSaveNumber)
                    throw new InvalidOperationException("Injected save failure.");
                Json = json;
            }

            public void Delete(string contentId)
            {
                Json = null;
            }

            public void Flush()
            {
                FlushCount++;
            }
        }

        private sealed class MemoryRewards : IContentRewardService
        {
            private readonly HashSet<string> _transactions = new(StringComparer.Ordinal);
            private readonly Dictionary<string, long> _balances = new(StringComparer.Ordinal);

            public bool IsAvailable => true;
            public int GrantCalls { get; private set; }

            public bool HasGranted(string transactionId)
            {
                return _transactions.Contains(transactionId);
            }

            public bool GrantOnce(string transactionId, IReadOnlyList<ContentReward> rewards)
            {
                if (!_transactions.Add(transactionId)) return false;
                GrantCalls++;
                foreach (ContentReward reward in rewards)
                {
                    _balances.TryGetValue(reward.RewardId, out long current);
                    _balances[reward.RewardId] = current + reward.Amount;
                }
                return true;
            }

            public long Balance(string rewardId)
            {
                return _balances.TryGetValue(rewardId, out long amount) ? amount : 0L;
            }
        }

        private sealed class ManualClock : IMatchRivalClock
        {
            public ManualClock(DateTime now)
            {
                Now = now;
            }

            public DateTime Now { get; private set; }

            public void Advance(TimeSpan duration)
            {
                Now += duration;
            }
        }

        private sealed class TestSchedule : IMatchRivalSchedulePolicy
        {
            public bool Enabled = true;
            public bool WeekendOnly;
            public bool IsEnabled => Enabled;
            public bool IsActiveDay(DayOfWeek dayOfWeek) => !WeekendOnly
                || dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            public DateTime GetActiveWindowEnd(DateTime now)
            {
                if (!IsEnabled || !IsActiveDay(now.DayOfWeek))
                    throw new InvalidOperationException("Test schedule is not active.");
                if (!WeekendOnly) return now.Date.AddDays(2);

                for (int dayOffset = 1; dayOffset <= 7; dayOffset++)
                {
                    DateTime candidate = now.Date.AddDays(dayOffset);
                    if (!IsActiveDay(candidate.DayOfWeek)) return candidate;
                }

                return now.Date.AddDays(7);
            }
        }

        private sealed class TestAccess : IMatchRivalAccessPolicy
        {
            public bool IsAccessAllowed => true;
        }

        private sealed class MinimumRandom : IMatchRivalRandom
        {
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
            public float Range(float minInclusive, float maxInclusive) => minInclusive;
        }

        private sealed class LinearCurve : IMatchRivalProgressCurveProvider
        {
            public int CurveCount => 1;
            public float Evaluate(int curveIndex, float normalizedTime) => normalizedTime;
        }

        private sealed class TestOpponentProvider : IMatchRivalOpponentProvider
        {
            public MatchRivalOpponent CreateOpponent()
            {
                return new MatchRivalOpponent("bot", "Bot", "profile", "frame");
            }
        }

        private sealed class TestCatalogResolver : IMatchRivalCatalogResolver
        {
            public TestCatalogResolver()
            {
                var difficulties = new List<MatchRivalDifficulty>();
                var rounds = new List<MatchRivalRoundRewards>();
                for (int stage = MatchRivalEngine.MinStage; stage <= MatchRivalEngine.MaxStage; stage++)
                {
                    difficulties.Add(new MatchRivalDifficulty(stage, 60f, 120f, 30f, 60f, stage * 10));
                    rounds.Add(new MatchRivalRoundRewards(
                        stage,
                        new[] { new ContentReward("Energy", stage * 10) },
                        new[] { new ContentReward("Dia", stage) }));
                }

                Current = new MatchRivalCatalog(
                    "catalog-v1",
                    "balance-v1",
                    difficulties,
                    new[] { new KeyValuePair<int, int>(3, 4) },
                    rounds,
                    new[]
                    {
                        new MatchRivalBoxRewards(2, new[] { new ContentReward("Gold", 20) }),
                    });
            }

            public MatchRivalCatalog Current { get; }

            public bool TryResolve(
                string catalogVersion,
                string balanceRevision,
                out MatchRivalCatalog catalog)
            {
                bool matches = string.Equals(catalogVersion, Current.CatalogVersion, StringComparison.Ordinal)
                    && string.Equals(balanceRevision, Current.BalanceRevision, StringComparison.Ordinal);
                catalog = matches ? Current : null;
                return matches;
            }
        }
    }
}
