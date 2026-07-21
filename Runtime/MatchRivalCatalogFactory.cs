using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ActionFit.Content;

namespace ActionFit.MatchRival
{
    /// <summary>Canonical CSV text required to build the standalone Match Rival balance.</summary>
    public sealed class MatchRivalCatalogCsvData
    {
        public MatchRivalCatalogCsvData(
            string beanOrders,
            string difficulties,
            string eventSettings,
            string boxRewards,
            string roundRewards)
        {
            BeanOrders = RequireText(beanOrders, nameof(beanOrders));
            Difficulties = RequireText(difficulties, nameof(difficulties));
            EventSettings = RequireText(eventSettings, nameof(eventSettings));
            BoxRewards = RequireText(boxRewards, nameof(boxRewards));
            RoundRewards = RequireText(roundRewards, nameof(roundRewards));
        }

        public string BeanOrders { get; }
        public string Difficulties { get; }
        public string EventSettings { get; }
        public string BoxRewards { get; }
        public string RoundRewards { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("CSV text must not be empty.", parameterName)
                : value;
        }
    }

    /// <summary>Complete importer-independent balance used by standalone compositions.</summary>
    public sealed class MatchRivalStandaloneCatalog
    {
        internal MatchRivalStandaloneCatalog(
            MatchRivalCatalog catalog,
            IMatchRivalSchedulePolicy schedulePolicy)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            SchedulePolicy = schedulePolicy ?? throw new ArgumentNullException(nameof(schedulePolicy));
        }

