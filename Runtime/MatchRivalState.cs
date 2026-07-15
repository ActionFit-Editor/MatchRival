using System;
using System.Collections.Generic;
using ActionFit.Content;
using UnityEngine;

namespace ActionFit.MatchRival
{
    public enum MatchRivalResult
    {
        None = 0,
        Win = 1,
        Lose = 2,
    }

    internal enum MatchRivalPendingRewardKind
    {
        None = 0,
        Round = 1,
        Box = 2,
    }

    public sealed class MatchRivalOpponent
    {
        public MatchRivalOpponent(string id, string displayName, string profileId, string frameId)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("ID is required.", nameof(id)) : id;
            DisplayName = displayName ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            FrameId = frameId ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string ProfileId { get; }
        public string FrameId { get; }
    }

    public sealed class MatchRivalImportState
    {
        public int Stage { get; set; } = MatchRivalEngine.MinStage;
        public bool IsHard { get; set; }
        public int CollectedBeans { get; set; }
        public MatchRivalResult PendingResult { get; set; }
        public bool ResultAnimationCompleted { get; set; }
        public int PreviousDisplayedBeans { get; set; }
        public int PreviousDisplayedRivalBeans { get; set; }
        public long MatchStartTicks { get; set; }
        public float RivalTimeLimitSeconds { get; set; }
        public int RivalCurveIndex { get; set; }
        public bool TutorialDone { get; set; }
        public bool EventStarted { get; set; }
        public bool PendingEnd { get; set; }
        public long EventEndTicks { get; set; }
        public IReadOnlyList<int> ClaimedBoxStages { get; set; } = Array.Empty<int>();
        public MatchRivalOpponent Opponent { get; set; }
    }

    public sealed class MatchRivalState
    {
        internal MatchRivalState(MatchRivalStateData data)
        {
            SchemaVersion = data.schemaVersion;
            CatalogVersion = data.catalogVersion ?? string.Empty;
            BalanceRevision = data.balanceRevision ?? string.Empty;
            EventStarted = data.eventStarted;
            PendingEnd = data.pendingEnd;
            EventEndTicks = data.eventEndTicks;
            Stage = data.stage;
            IsHard = data.isHard;
            CollectedBeans = data.collectedBeans;
            PendingResult = (MatchRivalResult)data.pendingResult;
            ResultAnimationCompleted = data.resultAnimationCompleted;
            PreviousDisplayedBeans = data.previousDisplayedBeans;
            PreviousDisplayedRivalBeans = data.previousDisplayedRivalBeans;
            MatchStartTicks = data.matchStartTicks;
            RivalTimeLimitSeconds = data.rivalTimeLimitSeconds;
            RivalCurveIndex = data.rivalCurveIndex;
            TutorialDone = data.tutorialDone;
            ClaimedBoxStages = new List<int>(data.claimedBoxStages);
            Opponent = data.opponent == null || string.IsNullOrWhiteSpace(data.opponent.id)
                ? null
                : new MatchRivalOpponent(
                    data.opponent.id,
                    data.opponent.displayName,
                    data.opponent.profileId,
                    data.opponent.frameId);
        }

        public int SchemaVersion { get; }
        public string CatalogVersion { get; }
        public string BalanceRevision { get; }
        public bool EventStarted { get; }
        public bool PendingEnd { get; }
        public long EventEndTicks { get; }
        public int Stage { get; }
        public bool IsHard { get; }
        public int CollectedBeans { get; }
        public MatchRivalResult PendingResult { get; }
        public bool ResultAnimationCompleted { get; }
        public int PreviousDisplayedBeans { get; }
        public int PreviousDisplayedRivalBeans { get; }
        public long MatchStartTicks { get; }
        public float RivalTimeLimitSeconds { get; }
        public int RivalCurveIndex { get; }
        public bool TutorialDone { get; }
        public IReadOnlyList<int> ClaimedBoxStages { get; }
        public MatchRivalOpponent Opponent { get; }
    }

    public sealed class MatchRivalRoundClaimResult
    {
        internal MatchRivalRoundClaimResult(bool succeeded, int stage, MatchRivalResult result)
        {
            Succeeded = succeeded;
            Stage = stage;
            Result = result;
        }

        public bool Succeeded { get; }
        public int Stage { get; }
        public MatchRivalResult Result { get; }
    }

    [Serializable]
    internal sealed class MatchRivalStateData
    {
        public int schemaVersion = MatchRivalStateSerializer.CurrentSchemaVersion;
        public string catalogVersion = string.Empty;
        public string balanceRevision = string.Empty;
        public bool eventStarted;
        public bool pendingEnd;
        public long eventEndTicks;
        public int stage = MatchRivalEngine.MinStage;
        public bool isHard;
        public int collectedBeans;
        public int pendingResult;
        public bool resultAnimationCompleted;
        public int previousDisplayedBeans;
        public int previousDisplayedRivalBeans;
        public long matchStartTicks;
        public float rivalTimeLimitSeconds;
        public int rivalCurveIndex;
        public bool tutorialDone;
        public List<int> claimedBoxStages = new();
        public MatchRivalOpponentData opponent;
        public int pendingRewardKind;
        public int pendingRewardStage;
        public string pendingTransactionId = string.Empty;
        public List<MatchRivalRewardData> pendingRewards = new();
    }

