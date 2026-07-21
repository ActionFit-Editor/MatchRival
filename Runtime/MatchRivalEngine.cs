using System;
using System.Collections.Generic;
using ActionFit.Content;
using ActionFit.Time;

namespace ActionFit.MatchRival
{
    public sealed class MatchRivalEngine
    {
        public const int MinStage = 1;
        public const int MaxStage = 10;

        private readonly IContentStateStore _stateStore;
        private readonly IContentRewardService _rewardService;
        private readonly IMatchRivalCatalogResolver _catalogResolver;
        private readonly IClock _clock;
        private readonly TimeZoneInfo _calendarTimeZone;
        private readonly TimeZoneInfo _legacyCalendarTimeZone;
        private readonly IMatchRivalRandom _random;
        private readonly IMatchRivalProgressCurveProvider _curveProvider;
        private readonly IMatchRivalOpponentProvider _opponentProvider;
        private readonly IMatchRivalAccessPolicy _accessPolicy;
        private readonly IMatchRivalSchedulePolicy _schedulePolicy;
        private readonly IMatchRivalAnalyticsSink _analytics;
        private readonly string _contentId;

        private MatchRivalStateData _state = MatchRivalStateSerializer.CreateDefault();
        private MatchRivalCatalog _catalog;

        public MatchRivalEngine(
            IContentStateStore stateStore,
            IContentRewardService rewardService,
            IMatchRivalCatalogResolver catalogResolver,
            IMatchRivalClock clock,
            IMatchRivalRandom random,
            IMatchRivalProgressCurveProvider curveProvider,
            IMatchRivalOpponentProvider opponentProvider,
            string contentId,
            IMatchRivalAccessPolicy accessPolicy,
            IMatchRivalSchedulePolicy schedulePolicy,
            IMatchRivalAnalyticsSink analytics = null)
            : this(
                stateStore,
                rewardService,
                catalogResolver,
                new LegacyMatchRivalClockAdapter(clock),
                TimeZoneInfo.Utc,
                TimeZoneInfo.Utc,
                random,
                curveProvider,
                opponentProvider,
                contentId,
                accessPolicy,
                schedulePolicy,
                analytics)
        {
        }

        public MatchRivalEngine(
            IContentStateStore stateStore,
            IContentRewardService rewardService,
            IMatchRivalCatalogResolver catalogResolver,
            IClock clock,
            TimeZoneInfo calendarTimeZone,
            TimeZoneInfo legacyCalendarTimeZone,
            IMatchRivalRandom random,
            IMatchRivalProgressCurveProvider curveProvider,
            IMatchRivalOpponentProvider opponentProvider,
            string contentId,
            IMatchRivalAccessPolicy accessPolicy,
            IMatchRivalSchedulePolicy schedulePolicy,
            IMatchRivalAnalyticsSink analytics = null)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _rewardService = rewardService ?? throw new ArgumentNullException(nameof(rewardService));
            _catalogResolver = catalogResolver ?? throw new ArgumentNullException(nameof(catalogResolver));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _calendarTimeZone = calendarTimeZone ?? throw new ArgumentNullException(nameof(calendarTimeZone));
            _legacyCalendarTimeZone = legacyCalendarTimeZone ?? throw new ArgumentNullException(nameof(legacyCalendarTimeZone));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _curveProvider = curveProvider ?? throw new ArgumentNullException(nameof(curveProvider));
            _opponentProvider = opponentProvider ?? throw new ArgumentNullException(nameof(opponentProvider));
            _contentId = string.IsNullOrWhiteSpace(contentId)
                ? throw new ArgumentException("Content ID is required.", nameof(contentId))
                : contentId;
            _accessPolicy = accessPolicy ?? throw new ArgumentNullException(nameof(accessPolicy));
            _schedulePolicy = schedulePolicy ?? throw new ArgumentNullException(nameof(schedulePolicy));
            _analytics = analytics ?? new NullMatchRivalAnalyticsSink();
            _catalog = _catalogResolver.Current ?? throw new InvalidOperationException("Current MatchRival catalog is unavailable.");
        }

