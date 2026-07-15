using System;

namespace ActionFit.MatchRival
{
    public interface IMatchRivalClock
    {
        DateTime Now { get; }
    }

    public interface IMatchRivalRandom
    {
        int Range(int minInclusive, int maxExclusive);
        float Range(float minInclusive, float maxInclusive);
    }

    public interface IMatchRivalAccessPolicy
    {
        bool IsAccessAllowed { get; }
    }

    public interface IMatchRivalSchedulePolicy
    {
        bool IsEnabled { get; }
        bool IsActiveDay(DayOfWeek dayOfWeek);
        DateTime GetActiveWindowEnd(DateTime now);
    }

    public interface IMatchRivalCatalogResolver
    {
        MatchRivalCatalog Current { get; }
        bool TryResolve(string catalogVersion, string balanceRevision, out MatchRivalCatalog catalog);
    }

    public interface IMatchRivalProgressCurveProvider
    {
        int CurveCount { get; }
        float Evaluate(int curveIndex, float normalizedTime);
    }

    public interface IMatchRivalOpponentProvider
    {
        MatchRivalOpponent CreateOpponent();
    }

    public interface IMatchRivalAnalyticsSink
    {
        void EventStarted(long eventEndTicks);
        void RoundStarted(int stage);
        void RoundEnded(int stage, MatchRivalResult result, int collectedBeans, int requiredBeans);
        void BoxRewardClaimed(int stage);
        void EventEnded(long eventEndTicks);
    }

    public sealed class SystemMatchRivalClock : IMatchRivalClock
    {
        public DateTime Now => DateTime.Now;
    }

    public sealed class SystemMatchRivalRandom : IMatchRivalRandom
    {
        private readonly Random _random = new Random();

        public int Range(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            }

            return minInclusive + (float)_random.NextDouble() * (maxInclusive - minInclusive);
        }
    }

    public sealed class AllowMatchRivalAccessPolicy : IMatchRivalAccessPolicy
    {
        public bool IsAccessAllowed => true;
    }

    public sealed class LinearMatchRivalProgressCurveProvider : IMatchRivalProgressCurveProvider
    {
        public int CurveCount => 1;

        public float Evaluate(int curveIndex, float normalizedTime)
        {
            if (curveIndex != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(curveIndex));
            }

            return Math.Max(0f, Math.Min(1f, normalizedTime));
        }
    }

    public sealed class DefaultMatchRivalOpponentProvider : IMatchRivalOpponentProvider
    {
        public MatchRivalOpponent CreateOpponent()
        {
            return new MatchRivalOpponent("default-rival", "Rival", string.Empty, string.Empty);
        }
    }

    public sealed class NullMatchRivalAnalyticsSink : IMatchRivalAnalyticsSink
    {
        public void EventStarted(long eventEndTicks) { }
        public void RoundStarted(int stage) { }
        public void RoundEnded(int stage, MatchRivalResult result, int collectedBeans, int requiredBeans) { }
        public void BoxRewardClaimed(int stage) { }
        public void EventEnded(long eventEndTicks) { }
    }
}
