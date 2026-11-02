# 🕹️ Cartridge

**Cartridge** is a lightweight, zero-dependency, engine-agnostic C# game core toolkit. Write your meta-game and core gameplay systems once and plug them directly into **Unity**, **Godot 4 (.NET)**, **MonoGame**, or any custom C# engine.

---

## ✨ Why Cartridge?

- **100% Engine-Agnostic:** Pure C# with zero dependencies on `UnityEngine`, `Godot`, or external NuGet packages.
- **Multi-Targeted:** Targets `netstandard2.1` (for Unity 2021.2+) and `net10.0` (for Godot .NET, MonoGame, and desktop apps).
- **Single Heartbeat Loop:** One `CartridgeHost.Tick(deltaTime)` call updates combo decay, temporary score buffs, cooldowns, timers, and state machines—with built-in `IsPaused`, `TimeScale` (slow-mo/fast-forward), and `UnscaledTimers`.
- **One-Line Save/Load:** Includes a built-in zero-dependency JSON serializer (`CartridgeProfileJson`) that snapshots and restores High Scores, Leaderboards, XP/Levels, Multi-Currency Wallets, Achievements, and custom game flags.

---

## 📦 Architecture & Modules

```text
cartridge/
├── src/Cartridge/
│   ├── CartridgeHost.cs              # Central coordinator (Tick, Pause, TimeScale, Save/Load)
│   ├── Scoring/
│   │   ├── ScoreManager.cs           # Points, timed buffs (Additive/Multiplicative), style variety
│   │   ├── ComboTracker.cs           # Timed combo chains & multiplier tiers
│   │   ├── RankAndSummary.cs         # End-of-round breakdown & S/A/B/C/D RankEvaluator
│   │   └── Leaderboard.cs            # Top-N sorted arcade leaderboard
│   ├── Progression/
│   │   └── ProgressionSystems.cs     # ExperienceSystem (XP curves), Wallet, AchievementTracker
│   ├── State/
│   │   └── StateAndEvents.cs         # Generic StateMachine<TStateId> & pub/sub EventBus
│   ├── Timing/
│   │   └── TimingSystems.cs          # CountdownTimer, Cooldown, TimerManager
│   ├── Random/
│   │   └── RandomSystems.cs          # Deterministic SeededRng, weighted LootTable<T>, Deck<T>
│   ├── Persistence/
│   │   ├── SaveStorage.cs            # ISaveStorage, FileSaveStorage, InMemorySaveStorage
│   │   └── CartridgeProfile.cs       # Zero-dependency JSON profile serializer
│   ├── package.json                  # Unity Package Manager (UPM) manifest
│   └── Cartridge.asmdef              # Unity Assembly Definition
├── adapters/
│   ├── Unity/CartridgeBehaviour.cs   # Ready-to-use Unity MonoBehaviour singleton
│   └── Godot/CartridgeAutoload.cs    # Ready-to-use Godot 4 Autoload Node
└── tests/Cartridge.Tests/            # Automated verification suite
```

---

## 🚀 Quick Start

### 1. Using `CartridgeHost` (Recommended)

`CartridgeHost` wires all core modules together and gives you a single `Tick(deltaTime)` method:

```csharp
using Cartridge;
using Cartridge.Persistence;
using Cartridge.Scoring;

// Initialize with file storage (or InMemorySaveStorage)
var host = new CartridgeHost(
    storage: new FileSaveStorage("./saves"),
    rngSeed: 1337,
    comboDecaySeconds: 3.0f
);

// Load existing player profile if present
host.LoadProfile("slot_1");

// Subscribe to events for UI/Audio feedback
host.Score.OnPointsAdded += evt =>
    Console.WriteLine($"+{evt.FinalPoints} pts ({evt.Reason}) [Mult: {evt.Multiplier:0.0}x]");

host.Combo.OnComboBroken += lostCombo =>
    Console.WriteLine($"Combo broken at {lostCombo} hits!");

// Award points during gameplay
host.Score.AddPoints(100, "Headshot");

// Trigger a 2x Double Points power-up for 10 seconds
host.Score.AddTemporaryMultiplier(2.0f, durationSeconds: 10.0f, tag: "DoublePoints");

// In your engine's update loop:
host.Tick(deltaTime);

// End of round: get itemized breakdown and S/A/B/C/D grade
RoundSummary summary = host.BuildRoundSummary();
Console.WriteLine($"Final Score: {summary.TotalScore} | Max Combo: {summary.MaxCombo} | Grade: {summary.Grade}");

// Save everything (HighScore, Leaderboard, XP, Wallet, Achievements, CustomData)
host.SaveProfile("slot_1");
```