        public event Action<MatchRivalState> StateChanged;

        public MatchRivalState State => new MatchRivalState(_state);
        public MatchRivalCatalog Catalog => ResolveCatalog();
        public bool IsEventStarted => _state.eventStarted;
        public bool PendingEnd => _state.pendingEnd;
        public bool IsMatchActive => _state.matchStartTicks > 0;
        public bool IsRewardServiceAvailable => _rewardService.IsAvailable;
        public bool IsEventActive => _accessPolicy.IsAccessAllowed
            && _schedulePolicy.IsEnabled
            && _state.eventEndTicks > NowTicks;
        public bool IsEventDay => _schedulePolicy.IsEnabled && _schedulePolicy.IsActiveDay(CalendarNow.DayOfWeek);
        public TimeSpan EventRemainingTime => RemainingUntil(_state.eventEndTicks);
        public TimeSpan ExpectedRemainingTime
        {
            get
            {
                TimeSpan remaining = EventRemainingTime;
                if (remaining > TimeSpan.Zero) return remaining;
                if (!IsEventDay) return TimeSpan.Zero;
                return RemainingUntil(GetActiveWindowEndTicks());
            }
        }
        public int Stage => _state.stage;
        public bool IsHard => _state.isHard;
        public int CollectedBeans => _state.collectedBeans;
        public int RequiredBeans => ResolveCatalog().GetDifficulty(_state.stage).RequiredBeans;
        public bool IsBeansComplete => IsMatchActive && _state.collectedBeans >= RequiredBeans;
        public MatchRivalResult PendingResult => (MatchRivalResult)_state.pendingResult;
        public bool ResultAnimationCompleted => _state.resultAnimationCompleted;
        public int PreviousDisplayedBeans => _state.previousDisplayedBeans;
        public int PreviousDisplayedRivalBeans => _state.previousDisplayedRivalBeans;
        public long MatchStartTicks => _state.matchStartTicks;
        public float RivalTimeLimitSeconds => _state.rivalTimeLimitSeconds;
        public int RivalCurveIndex => _state.rivalCurveIndex;
        public bool TutorialDone => _state.tutorialDone;

        public TimeSpan RivalRemainingTime
        {
            get
            {
                if (!IsMatchActive) return TimeSpan.Zero;
                double elapsedSeconds = (NowTicks - _state.matchStartTicks)
                    / (double)TimeSpan.TicksPerSecond;
                double remaining = _state.rivalTimeLimitSeconds - elapsedSeconds;
                if (remaining <= 0d) return TimeSpan.Zero;
                return TimeSpan.FromSeconds(Math.Min(remaining, 172800d));
            }
        }

        public bool IsRivalFinished => IsMatchActive && RivalRemainingTime <= TimeSpan.Zero;

        public int RivalActualBeans
        {
            get
            {
                if (!IsMatchActive) return 0;
                if (IsRivalFinished) return RequiredBeans;
                float total = _state.rivalTimeLimitSeconds;
                if (total <= 0f) return 0;
                float elapsed = total - (float)RivalRemainingTime.TotalSeconds;
                float normalized = Math.Max(0f, Math.Min(1f, elapsed / total));
                float value = Math.Max(0f, Math.Min(1f, _curveProvider.Evaluate(_state.rivalCurveIndex, normalized)));
                return (int)Math.Floor(value * RequiredBeans);
            }
        }

        public MatchRivalResult CurrentResult
        {
            get
            {
                if (!IsMatchActive) return MatchRivalResult.None;
                if (IsBeansComplete) return MatchRivalResult.Win;
                return IsRivalFinished ? MatchRivalResult.Lose : MatchRivalResult.None;
            }
        }

