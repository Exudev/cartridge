using System;
using Cartridge;
using Cartridge.Persistence;
using Cartridge.Progression;
using Cartridge.Random;
using Cartridge.Scoring;
using Cartridge.State;
using Cartridge.Timing;

namespace Cartridge.Tests
{
    internal static class Program
    {
        private enum GameFlow { Menu, Playing, Paused, GameOver }

        private static int Main()
        {
            Console.WriteLine("Running Cartridge Core Test Suite...");

            TestScoringAndCombos();
            TestScoreBuffStackModes();
            TestStyleAndRoundSummaryRanks();
            TestLeaderboard();
            TestProgression();
            TestStateAndEvents();
            TestTiming();
            TestRandomAndLoot();
            TestHostPauseTimeScaleAndProfileSaveLoad();

            Console.WriteLine("All Cartridge tests passed!");
            return 0;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion failed: " + message);
            }
        }

        private static void TestScoringAndCombos()
        {
            var combo = new ComboTracker(decayTimeSeconds: 2.0f);
            var score = new ScoreManager(initialHighScore: 500, comboTracker: combo);

            for (int i = 0; i < 4; i++)
            {
                score.AddPoints(100, "EnemyDefeated");
            }
            Assert(score.CurrentScore == 400, "Expected 400 points after 4 hits at 1x");
            Assert(combo.CurrentCombo == 4, "Expected 4 combo");

            var fifth = score.AddPoints(100, "Headshot");
            Assert(fifth.FinalPoints == 200, "5th hit should award 200 points at 2x");
            Assert(score.CurrentScore == 600, "Total score should be 600");
            Assert(score.HighScore == 600, "HighScore should update to 600");

            combo.Tick(2.5f);
            Assert(combo.CurrentCombo == 0 && combo.CurrentMultiplier == 1f, "Combo should reset after decay");
        }

        private static void TestScoreBuffStackModes()
        {
            var score = new ScoreManager();

            // 1. Multiplicative mode: 2.0x and 1.5x -> 3.0x
            score.BuffStackMode = BuffStackMode.Multiplicative;
            score.AddTemporaryMultiplier(2.0f, durationSeconds: 5.0f, tag: "Shrine");
            score.AddTemporaryMultiplier(1.5f, durationSeconds: 2.0f, tag: "Potion");
            Assert(Math.Abs(score.ActiveBuffMultiplier - 3.0f) < 0.001f, "Multiplicative buffs should be 3.0x");

            // 2. Additive mode: 2.0x (+100%) and 1.5x (+50%) -> 2.5x
            score.BuffStackMode = BuffStackMode.Additive;
            Assert(Math.Abs(score.ActiveBuffMultiplier - 2.5f) < 0.001f, "Additive buffs should be 2.5x");

            // Tick 2.5s so the 1.5x potion expires, leaving only the 2.0x shrine
            score.Tick(2.5f);
            Assert(score.ActiveBuffs.Count == 1, "One buff should remain after 2.5s");
            Assert(Math.Abs(score.ActiveBuffMultiplier - 2.0f) < 0.001f, "Remaining buff should be 2.0x");
        }

