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
        }

        private sealed class MemoryStore : IContentStateStore, IFlushableContentStateStore
        {
            public string Json;
            public int SaveCount;
            public int FailOnSaveNumber;

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

            public void Flush() { }
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
            public bool IsEnabled => Enabled;
            public bool IsActiveDay(DayOfWeek dayOfWeek) => true;
            public DateTime GetActiveWindowEnd(DateTime now) => now.Date.AddDays(2);
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