        public void Restore()
        {
            if (!_stateStore.TryLoad(_contentId, out string json))
            {
                _state = MatchRivalStateSerializer.CreateDefault();
                _catalog = _catalogResolver.Current
                    ?? throw new InvalidOperationException("Current MatchRival catalog is unavailable.");
                NotifyStateChanged();
                return;
            }

            MatchRivalStateData restored = MatchRivalStateSerializer.Deserialize(json, out bool upgraded);
            _state = restored;
            _catalog = ResolveCatalog();
            ValidateCatalogState();
            if (upgraded) Persist(true);
            RecoverPendingTransaction();
            NotifyStateChanged();
        }

        public bool ImportStateIfEmpty(MatchRivalImportState importState)
        {
            if (importState == null) throw new ArgumentNullException(nameof(importState));
            if (_stateStore.TryLoad(_contentId, out _)) return false;

            MatchRivalCatalog catalog = _catalogResolver.Current
                ?? throw new InvalidOperationException("Current MatchRival catalog is unavailable.");
            var imported = MatchRivalStateSerializer.CreateDefault();
            imported.timeBasis = (int)MatchRivalTimeBasis.LegacyCalendarTicks;
            imported.catalogVersion = catalog.CatalogVersion;
            imported.balanceRevision = catalog.BalanceRevision;
            imported.stage = Math.Max(MinStage, Math.Min(MaxStage, importState.Stage));
            imported.isHard = importState.IsHard;
            imported.collectedBeans = Math.Min(
                catalog.GetDifficulty(imported.stage).RequiredBeans,
                Math.Max(0, importState.CollectedBeans));
            imported.pendingResult = (int)importState.PendingResult;
            imported.resultAnimationCompleted = importState.ResultAnimationCompleted;
            imported.previousDisplayedBeans = Math.Max(0, importState.PreviousDisplayedBeans);
            imported.previousDisplayedRivalBeans = Math.Max(0, importState.PreviousDisplayedRivalBeans);
            imported.matchStartTicks = Math.Max(0L, importState.MatchStartTicks);
            imported.rivalTimeLimitSeconds = Math.Max(0f, importState.RivalTimeLimitSeconds);
            imported.rivalCurveIndex = Math.Max(0, importState.RivalCurveIndex);
            imported.tutorialDone = importState.TutorialDone;
            imported.eventStarted = importState.EventStarted;
            imported.pendingEnd = importState.PendingEnd;
            imported.eventEndTicks = Math.Max(0L, importState.EventEndTicks);
            imported.claimedBoxStages = NormalizeStages(importState.ClaimedBoxStages);
            imported.opponent = ToData(importState.Opponent);

            if (imported.pendingResult != (int)MatchRivalResult.None)
            {
                MatchRivalResult result = (MatchRivalResult)imported.pendingResult;
                IReadOnlyList<ContentReward> rewards = result == MatchRivalResult.Win
                    ? catalog.GetRoundRewards(imported.stage).WinRewards
                    : catalog.GetRoundRewards(imported.stage).LoseRewards;
                imported.pendingRewardKind = (int)MatchRivalPendingRewardKind.Round;
                imported.pendingRewardStage = imported.stage;
                imported.pendingRewards = MatchRivalStateSerializer.ToData(rewards);
            }

            _state = imported;
            _catalog = catalog;
            ValidateCatalogState();
            Save(true);
            return true;
        }

        public bool TryStartEvent()
        {
            if (!_accessPolicy.IsAccessAllowed || !_schedulePolicy.IsEnabled
                || !_schedulePolicy.IsActiveDay(CalendarNow.DayOfWeek))
                return false;
            if (_state.eventStarted) return false;
            if (IsEventActive) return false;

            MatchRivalCatalog current = _catalogResolver.Current
                ?? throw new InvalidOperationException("Current MatchRival catalog is unavailable.");
            _state.timeBasis = (int)MatchRivalTimeBasis.UtcTicks;
            long endTicks = GetActiveWindowEndTicks();
            if (endTicks <= NowTicks)
                throw new InvalidOperationException("MatchRival schedule returned a non-future end time.");

            bool tutorialDone = _state.tutorialDone;
            ResetGameplayState();
            _state.tutorialDone = tutorialDone;
            _state.catalogVersion = current.CatalogVersion;
            _state.balanceRevision = current.BalanceRevision;
            _state.eventStarted = true;
            _state.pendingEnd = false;
            _state.eventEndTicks = endTicks;
            _catalog = current;
            Save(true);
            _analytics.EventStarted(_state.eventEndTicks);
            return true;
        }