        private static void TestStyleAndRoundSummaryRanks()
        {
            var combo = new ComboTracker(decayTimeSeconds: 5.0f);
            var score = new ScoreManager(comboTracker: combo);
            score.Style.Enabled = true;
            score.Style.HistoryWindowSize = 3;
            score.Style.StaleMoveDecayPerRepeat = 0.25f;
            score.Style.FullVarietyBonusMultiplier = 1.5f;

            // 1st "Slash" -> 1.0x style (100 pts)
            var e1 = score.AddPoints(100, "Slash", registerComboHit: false);
            Assert(e1.FinalPoints == 100, "First Slash should give 100");

            // 2nd consecutive "Slash" -> 0.75x stale move decay (75 pts)
            var e2 = score.AddPoints(100, "Slash", registerComboHit: false);
            Assert(e2.FinalPoints == 75, "Repeated Slash should decay to 75");

            // Now perform 3 distinct moves to fill the window of 3: "Slash" (already at tail), "Kick", "Parry"
            var e3 = score.AddPoints(100, "Kick", registerComboHit: false);
            Assert(e3.FinalPoints == 100, "Kick is new -> 1.0x");

            var e4 = score.AddPoints(100, "Parry", registerComboHit: false);
            // Window is now ["Slash", "Kick", "Parry"] -> all 3 distinct -> 1.5x FullVarietyBonus!
            Assert(e4.FinalPoints == 150, "3 distinct moves should award 1.5x variety bonus (150 pts)");

            // Build summary and verify breakdown and Rank evaluation
            score.RankEvaluator = new RankEvaluator("D")
                .AddTier("S", minScore: 400)
                .AddTier("A", minScore: 200);

            var summary = score.BuildRoundSummary(elapsedSeconds: 12.5f);
            Assert(summary.TotalScore == 425, "Total score should be 425");
            Assert(summary.Grade == "S", "Should achieve S rank (>= 400)");
            Assert(summary.PointsByReason["Slash"] == 175, "Slash breakdown should be 175");
            Assert(summary.PointsByReason["Parry"] == 150, "Parry breakdown should be 150");
        }

        private static void TestLeaderboard()
        {
            var board = new Leaderboard(maxEntries: 3);
            board.TrySubmit("ACE", 1000);
            board.TrySubmit("BOB", 2500);
            board.TrySubmit("CAT", 1800);
            int rank = board.TrySubmit("DAN", 3000);

            Assert(rank == 1, "DAN should be rank 1");
            Assert(board.Entries.Count == 3, "Leaderboard should cap at 3 entries");
            Assert(board.Entries[0].PlayerName == "DAN" && board.Entries[1].PlayerName == "BOB", "Sorting check");
        }

        private static void TestProgression()
        {
            var xp = new ExperienceSystem(startingLevel: 1, maxLevel: 10, xpToNextLevelCurve: lvl => lvl * 100);
            int gained = xp.AddXp(250);
            Assert(gained == 1 && xp.CurrentLevel == 2 && xp.CurrentXp == 150, "XP level up calculation");

            var wallet = new Wallet();
            wallet.Deposit("Gold", 500);
            Assert(wallet.TrySpend("Gold", 200), "Should afford 200 Gold");
            Assert(wallet.GetBalance("Gold") == 300, "Remaining Gold should be 300");
            Assert(!wallet.TrySpend("Gold", 400), "Should reject overspending");

            var achievements = new AchievementTracker();
            achievements.Register(new Achievement("slayer", "Slayer", "Defeat 3 bosses", targetProgress: 3));
            Assert(!achievements.AddProgress("slayer", 2), "Not unlocked at 2/3");
            Assert(achievements.AddProgress("slayer", 1), "Unlocked at 3/3");
        }

        private static void TestStateAndEvents()
        {
            bool enteredPlaying = false;
            var fsm = new StateMachine<GameFlow>()
                .AddState(GameFlow.Menu)
                .AddState(GameFlow.Playing, onEnter: () => enteredPlaying = true);

            fsm.ChangeState(GameFlow.Menu);
            fsm.ChangeState(GameFlow.Playing);
            Assert(enteredPlaying && fsm.CurrentStateId == GameFlow.Playing, "FSM should transition to Playing");

            var bus = new EventBus();
            int receivedScore = 0;
            bus.Subscribe<int>(val => receivedScore = val);
            bus.Publish(42);
            Assert(receivedScore == 42, "EventBus should deliver payload");
        }

        private static void TestTiming()
        {
            var manager = new TimerManager();
            bool completed = false;
            var timer = manager.Register(new CountdownTimer(1.0f));
            timer.OnCompleted += () => completed = true;

            var dashCooldown = manager.Register(new Cooldown(2.0f));
            Assert(dashCooldown.TryUse(), "First dash should succeed");
            Assert(!dashCooldown.TryUse(), "Second dash should be on cooldown");

            manager.Tick(1.2f);
            Assert(completed, "CountdownTimer should complete after 1.2s");
            Assert(!dashCooldown.IsReady, "Dash cooldown still has 0.8s left");

            manager.Tick(1.0f);
            Assert(dashCooldown.IsReady, "Dash cooldown should be ready after 2.2s");
        }

