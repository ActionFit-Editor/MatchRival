using System;
using System.IO;
using NUnit.Framework;

namespace ActionFit.MatchRival.Tests
{
    public sealed class MatchRivalStandaloneCatalogParityTests
    {
        [Test]
        public void CanonicalCsvFactory_DefaultAndRewardSegmentsPreserveReleasedBalance()
        {
            MatchRivalCatalogCsvData csv = ReadCsv();
            MatchRivalStandaloneCatalog defaultStandalone = MatchRivalCatalogFactory.Create(csv);
            MatchRivalStandaloneCatalog rewardStandalone = MatchRivalCatalogFactory.Create(
                csv,
                MatchRivalCatalogFactory.RewardSegment);
            MatchRivalCatalog catalog = defaultStandalone.Catalog;

            Assert.That(catalog.CatalogVersion, Is.EqualTo(MatchRivalCatalogFactory.DefaultCatalogVersion));
            Assert.That(catalog.BalanceRevision, Is.EqualTo(MatchRivalCatalogFactory.DefaultBalanceRevision));
            Assert.That(rewardStandalone.Catalog.BalanceRevision, Is.EqualTo(MatchRivalCatalogFactory.RewardBalanceRevision));
            Assert.That(defaultStandalone.SchedulePolicy.IsActiveDay(DayOfWeek.Saturday), Is.True);
            Assert.That(defaultStandalone.SchedulePolicy.IsActiveDay(DayOfWeek.Sunday), Is.True);
            Assert.That(defaultStandalone.SchedulePolicy.IsActiveDay(DayOfWeek.Monday), Is.False);
            Assert.That(
                defaultStandalone.SchedulePolicy.GetActiveWindowEnd(new DateTime(2026, 7, 18)),
                Is.EqualTo(new DateTime(2026, 7, 20)));

            Assert.That(catalog.GetOrderBeans(3), Is.EqualTo(3));
            Assert.That(rewardStandalone.Catalog.GetOrderBeans(3), Is.EqualTo(2));
            Assert.That(catalog.GetOrderBeans(12), Is.EqualTo(120));
            Assert.That(rewardStandalone.Catalog.GetOrderBeans(12), Is.EqualTo(96));
            Assert.That(catalog.GetDifficulty(2).RequiredBeans, Is.EqualTo(30));
            Assert.That(rewardStandalone.Catalog.GetDifficulty(2).RequiredBeans, Is.EqualTo(40));
            Assert.That(catalog.GetDifficulty(2).EasyMinSeconds, Is.EqualTo(13 * 60));
            Assert.That(rewardStandalone.Catalog.GetDifficulty(2).HardMinSeconds, Is.EqualTo(7 * 60));
            Assert.That(catalog.GetRoundRewards(10).WinRewards[0].Amount, Is.EqualTo(70));
            Assert.That(catalog.GetRoundRewards(10).LoseRewards[0].Amount, Is.EqualTo(10));
            Assert.That(catalog.TryGetBoxRewards(4, out MatchRivalBoxRewards defaultBox), Is.True);
            Assert.That(rewardStandalone.Catalog.TryGetBoxRewards(4, out MatchRivalBoxRewards rewardBox), Is.True);
            Assert.That(defaultBox.Rewards[1].Amount, Is.EqualTo(30));
            Assert.That(rewardBox.Rewards[1].Amount, Is.EqualTo(20));
            Assert.That(catalog.TryGetBoxRewards(3, out _), Is.False);
        }

        [Test]
        public void CanonicalCsvFactory_UnsupportedSegmentFailsClosed()
        {
            Assert.Throws<ArgumentException>(() => MatchRivalCatalogFactory.Create(ReadCsv(), "UnknownVariant"));
        }

        private static MatchRivalCatalogCsvData ReadCsv()
        {
            return new MatchRivalCatalogCsvData(
                Read("MatchRival_Bean_Order.csv"),
                Read("MatchRival_Difficulty.csv"),
                Read("MatchRival_EventSettings.csv"),
                Read("MatchRival_Reward_Box.csv"),
                Read("MatchRival_Reward_Round.csv"));
        }

        private static string Read(string fileName)
        {
            return File.ReadAllText(Path.Combine(
                "Packages/com.actionfit.match-rival/Data/CSV",
                fileName));
        }
    }
}