        public void EvaluateTimeout()
        {
            if (!_schedulePolicy.IsEnabled)
            {
                ForceTerminate();
                return;
            }

            if (!_state.eventStarted || _state.eventEndTicks > NowTicks) return;
            if (IsMatchActive && CurrentResult == MatchRivalResult.None) return;
            if (_state.pendingEnd) return;
            _state.pendingEnd = true;
            Save(true);
        }

        public void MarkPendingEnd()
        {
            SetPendingEnd(true);
        }

        public void SetPendingEnd(bool pending)
        {
            if (_state.pendingEnd == pending) return;
            _state.pendingEnd = pending;
            Save(true);
        }

        public void EndEvent()
        {
            CompletePreparedPendingReward();
            long endedInstance = _state.eventEndTicks;
            bool tutorialDone = _state.tutorialDone;
            ResetGameplayState();
            _state.tutorialDone = tutorialDone;
            _state.eventStarted = false;
            _state.pendingEnd = false;
            _state.eventEndTicks = endedInstance;
            Save(true);
            _analytics.EventEnded(endedInstance);
        }

        public void ResetGameplay()
        {
            bool tutorialDone = _state.tutorialDone;
            ResetGameplayState();
            _state.tutorialDone = tutorialDone;
            Save(true);
        }

        public bool StartMatch()
        {
            if (!_state.eventStarted || !IsEventActive || _state.pendingEnd || IsMatchActive) return false;
            MatchRivalDifficulty difficulty = ResolveCatalog().GetDifficulty(_state.stage);
            if (_curveProvider.CurveCount <= 0)
                throw new InvalidOperationException("MatchRival requires at least one progress curve.");

            float minSeconds = _state.isHard ? difficulty.HardMinSeconds : difficulty.EasyMinSeconds;
            float maxSeconds = _state.isHard ? difficulty.HardMaxSeconds : difficulty.EasyMaxSeconds;
            _state.opponent = ToData(_opponentProvider.CreateOpponent());
            _state.rivalCurveIndex = _random.Range(0, _curveProvider.CurveCount);
            _state.rivalTimeLimitSeconds = _random.Range(minSeconds, maxSeconds);
            _state.matchStartTicks = NowTicks;
            _state.collectedBeans = 0;
            _state.previousDisplayedBeans = 0;
            _state.previousDisplayedRivalBeans = 0;
            _state.resultAnimationCompleted = false;
            _state.pendingResult = (int)MatchRivalResult.None;
            ClearPendingReward();
            Save(true);
            _analytics.RoundStarted(_state.stage);
            return true;
        }

        public bool AddBeans(int amount)
        {
            if (amount <= 0 || !IsMatchActive || CurrentResult != MatchRivalResult.None) return false;
            _state.collectedBeans = Math.Min(RequiredBeans, checked(_state.collectedBeans + amount));
            Save(false);
            return true;
        }

        public int GetOrderBeans(IEnumerable<int> orderItemValues)
        {
            if (orderItemValues == null) throw new ArgumentNullException(nameof(orderItemValues));
            int total = 0;
            foreach (int value in orderItemValues)
                total = checked(total + ResolveCatalog().GetOrderBeans(value));
            return total;
        }