        public MatchRivalCatalog Catalog { get; }
        public IMatchRivalSchedulePolicy SchedulePolicy { get; }
    }

    /// <summary>Builds the package catalog directly from the canonical CSV text.</summary>
    public static class MatchRivalCatalogFactory
    {
        public const string DefaultCatalogVersion = "cat-merge-match-rival-v1";
        public const string DefaultSegment = "";
        public const string RewardSegment = "Reward";
        public const string DefaultBalanceRevision = "unknown-v1";
        public const string RewardBalanceRevision = "reward-v1";

        public static MatchRivalStandaloneCatalog Create(
            MatchRivalCatalogCsvData csv,
            string segment = DefaultSegment)
        {
            string balanceRevision = NormalizeSegment(segment) == RewardSegment
                ? RewardBalanceRevision
                : DefaultBalanceRevision;
            return Create(csv, DefaultCatalogVersion, balanceRevision, segment);
        }

        public static MatchRivalStandaloneCatalog Create(
            MatchRivalCatalogCsvData csv,
            string catalogVersion,
            string balanceRevision,
            string segment)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            segment = NormalizeSegment(segment);

            List<KeyValuePair<int, int>> beanOrders = ParseBeanOrders(csv.BeanOrders, segment);
            List<MatchRivalDifficulty> difficulties = ParseDifficulties(csv.Difficulties, segment);
            List<DayOfWeek> activeDays = ParseActiveDays(csv.EventSettings);
            List<MatchRivalBoxRewards> boxRewards = ParseBoxRewards(csv.BoxRewards, segment);
            List<MatchRivalRoundRewards> roundRewards = ParseRoundRewards(csv.RoundRewards);

            var catalog = new MatchRivalCatalog(
                catalogVersion,
                balanceRevision,
                difficulties,
                beanOrders,
                roundRewards,
                boxRewards);
            return new MatchRivalStandaloneCatalog(catalog, new FixedSchedulePolicy(activeDays));
        }

        private static string NormalizeSegment(string segment)
        {
            segment = segment?.Trim() ?? string.Empty;
            if (segment.Length == 0) return DefaultSegment;
            if (string.Equals(segment, RewardSegment, StringComparison.Ordinal)) return RewardSegment;
            throw new ArgumentException($"Unsupported MatchRival segment '{segment}'.", nameof(segment));
        }

        private static List<KeyValuePair<int, int>> ParseBeanOrders(string csv, string segment)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "Bean_Order");
            var rows = new Dictionary<RowKey, int>();
            var levels = new SortedSet<int>();
            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyList<string> row = table.Rows[index];
                int level = CanonicalCsvValue.ParseInt(
                    table.Value(row, "OrderItemValue"),
                    "Bean_Order.OrderItemValue");
                string rowSegment = table.Value(row, "NetSeg").Trim();
                int reward = CanonicalCsvValue.ParseInt(
                    table.Value(row, "CompleteReward"),
                    "Bean_Order.CompleteReward");
                if (!rows.TryAdd(new RowKey(level, rowSegment), reward))
                    throw new FormatException($"MatchRival Bean_Order contains duplicate row {level}/{rowSegment}.");
                levels.Add(level);
            }

            var result = new List<KeyValuePair<int, int>>(levels.Count);
            foreach (int level in levels)
            {
                if (!TrySelect(rows, level, segment, out int reward))
                    throw new FormatException($"MatchRival Bean_Order has no default row for level {level}.");
                result.Add(new KeyValuePair<int, int>(level, reward));
            }
            return result;
        }

        private static List<MatchRivalDifficulty> ParseDifficulties(string csv, string segment)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "Difficulty");
            var rows = new Dictionary<RowKey, DifficultyRow>();
            var stages = new SortedSet<int>();
            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyList<string> row = table.Rows[index];
                int stage = CanonicalCsvValue.ParseInt(table.Value(row, "Round"), "Difficulty.Round");
                string rowSegment = table.Value(row, "NetSeg").Trim();
                (int easyMin, int easyMax) = CanonicalCsvValue.ParseVector2Int(
                    table.Value(row, "EasyTime"),
                    "Difficulty.EasyTime");
                (int hardMin, int hardMax) = CanonicalCsvValue.ParseVector2Int(
                    table.Value(row, "HardTime"),
                    "Difficulty.HardTime");
                int required = CanonicalCsvValue.ParseInt(
                    table.Value(row, "RequireValue"),
                    "Difficulty.RequireValue");
                if (!rows.TryAdd(
                        new RowKey(stage, rowSegment),
                        new DifficultyRow(stage, easyMin, easyMax, hardMin, hardMax, required)))
                {
                    throw new FormatException($"MatchRival Difficulty contains duplicate row {stage}/{rowSegment}.");
                }
                stages.Add(stage);
            }

            var result = new List<MatchRivalDifficulty>(stages.Count);
            foreach (int stage in stages)
            {
                if (!TrySelect(rows, stage, segment, out DifficultyRow row))
                    throw new FormatException($"MatchRival Difficulty has no default row for stage {stage}.");
                result.Add(new MatchRivalDifficulty(
                    stage,
                    checked(row.EasyMinMinutes * 60),
                    checked(row.EasyMaxMinutes * 60),
                    checked(row.HardMinMinutes * 60),
                    checked(row.HardMaxMinutes * 60),
                    row.RequiredBeans));
            }
            return result;
        }

        private static List<DayOfWeek> ParseActiveDays(string csv)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "EventSettings");
            if (table.Rows.Count != 1)
                throw new FormatException("MatchRival EventSettings must contain exactly one row.");
            return CanonicalCsvValue.ParseDays(table.Value(table.Rows[0], "ActiveDays"));
        }

        private static List<MatchRivalBoxRewards> ParseBoxRewards(string csv, string segment)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "Reward_Box");
            var rows = new Dictionary<RowKey, IReadOnlyList<ContentReward>>();
            var stages = new SortedSet<int>();
            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyList<string> row = table.Rows[index];
                int stage = CanonicalCsvValue.ParseInt(table.Value(row, "Round"), "Reward_Box.Round");
                string rowSegment = table.Value(row, "NetSeg").Trim();
                if (!rows.TryAdd(
                        new RowKey(stage, rowSegment),
                        CanonicalCsvValue.ParseRewards(
                            table.Value(row, "RewardBox"),
                            "Reward_Box.RewardBox",
                            requireAny: true)))
                {
                    throw new FormatException($"MatchRival Reward_Box contains duplicate row {stage}/{rowSegment}.");
                }
                stages.Add(stage);
            }

            var result = new List<MatchRivalBoxRewards>();
            foreach (int stage in stages)
            {
                if (TrySelect(rows, stage, segment, out IReadOnlyList<ContentReward> rewards))
                    result.Add(new MatchRivalBoxRewards(stage, rewards));
            }
            return result;
        }

        private static List<MatchRivalRoundRewards> ParseRoundRewards(string csv)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "Reward_Round");
            var result = new List<MatchRivalRoundRewards>(table.Rows.Count);
            var seen = new HashSet<int>();
            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyList<string> row = table.Rows[index];
                int stage = CanonicalCsvValue.ParseInt(table.Value(row, "Round"), "Reward_Round.Round");
                if (!seen.Add(stage))
                    throw new FormatException($"MatchRival Reward_Round contains duplicate stage {stage}.");
                result.Add(new MatchRivalRoundRewards(
                    stage,
                    CanonicalCsvValue.ParseRewards(
                        table.Value(row, "RewardWin"),
                        "Reward_Round.RewardWin",
                        requireAny: true),
                    CanonicalCsvValue.ParseRewards(
                        table.Value(row, "RewardLose"),
                        "Reward_Round.RewardLose",
                        requireAny: true)));
            }
            result.Sort((left, right) => left.Stage.CompareTo(right.Stage));
            return result;
        }

        private static bool TrySelect<T>(Dictionary<RowKey, T> rows, int key, string segment, out T value)
        {
            if (segment.Length > 0 && rows.TryGetValue(new RowKey(key, segment), out value)) return true;
            return rows.TryGetValue(new RowKey(key, DefaultSegment), out value);
        }

        private readonly struct RowKey : IEquatable<RowKey>
        {
            public RowKey(int key, string segment)
            {
                Key = key;
                Segment = segment ?? string.Empty;
            }

            private int Key { get; }
            private string Segment { get; }

            public bool Equals(RowKey other)
            {
                return Key == other.Key && string.Equals(Segment, other.Segment, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is RowKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (Key * 397) ^ StringComparer.Ordinal.GetHashCode(Segment);
            }
        }

        private sealed class DifficultyRow
        {
            public DifficultyRow(
                int stage,
                int easyMinMinutes,
                int easyMaxMinutes,
                int hardMinMinutes,
                int hardMaxMinutes,
                int requiredBeans)
            {
                Stage = stage;
                EasyMinMinutes = easyMinMinutes;
                EasyMaxMinutes = easyMaxMinutes;
                HardMinMinutes = hardMinMinutes;
                HardMaxMinutes = hardMaxMinutes;
                RequiredBeans = requiredBeans;
            }

            public int Stage { get; }
            public int EasyMinMinutes { get; }
            public int EasyMaxMinutes { get; }
            public int HardMinMinutes { get; }
            public int HardMaxMinutes { get; }
            public int RequiredBeans { get; }
        }

        private sealed class FixedSchedulePolicy : IMatchRivalSchedulePolicy
        {
            private readonly HashSet<DayOfWeek> _activeDays;

            public FixedSchedulePolicy(IEnumerable<DayOfWeek> activeDays)
            {
                _activeDays = new HashSet<DayOfWeek>(activeDays);
            }

            public bool IsEnabled => _activeDays.Count > 0;

            public bool IsActiveDay(DayOfWeek dayOfWeek)
            {
                return _activeDays.Contains(dayOfWeek);
            }

            public DateTime GetActiveWindowEnd(DateTime now)
            {
                if (!IsEnabled || !IsActiveDay(now.DayOfWeek))
                    throw new InvalidOperationException("The MatchRival schedule is not active.");
                DateTime date = now.Date;
                for (int offset = 1; offset <= 7; offset++)
                {
                    DateTime candidate = date.AddDays(offset);
                    if (!IsActiveDay(candidate.DayOfWeek)) return candidate;
                }
                return date.AddDays(7);
            }
        }
    }

    internal sealed class CanonicalCsvTable
    {
        private readonly Dictionary<string, int> _columns;

        private CanonicalCsvTable(string name, Dictionary<string, int> columns, List<IReadOnlyList<string>> rows)
        {
            Name = name;
            _columns = columns;
            Rows = rows;
        }

        public string Name { get; }
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

        public string Value(IReadOnlyList<string> row, string column)
        {
            if (!_columns.TryGetValue(column, out int index))
                throw new FormatException($"{Name} is missing column '{column}'.");
            return index < row.Count ? row[index] : string.Empty;
        }

        public static CanonicalCsvTable Parse(string text, string name)
        {
            List<List<string>> records = ParseRecords(text);
            if (records.Count < 3)
                throw new FormatException($"{name} must contain the three canonical header rows.");
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < records[2].Count; index++)
            {
                string field = records[2][index].Trim().TrimStart('\uFEFF');
                int annotation = field.IndexOf('(');
                string column = (annotation >= 0 ? field.Substring(0, annotation) : field).Trim();
                if (column.Length == 0 || !columns.TryAdd(column, index))
                    throw new FormatException($"{name} contains an empty or duplicate column name.");
            }
            var rows = new List<IReadOnlyList<string>>();
            for (int index = 3; index < records.Count; index++)
            {
                bool hasValue = false;
                for (int fieldIndex = 0; fieldIndex < records[index].Count; fieldIndex++)
                {
                    if (!string.IsNullOrWhiteSpace(records[index][fieldIndex]))
                    {
                        hasValue = true;
                        break;
                    }
                }
                if (hasValue) rows.Add(records[index]);
            }
            return new CanonicalCsvTable(name, columns, rows);
        }

        private static List<List<string>> ParseRecords(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (quoted)
                {
                    if (character == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else quoted = false;
                    }
                    else field.Append(character);
                    continue;
                }
                if (character == '"' && field.Length == 0) quoted = true;
                else if (character == ',')
                {
                    record.Add(field.ToString());
                    field.Clear();
                }
                else if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = new List<string>();
                }
                else field.Append(character);
            }
            if (quoted) throw new FormatException("CSV contains an unterminated quoted field.");
            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }
    }

    internal static class CanonicalCsvValue
    {
        public static int ParseInt(string value, string field)
        {
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new FormatException($"{field} must be an integer.");
            return result;
        }

        public static (int X, int Y) ParseVector2Int(string value, string field)
        {
            value = value.Trim();
            if (value.Length < 5 || value[0] != '(' || value[value.Length - 1] != ')')
                throw new FormatException($"{field} must use '(x,y)' format.");
            string[] values = value.Substring(1, value.Length - 2).Split(',');
            if (values.Length != 2) throw new FormatException($"{field} must contain two integers.");
            return (ParseInt(values[0], field), ParseInt(values[1], field));
        }

        public static List<DayOfWeek> ParseDays(string value)
        {
            var result = new List<DayOfWeek>();
            string[] values = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < values.Length; index++)
            {
                if (!Enum.TryParse(values[index].Trim(), false, out DayOfWeek day)
                    || !Enum.IsDefined(typeof(DayOfWeek), day))
                    throw new FormatException($"Unsupported active day '{values[index]}'.");
                if (!result.Contains(day)) result.Add(day);
            }
            return result;
        }

        public static IReadOnlyList<ContentReward> ParseRewards(string value, string field, bool requireAny)
        {
            value = value.Trim();
            var result = new List<ContentReward>();
            if (value.Length == 0)
            {
                if (requireAny) throw new FormatException($"{field} requires at least one reward.");
                return result;
            }
            if (value.Length < 2 || value[0] != '[' || value[value.Length - 1] != ']')
                throw new FormatException($"{field} must use a reward array.");
            int index = 1;
            while (index < value.Length - 1)
            {
                while (index < value.Length - 1 && (char.IsWhiteSpace(value[index]) || value[index] == ',')) index++;
                if (index >= value.Length - 1) break;
                if (value[index] != '(') throw new FormatException($"{field} contains an invalid reward tuple.");
                int end = value.IndexOf(')', index + 1);
                if (end < 0) throw new FormatException($"{field} contains an unterminated reward tuple.");
                string[] tuple = value.Substring(index + 1, end - index - 1).Split(',');
                if (tuple.Length != 3)
                    throw new FormatException($"{field} reward tuples require type, item ID, and amount.");
                string type = tuple[0].Trim();
                string itemId = tuple[1].Trim();
                int amount = ParseInt(tuple[2], field + ".Amount");
                result.Add(new ContentReward(UsesItemKey(type) ? type + "/" + itemId : type, amount));
                index = end + 1;
            }
            if (requireAny && result.Count == 0)
                throw new FormatException($"{field} requires at least one reward.");
            return result;
        }

        private static bool UsesItemKey(string itemType)
        {
            return itemType == "BoardItem" || itemType == "Pass" || itemType == "Profile" || itemType == "Frame";
        }
    }
}
