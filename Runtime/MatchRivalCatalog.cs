using System;
using System.Collections.Generic;
using ActionFit.Content;

namespace ActionFit.MatchRival
{
    public sealed class MatchRivalDifficulty
    {
        public MatchRivalDifficulty(
            int stage,
            float easyMinSeconds,
            float easyMaxSeconds,
            float hardMinSeconds,
            float hardMaxSeconds,
            int requiredBeans)
        {
            if (stage < MatchRivalEngine.MinStage || stage > MatchRivalEngine.MaxStage)
                throw new ArgumentOutOfRangeException(nameof(stage));
            if (easyMinSeconds <= 0f || easyMaxSeconds < easyMinSeconds)
                throw new ArgumentOutOfRangeException(nameof(easyMinSeconds));
            if (hardMinSeconds <= 0f || hardMaxSeconds < hardMinSeconds)
                throw new ArgumentOutOfRangeException(nameof(hardMinSeconds));
            if (requiredBeans <= 0) throw new ArgumentOutOfRangeException(nameof(requiredBeans));

            Stage = stage;
            EasyMinSeconds = easyMinSeconds;
            EasyMaxSeconds = easyMaxSeconds;
            HardMinSeconds = hardMinSeconds;
            HardMaxSeconds = hardMaxSeconds;
            RequiredBeans = requiredBeans;
        }

        public int Stage { get; }
        public float EasyMinSeconds { get; }
        public float EasyMaxSeconds { get; }
        public float HardMinSeconds { get; }
        public float HardMaxSeconds { get; }
        public int RequiredBeans { get; }
    }

    public sealed class MatchRivalRoundRewards
    {
        public MatchRivalRoundRewards(
            int stage,
            IReadOnlyList<ContentReward> winRewards,
            IReadOnlyList<ContentReward> loseRewards)
        {
            if (stage < MatchRivalEngine.MinStage || stage > MatchRivalEngine.MaxStage)
                throw new ArgumentOutOfRangeException(nameof(stage));
            Stage = stage;
            WinRewards = CopyRewards(winRewards, nameof(winRewards));
            LoseRewards = CopyRewards(loseRewards, nameof(loseRewards));
        }

        public int Stage { get; }
        public IReadOnlyList<ContentReward> WinRewards { get; }
        public IReadOnlyList<ContentReward> LoseRewards { get; }

        private static IReadOnlyList<ContentReward> CopyRewards(
            IReadOnlyList<ContentReward> rewards,
            string parameterName)
        {
            if (rewards == null || rewards.Count == 0)
                throw new ArgumentException("At least one reward is required.", parameterName);
            var copy = new List<ContentReward>(rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                ContentReward reward = rewards[index]
                    ?? throw new ArgumentException("Rewards must not contain null.", parameterName);
                copy.Add(new ContentReward(reward.RewardId, reward.Amount));
            }

            return copy;
        }
    }

    public sealed class MatchRivalBoxRewards
    {
        public MatchRivalBoxRewards(int stage, IReadOnlyList<ContentReward> rewards)
        {
            if (stage < MatchRivalEngine.MinStage || stage > MatchRivalEngine.MaxStage)
                throw new ArgumentOutOfRangeException(nameof(stage));
            if (rewards == null || rewards.Count == 0)
                throw new ArgumentException("At least one box reward is required.", nameof(rewards));

            Stage = stage;
            var copy = new List<ContentReward>(rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                ContentReward reward = rewards[index]
                    ?? throw new ArgumentException("Rewards must not contain null.", nameof(rewards));
                copy.Add(new ContentReward(reward.RewardId, reward.Amount));
            }

            Rewards = copy;
        }

        public int Stage { get; }
        public IReadOnlyList<ContentReward> Rewards { get; }
    }

    public sealed class MatchRivalCatalog
    {
        private readonly Dictionary<int, MatchRivalDifficulty> _difficulties = new();
        private readonly Dictionary<int, int> _orderBeans = new();
        private readonly Dictionary<int, MatchRivalRoundRewards> _roundRewards = new();
        private readonly Dictionary<int, MatchRivalBoxRewards> _boxRewards = new();

        public MatchRivalCatalog(
            string catalogVersion,
            string balanceRevision,
            IEnumerable<MatchRivalDifficulty> difficulties,
            IEnumerable<KeyValuePair<int, int>> orderBeans,
            IEnumerable<MatchRivalRoundRewards> roundRewards,
            IEnumerable<MatchRivalBoxRewards> boxRewards)
        {
            CatalogVersion = ValidateId(catalogVersion, nameof(catalogVersion));
            BalanceRevision = ValidateId(balanceRevision, nameof(balanceRevision));
            AddUnique(difficulties, _difficulties, item => item.Stage, nameof(difficulties));
            AddOrderBeans(orderBeans);
            AddUnique(roundRewards, _roundRewards, item => item.Stage, nameof(roundRewards));
            if (boxRewards != null)
                AddUnique(boxRewards, _boxRewards, item => item.Stage, nameof(boxRewards));

            for (int stage = MatchRivalEngine.MinStage; stage <= MatchRivalEngine.MaxStage; stage++)
            {
                if (!_difficulties.ContainsKey(stage))
                    throw new ArgumentException($"Difficulty is missing for stage {stage}.", nameof(difficulties));
                if (!_roundRewards.ContainsKey(stage))
                    throw new ArgumentException($"Round rewards are missing for stage {stage}.", nameof(roundRewards));
            }
        }

        public string CatalogVersion { get; }
        public string BalanceRevision { get; }

        public MatchRivalDifficulty GetDifficulty(int stage)
        {
            if (!_difficulties.TryGetValue(stage, out MatchRivalDifficulty difficulty))
                throw new KeyNotFoundException($"Difficulty is missing for stage {stage}.");
            return difficulty;
        }

        public MatchRivalRoundRewards GetRoundRewards(int stage)
        {
            if (!_roundRewards.TryGetValue(stage, out MatchRivalRoundRewards rewards))
                throw new KeyNotFoundException($"Round rewards are missing for stage {stage}.");
            return rewards;
        }

        public bool TryGetBoxRewards(int stage, out MatchRivalBoxRewards rewards)
        {
            return _boxRewards.TryGetValue(stage, out rewards);
        }

        public int GetOrderBeans(int orderItemValue)
        {
            return _orderBeans.TryGetValue(orderItemValue, out int amount) ? amount : 0;
        }

        private void AddOrderBeans(IEnumerable<KeyValuePair<int, int>> orderBeans)
        {
            if (orderBeans == null) throw new ArgumentNullException(nameof(orderBeans));
            foreach (KeyValuePair<int, int> entry in orderBeans)
            {
                if (entry.Key <= 0 || entry.Value < 0)
                    throw new ArgumentOutOfRangeException(nameof(orderBeans));
                if (!_orderBeans.TryAdd(entry.Key, entry.Value))
                    throw new ArgumentException($"Duplicate order item value {entry.Key}.", nameof(orderBeans));
            }
        }

        private static void AddUnique<T>(
            IEnumerable<T> values,
            Dictionary<int, T> target,
            Func<T, int> getKey,
            string parameterName)
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            foreach (T value in values)
            {
                if (value == null) throw new ArgumentException("Entries must not contain null.", parameterName);
                int key = getKey(value);
                if (!target.TryAdd(key, value))
                    throw new ArgumentException($"Duplicate stage {key}.", parameterName);
            }
        }

        private static string ValidateId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value must not be empty or whitespace.", parameterName);
            return value;
        }
    }
}