        public bool PrepareResultReward(MatchRivalResult result)
        {
            if (result == MatchRivalResult.None || CurrentResult != result) return false;
            if (!_rewardService.IsAvailable) return false;
            if ((MatchRivalResult)_state.pendingResult == result
                && (MatchRivalPendingRewardKind)_state.pendingRewardKind == MatchRivalPendingRewardKind.Round)
                return true;

            IReadOnlyList<ContentReward> rewards = result == MatchRivalResult.Win
                ? ResolveCatalog().GetRoundRewards(_state.stage).WinRewards
                : ResolveCatalog().GetRoundRewards(_state.stage).LoseRewards;
            _state.pendingResult = (int)result;
            _state.pendingRewardKind = (int)MatchRivalPendingRewardKind.Round;
            _state.pendingRewardStage = _state.stage;
            _state.pendingTransactionId = string.Empty;
            _state.pendingRewards = MatchRivalStateSerializer.ToData(rewards);
            Save(true);
            return true;
        }

        public MatchRivalRoundClaimResult ClaimPendingResultReward()
        {
            MatchRivalResult result = (MatchRivalResult)_state.pendingResult;
            int stage = _state.pendingRewardStage;
            if (result == MatchRivalResult.None
                || (MatchRivalPendingRewardKind)_state.pendingRewardKind != MatchRivalPendingRewardKind.Round)
                return new MatchRivalRoundClaimResult(false, _state.stage, MatchRivalResult.None);

            if (string.IsNullOrWhiteSpace(_state.pendingTransactionId))
            {
                _state.pendingTransactionId = BuildRoundTransactionId(stage, result);
                Save(true);
            }

            CompletePendingTransaction();
            return new MatchRivalRoundClaimResult(true, stage, result);
        }

        public bool ClaimBoxReward(int stage)
        {
            if (!_state.eventStarted || !IsMatchActive || CurrentResult != MatchRivalResult.Win)
                return false;
            if (stage < MinStage || stage > MaxStage || stage > _state.stage || IsBoxRewardClaimed(stage))
                return false;
            if (!_rewardService.IsAvailable) return false;
            if (!ResolveCatalog().TryGetBoxRewards(stage, out MatchRivalBoxRewards rewards)) return false;
            if ((MatchRivalPendingRewardKind)_state.pendingRewardKind != MatchRivalPendingRewardKind.None)
                throw new InvalidOperationException("Another MatchRival reward transaction is pending.");

            _state.pendingRewardKind = (int)MatchRivalPendingRewardKind.Box;
            _state.pendingRewardStage = stage;
            _state.pendingTransactionId = BuildBoxTransactionId(stage);
            _state.pendingRewards = MatchRivalStateSerializer.ToData(rewards.Rewards);
            Save(true);
            CompletePendingTransaction();
            return true;
        }

        public bool IsBoxRewardClaimed(int stage)
        {
            return _state.claimedBoxStages.Contains(stage);
        }

        public void SetResultAnimationCompleted(bool completed)
        {
            if (_state.resultAnimationCompleted == completed) return;
            _state.resultAnimationCompleted = completed;
            Save(completed);
        }

        public void SetDisplayedBeans(int playerBeans, int rivalBeans)
        {
            _state.previousDisplayedBeans = Math.Max(0, playerBeans);
            _state.previousDisplayedRivalBeans = Math.Max(0, rivalBeans);
            Save(false);
        }

        public void SetTutorialDone(bool done)
        {
            if (_state.tutorialDone == done) return;
            _state.tutorialDone = done;
            Save(done);
        }

        public bool ForceWin()
        {
            if (!IsMatchActive || CurrentResult != MatchRivalResult.None) return false;
            _state.collectedBeans = RequiredBeans;
            Save(true);
            return true;
        }

        public bool ForceLose()
        {
            if (!IsMatchActive || CurrentResult != MatchRivalResult.None) return false;
            _state.matchStartTicks = NowTicks
                - TimeSpan.FromSeconds(_state.rivalTimeLimitSeconds + 1f).Ticks;
            Save(true);
            return true;
        }

        private DateTime UtcNow
        {
            get
            {
                DateTime utcNow = _clock.UtcNow;
                if (utcNow.Kind != DateTimeKind.Utc)
                    throw new InvalidOperationException("IClock.UtcNow must have DateTimeKind.Utc.");
                return utcNow;
            }
        }