        private static void TestRandomAndLoot()
        {
            var rng1 = new SeededRng(12345);
            var rng2 = new SeededRng(12345);
            Assert(rng1.Range(0, 1000) == rng2.Range(0, 1000), "SeededRng must be deterministic");

            var loot = new LootTable<string>()
                .Add("CommonSword", 90f)
                .Add("LegendaryBlade", 10f);
            string drop = loot.Roll(rng1);
            Assert(drop == "CommonSword" || drop == "LegendaryBlade", "LootTable should return valid item");

            var deck = new Deck<int>(new[] { 1, 2, 3 }, rng1);
            int c1 = deck.Draw();
            int c2 = deck.Draw();
            int c3 = deck.Draw();
            Assert(c1 + c2 + c3 == 6, "Deck should draw all 3 cards before reshuffling");
            int c4 = deck.Draw();
            Assert(c4 >= 1 && c4 <= 3, "Deck should auto-reshuffle discard pile");
        }

        private static void TestHostPauseTimeScaleAndProfileSaveLoad()
        {
            var sharedStorage = new InMemorySaveStorage();
            var host = new CartridgeHost(storage: sharedStorage, comboDecaySeconds: 4.0f);

            host.Achievements.Register(new Achievement("first_win", "First Win", "Win a match", 1));
            host.Score.AddPoints(2500, "BossClear");
            host.Leaderboard.TrySubmit("EXU", host.Score.CurrentScore);
            host.Wallet.Deposit("Gems", 75);
            host.Experience.AddXp(350);
            host.Achievements.Unlock("first_win");
            host.SetCustomData("selected_ship", "viper_mk2");

            // Test Pause & UnscaledTimers vs Scaled Timers
            bool scaledFired = false;
            bool unscaledFired = false;
            var scaledTimer = host.Timers.Register(new CountdownTimer(1.0f));
            var unscaledTimer = host.UnscaledTimers.Register(new CountdownTimer(1.0f));
            scaledTimer.OnCompleted += () => scaledFired = true;
            unscaledTimer.OnCompleted += () => unscaledFired = true;

            host.IsPaused = true;
            host.Tick(1.5f);
            Assert(!scaledFired, "Scaled timer must not fire while paused");
            Assert(unscaledFired, "Unscaled timer must fire even while paused");
            Assert(host.Combo.CurrentCombo == 1, "Combo must not decay while paused");

            // Unpause with 0.5x slow-motion
            host.IsPaused = false;
            host.TimeScale = 0.5f;
            host.Tick(1.0f); // 0.5s scaled
            Assert(!scaledFired, "Scaled timer at 0.5x needs 2.0 real seconds");
            host.Tick(1.2f); // +0.6s scaled -> 1.1s total scaled
            Assert(scaledFired, "Scaled timer should fire now");

            // Save profile to JSON
            string json = host.SaveProfile("slot_1");
            Assert(json.Contains("viper_mk2") && json.Contains("EXU"), "Serialized JSON should contain custom data and leaderboard");

            // Load into a brand new CartridgeHost instance
            var loadedHost = new CartridgeHost(storage: sharedStorage);
            loadedHost.Achievements.Register(new Achievement("first_win", "First Win", "Win a match", 1));
            bool loaded = loadedHost.LoadProfile("slot_1");

            Assert(loaded, "Profile should load from storage");
            Assert(loadedHost.Score.HighScore == 2500, "Restored HighScore should be 2500");
            Assert(loadedHost.Wallet.GetBalance("Gems") == 75, "Restored Gems should be 75");
            Assert(loadedHost.Experience.CurrentLevel == host.Experience.CurrentLevel, "Restored Level should match");
            Assert(loadedHost.Leaderboard.Entries.Count == 1 && loadedHost.Leaderboard.Entries[0].PlayerName == "EXU", "Restored Leaderboard should match");
            Assert(loadedHost.Achievements.Achievements["first_win"].IsUnlocked, "Restored achievement should be unlocked");
            Assert(loadedHost.GetCustomData("selected_ship") == "viper_mk2", "Restored custom data should match");
        }
    }
}