    [Serializable]
    internal sealed class MatchRivalOpponentData
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string profileId = string.Empty;
        public string frameId = string.Empty;
    }

    [Serializable]
    internal sealed class MatchRivalRewardData
    {
        public string rewardId = string.Empty;
        public long amount;
    }

    internal static class MatchRivalStateSerializer
    {
        internal const int CurrentSchemaVersion = 1;

        internal static string Serialize(MatchRivalStateData state)
        {
            Validate(state);
            return JsonUtility.ToJson(state);
        }

        internal static MatchRivalStateData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("MatchRival state JSON is empty.");

            MatchRivalStateData state;
            try
            {
                state = JsonUtility.FromJson<MatchRivalStateData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("MatchRival state JSON is malformed.", exception);
            }

            Validate(state);
            return state;
        }

        internal static MatchRivalStateData CreateDefault()
        {
            return new MatchRivalStateData();
        }

        internal static void Validate(MatchRivalStateData state)
        {
            if (state == null) throw new FormatException("MatchRival state is null.");
            if (state.schemaVersion != CurrentSchemaVersion)
                throw new NotSupportedException($"Unsupported MatchRival schema version {state.schemaVersion}.");
            if (state.stage < MatchRivalEngine.MinStage || state.stage > MatchRivalEngine.MaxStage)
                throw new FormatException("MatchRival stage is outside the supported range.");
            if (state.collectedBeans < 0 || state.previousDisplayedBeans < 0 || state.previousDisplayedRivalBeans < 0)
                throw new FormatException("MatchRival bean values must be non-negative.");
            if (state.eventEndTicks < 0 || state.matchStartTicks < 0 || state.rivalTimeLimitSeconds < 0f)
                throw new FormatException("MatchRival time values must be non-negative.");
            if (state.eventStarted && (state.eventEndTicks <= 0
                || string.IsNullOrWhiteSpace(state.catalogVersion)
                || string.IsNullOrWhiteSpace(state.balanceRevision)))
                throw new FormatException("An active MatchRival event requires an end time and catalog pin.");
            if (state.matchStartTicks > 0 && state.rivalTimeLimitSeconds <= 0f)
                throw new FormatException("An active MatchRival match requires a positive rival time limit.");
            if (!Enum.IsDefined(typeof(MatchRivalResult), state.pendingResult))
                throw new FormatException("MatchRival pending result is invalid.");
            if (!Enum.IsDefined(typeof(MatchRivalPendingRewardKind), state.pendingRewardKind))
                throw new FormatException("MatchRival pending reward kind is invalid.");

            state.claimedBoxStages ??= new List<int>();
            var uniqueStages = new HashSet<int>();
            foreach (int stage in state.claimedBoxStages)
            {
                if (stage < MatchRivalEngine.MinStage || stage > MatchRivalEngine.MaxStage || !uniqueStages.Add(stage))
                    throw new FormatException("MatchRival box claim stages are invalid.");
            }

            state.pendingRewards ??= new List<MatchRivalRewardData>();
            bool hasTransaction = !string.IsNullOrWhiteSpace(state.pendingTransactionId);
            if (hasTransaction && ((MatchRivalPendingRewardKind)state.pendingRewardKind == MatchRivalPendingRewardKind.None
                || state.pendingRewardStage < MatchRivalEngine.MinStage
                || state.pendingRewardStage > MatchRivalEngine.MaxStage
                || state.pendingRewards.Count == 0))
                throw new FormatException("MatchRival pending transaction state is incomplete.");

            foreach (MatchRivalRewardData reward in state.pendingRewards)
            {
                if (reward == null || string.IsNullOrWhiteSpace(reward.rewardId) || reward.amount <= 0)
                    throw new FormatException("MatchRival pending reward snapshot is invalid.");
            }
        }

        internal static List<MatchRivalRewardData> ToData(IReadOnlyList<ContentReward> rewards)
        {
            var result = new List<MatchRivalRewardData>(rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                ContentReward reward = rewards[index];
                result.Add(new MatchRivalRewardData { rewardId = reward.RewardId, amount = reward.Amount });
            }

            return result;
        }

        internal static IReadOnlyList<ContentReward> ToRewards(IReadOnlyList<MatchRivalRewardData> rewards)
        {
            var result = new List<ContentReward>(rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                MatchRivalRewardData reward = rewards[index];
                result.Add(new ContentReward(reward.rewardId, reward.amount));
            }

            return result;
        }
    }
}