        private TimeZoneInfo ActiveCalendarTimeZone => (MatchRivalTimeBasis)_state.timeBasis
            == MatchRivalTimeBasis.LegacyCalendarTicks
                ? _legacyCalendarTimeZone
                : _calendarTimeZone;

        private DateTime CalendarNow => _clock.GetCurrentTime(ActiveCalendarTimeZone).DateTime;

        private long NowTicks => (MatchRivalTimeBasis)_state.timeBasis == MatchRivalTimeBasis.LegacyCalendarTicks
            ? CalendarNow.Ticks
            : UtcNow.Ticks;

        private long GetActiveWindowEndTicks()
        {
            DateTime calendarEnd = _schedulePolicy.GetActiveWindowEnd(CalendarNow);
            if ((MatchRivalTimeBasis)_state.timeBasis == MatchRivalTimeBasis.LegacyCalendarTicks)
                return calendarEnd.Ticks;

            DateTime unspecifiedEnd = DateTime.SpecifyKind(calendarEnd, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedEnd, _calendarTimeZone).Ticks;
        }

        private TimeSpan RemainingUntil(long endTicks)
        {
            long remainingTicks = endTicks - NowTicks;
            return remainingTicks > 0 ? TimeSpan.FromTicks(remainingTicks) : TimeSpan.Zero;
        }

        private void RecoverPendingTransaction()
        {
            if (string.IsNullOrWhiteSpace(_state.pendingTransactionId)) return;
            CompletePendingTransaction();
        }

        private void CompletePreparedPendingReward()
        {
            MatchRivalPendingRewardKind kind = (MatchRivalPendingRewardKind)_state.pendingRewardKind;
            if (kind == MatchRivalPendingRewardKind.None) return;
            if (string.IsNullOrWhiteSpace(_state.pendingTransactionId))
            {
                _state.pendingTransactionId = kind == MatchRivalPendingRewardKind.Round
                    ? BuildRoundTransactionId(_state.pendingRewardStage, (MatchRivalResult)_state.pendingResult)
                    : BuildBoxTransactionId(_state.pendingRewardStage);
                Save(true);
            }

            CompletePendingTransaction();
        }

        private void CompletePendingTransaction()
        {
            if (!_rewardService.IsAvailable)
                throw new InvalidOperationException("MatchRival reward service is unavailable.");

            string transactionId = _state.pendingTransactionId;
            IReadOnlyList<ContentReward> rewards = MatchRivalStateSerializer.ToRewards(_state.pendingRewards);
            if (!_rewardService.HasGranted(transactionId))
                _rewardService.GrantOnce(transactionId, rewards);
            if (!_rewardService.HasGranted(transactionId))
                throw new InvalidOperationException("MatchRival reward receipt was not persisted.");

            MatchRivalPendingRewardKind kind = (MatchRivalPendingRewardKind)_state.pendingRewardKind;
            int stage = _state.pendingRewardStage;
            if (kind == MatchRivalPendingRewardKind.Round)
            {
                MatchRivalResult result = (MatchRivalResult)_state.pendingResult;
                int beans = _state.collectedBeans;
                int required = ResolveCatalog().GetDifficulty(stage).RequiredBeans;
                ClearMatchState();
                _state.isHard = result == MatchRivalResult.Win;
                if (result == MatchRivalResult.Win && stage < MaxStage) _state.stage = stage + 1;
                ClearPendingReward();
                Save(true);
                _analytics.RoundEnded(stage, result, beans, required);
                return;
            }

            if (kind == MatchRivalPendingRewardKind.Box)
            {
                if (!_state.claimedBoxStages.Contains(stage)) _state.claimedBoxStages.Add(stage);
                ClearPendingReward();
                Save(true);
                _analytics.BoxRewardClaimed(stage);
                return;
            }

            throw new InvalidOperationException("MatchRival pending reward kind is invalid.");
        }

        private void ForceTerminate()
        {
            RecoverPendingTransaction();
            bool tutorialDone = _state.tutorialDone;
            ResetGameplayState();
            _state.tutorialDone = tutorialDone;
            _state.eventStarted = false;
            _state.pendingEnd = false;
            _state.eventEndTicks = 0L;
            _state.catalogVersion = string.Empty;
            _state.balanceRevision = string.Empty;
            Save(true);
        }