---

### 2. Advanced Scoring Features

#### Timed Buff Stacking (`Multiplicative` vs `Additive`)
```csharp
// Multiplicative (default): 2.0x buff + 1.5x buff = 3.0x total
host.Score.BuffStackMode = BuffStackMode.Multiplicative;

// Additive: 2.0x (+100%) + 1.5x (+50%) = 2.5x total
host.Score.BuffStackMode = BuffStackMode.Additive;
```

#### Style & Move Variety (Anti-Spam)
Reward players for mixing up their actions and penalize button mashing:
```csharp
host.Score.Style.Enabled = true;
host.Score.Style.HistoryWindowSize = 4;
host.Score.Style.StaleMoveDecayPerRepeat = 0.2f;      // -20% per consecutive repeat
host.Score.Style.MinStaleMultiplier = 0.25f;          // Floor at 0.25x
host.Score.Style.FullVarietyBonusMultiplier = 1.5f;   // 1.5x bonus when last 4 moves are all unique!
```

#### Customizing End-of-Round Ranks (`S`, `A`, `B`, `C`, `D`)
```csharp
host.Score.RankEvaluator = new RankEvaluator(fallbackGrade: "D")
    .AddTier("S", minScore: 15000, minMaxCombo: 30, maxTimeSeconds: 120f)
    .AddTier("A", minScore: 10000, minMaxCombo: 15)
    .AddTier("B", minScore: 5000)
    .AddTier("C", minScore: 2000);
```

---

### 3. Progression, State, Timing & RNG

```csharp
// XP & Leveling
host.Experience.OnLevelUp += newLevel => Console.WriteLine($"Level Up! Lv.{newLevel}");
host.Experience.AddXp(500);

// Multi-Currency Wallet
host.Wallet.Deposit("Gold", 250);
if (host.Wallet.TrySpend("Gold", 100))
{
    // Purchased item!
}

// Cooldowns & Countdown Timers
var dashCooldown = host.Timers.Register(new Cooldown(1.5f));
if (dashCooldown.TryUse())
{
    // Perform dash! Automatically recharges via host.Tick(deltaTime)
}

// Weighted Loot Table & Seeded RNG
var chestLoot = new LootTable<string>()
    .Add("HealthPotion", weight: 70f)
    .Add("RareGem", weight: 25f)
    .Add("MythicSword", weight: 5f);

string drop = chestLoot.Roll(host.Rng);
```

---

## 🎮 Engine Integration Guide

### Unity (2021.2+)
1. Open **Window $\rightarrow$ Package Manager**.
2. Click **`+` $\rightarrow$ Add package from disk...**
3. Select `src/Cartridge/package.json`.
4. *(Optional)* Copy [`adapters/Unity/CartridgeBehaviour.cs`](adapters/Unity/CartridgeBehaviour.cs) into your project's `Assets/Scripts/` folder and attach it to a bootstrap GameObject.

### Godot 4 (.NET)
1. Add a project reference to your game's `.csproj`:
   ```xml
   <ItemGroup>
     <ProjectReference Include="../cartridge/src/Cartridge/Cartridge.csproj" />
   </ItemGroup>
   ```
2. *(Optional)* Copy [`adapters/Godot/CartridgeAutoload.cs`](adapters/Godot/CartridgeAutoload.cs) into your Godot project and register it under **Project Settings $\rightarrow$ Globals $\rightarrow$ Autoload**.

---

## 🧪 Running Tests

```bash
dotnet run --project tests/Cartridge.Tests/Cartridge.Tests.csproj
```