        private void ResetGameplayState()
        {
            ClearMatchState();
            _state.stage = MinStage;
            _state.isHard = false;
            _state.claimedBoxStages.Clear();
            ClearPendingReward();
        }

        private void ClearMatchState()
        {
            _state.collectedBeans = 0;
            _state.pendingResult = (int)MatchRivalResult.None;
            _state.resultAnimationCompleted = false;
            _state.previousDisplayedBeans = 0;
            _state.previousDisplayedRivalBeans = 0;
            _state.matchStartTicks = 0L;
            _state.rivalTimeLimitSeconds = 0f;
            _state.rivalCurveIndex = 0;
            _state.opponent = null;
        }

        private void ClearPendingReward()
        {
            _state.pendingRewardKind = (int)MatchRivalPendingRewardKind.None;
            _state.pendingRewardStage = 0;
            _state.pendingTransactionId = string.Empty;
            _state.pendingRewards.Clear();
        }

        private MatchRivalCatalog ResolveCatalog()
        {
            if (_state.eventStarted || !string.IsNullOrWhiteSpace(_state.catalogVersion))
            {
                if (!_catalogResolver.TryResolve(
                    _state.catalogVersion,
                    _state.balanceRevision,
                    out MatchRivalCatalog resolved))
                    throw new InvalidOperationException(
                        $"Unknown MatchRival catalog {_state.catalogVersion}/{_state.balanceRevision}.");
                _catalog = resolved;
            }
            else
            {
                _catalog = _catalogResolver.Current
                    ?? throw new InvalidOperationException("Current MatchRival catalog is unavailable.");
            }

            return _catalog;
        }

        private void ValidateCatalogState()
        {
            MatchRivalCatalog catalog = ResolveCatalog();
            MatchRivalDifficulty difficulty = catalog.GetDifficulty(_state.stage);
            if (_state.collectedBeans > difficulty.RequiredBeans)
                throw new FormatException("MatchRival collected beans exceed the pinned stage requirement.");
            if (_state.matchStartTicks > 0
                && (_state.rivalCurveIndex < 0 || _state.rivalCurveIndex >= _curveProvider.CurveCount))
                throw new FormatException("MatchRival rival curve index is outside the available range.");
        }

        private void Save(bool flush)
        {
            Persist(flush);
            NotifyStateChanged();
        }

        private void Persist(bool flush)
        {
            MatchRivalStateSerializer.Validate(_state);
            _stateStore.Save(_contentId, MatchRivalStateSerializer.Serialize(_state));
            if (flush && _stateStore is IFlushableContentStateStore flushable) flushable.Flush();
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke(new MatchRivalState(_state));
        }

        private string BuildRoundTransactionId(int stage, MatchRivalResult result)
        {
            return $"{_contentId}/event/{_state.eventEndTicks}/round/{stage}/{result.ToString().ToLowerInvariant()}";
        }

        private string BuildBoxTransactionId(int stage)
        {
            return $"{_contentId}/event/{_state.eventEndTicks}/box/{stage}";
        }

        private static List<int> NormalizeStages(IReadOnlyList<int> stages)
        {
            var result = new List<int>();
            if (stages == null) return result;
            for (int index = 0; index < stages.Count; index++)
            {
                int stage = stages[index];
                if (stage < MinStage || stage > MaxStage)
                    throw new ArgumentOutOfRangeException(nameof(stages));
                if (!result.Contains(stage)) result.Add(stage);
            }

            return result;
        }

        private static MatchRivalOpponentData ToData(MatchRivalOpponent opponent)
        {
            return opponent == null
                ? null
                : new MatchRivalOpponentData
                {
                    id = opponent.Id,
                    displayName = opponent.DisplayName,
                    profileId = opponent.ProfileId,
                    frameId = opponent.FrameId,
                };
        }
    }
}
