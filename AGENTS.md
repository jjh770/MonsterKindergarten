# AGENTS.md

This file provides project guidance to coding agents working in this repository.

## Project Overview

Monster Kindergarten is a Unity 6 Android idle clicker and merge game. Grade 1 slimes spawn over time, players earn points through manual and automatic clicks, and dragging two slimes of the same grade merges them into the next grade. Points are spent on per-grade click upgrades and shared spawn interval and capacity upgrades.

The current content supports 10 slime grades. Runtime data covers currency, the highest unlocked grade, active slime counts, and upgrade levels. A guided tutorial runs on a new save, and time away from the game grants an offline auto-production reward.

## Build and Development

- Unity Editor: `6000.3.21f1`
- Primary target: Android
- Android application ID: `com.skku_say.Monster_Kindergarten`
- Startup scene: `Assets/01.Scenes/LoginScene.unity`
- Gameplay scene: `Assets/01.Scenes/GameScene.unity`
- Release profile: `Assets/Settings/Build Profiles/Android_Release.asset`
- Development profile: `Assets/Settings/Build Profiles/Android™.asset`

The release profile builds an AAB and includes LoginScene followed by GameScene. There is no supported command-line Unity build in this repository. Use Unity Play Mode for gameplay checks and a release-signed Android build or Play Console internal test for Google Play Games, Firebase, touch, device performance, and store-signing validation.

Generated `.csproj` files are not the source of truth for Unity package compatibility. A standalone `dotnet build` may fail on Firebase framework references even when the Unity project is valid; confirm compilation in the Unity Console.

## Runtime Modes

- Unity Editor and non-Android players use `LocalAccountRepository` with the fixed user ID `LocalPlayer`. Currency, slime state, and upgrades are stored locally.
- Android players use Google Play Games v2 authentication, exchange the server auth code for a Firebase Auth session, and use the Firebase UID as the save owner.
- Android game data uses `HybridRepository<T>`: PlayerPrefs saves immediately, Firebase writes are debounced by 0.6 seconds, and load resolves local and cloud data by `LastSaveTime`.
- When timestamps are equal or invalid, Firebase wins and refreshes the local copy. Keep `[FirestoreProperty]` on every cloud-persisted field, including `LastSaveTime`.

Do not change save keys, Firestore document ownership, Firebase UID handling, or serialized save fields without an explicit migration plan. Preserve existing player documents when testing schema changes.

## Architecture

### Folder Structure

Assets use numbered prefixes for project-owned content:

- `Assets/01.Scenes/` - login and gameplay scenes, plus unused Firebase and Web API learning samples
- `Assets/02.Scripts/Core/` - application and Firebase initialization
- `Assets/02.Scripts/Ingame/` - click, spawn, merge, slime, feedback, and gameplay managers
- `Assets/02.Scripts/Outgame/Feature/` - repository, domain, and manager layers for account, currency, slime, and upgrades
- `Assets/02.Scripts/Outgame/Scene/` - scene transition and login flow
- `Assets/02.Scripts/UI/` - gameplay and upgrade UI
- `Assets/03.Prefabs/` through `Assets/11.Sounds/` - project assets
- Slime and upgrade balance assets currently live beside their domain code under `Assets/02.Scripts/Outgame/Feature/`

Third-party and generated assets live under `Assets/Firebase/`, `Assets/GooglePlayGames/`, `Assets/ExternalDependencyManager/`, and `Assets/Plugins/`. Avoid editing generated dependency files unless the integration itself is being updated.

### Core Systems

**Initialization and authentication**

- `FirebaseInitializer` checks Firebase dependencies.
- `AccountManager` selects local authentication in the Editor and Google Play/Firebase authentication on Android.
- `LobbyScene` attempts silent Google Play sign-in first and supports an explicit manual retry.
- `SceneManagerEx` keeps scene transitions between LoginScene and GameScene.

**Game data**

- `CurrencyManager`, `SlimeManager`, and `UpgradeManager` own their domains and repository selection.
- `GameManager` waits for all three managers, then raises `OnAllDataInitialized` for gameplay systems.
- Repository interfaces separate local PlayerPrefs storage from Firebase Firestore storage.
- Each manager delays its initialization by one `await UniTask.Yield()` so `OnDataInitialized` fires after every subscriber has wired up in `Start`. Do not remove it.

**Offline reward and tutorial**

- `GameManager` also computes the offline reward from `CurrencyManager.LastSaveTime` and per-slime auto-production, gated by a minimum interval, a maximum accrual window, and an efficiency factor. Gameplay stays inactive until the reward popup is dismissed.
- `TutorialProgress` stores completion per user ID in PlayerPrefs and treats pre-tutorial saves as already completed.
- `TutorialManager` drives the guided steps and `GameplaySaveGate` blocks all domain saves until the tutorial finishes, at which point the three domains are committed together.

**Spawn and merge loop**

- `SpawnManager` applies saved spawn upgrades, restores saved active slimes, creates a Grade 1 slime only for a genuinely empty save, and performs timed spawning.
- `SlimeSpawner` uses Lean Pool and tracks active slime instances.
- `Clicker` uses Unity's Input System pointer API for both mouse and touch input.
- `SlimeController` handles manual clicks, dragging, point rewards, and overlap-based merge requests.
- `MergeManager` validates same-grade merges and promotes the surviving slime.
- `AutoClicker` maintains an independent timer for each active, non-dragged slime.

**Feedback**

Feedback components implement `IFeedback` and are discovered from a slime's child objects. Existing effects include color, scale, sound, and point floaters. Keep new feedback behavior component-based where possible.

`AudioManager` owns BGM and SFX sources through an Audio Mixer and mutes on application pause. `GameExitManager` handles the Android back key, closing the upgrade UI first and showing the exit confirmation popup otherwise.

### Key Patterns

- Scene-level singleton managers
- Repository interfaces with platform-specific implementations
- Domain data separated from manager orchestration
- ScriptableObject balance tables
- Event-driven initialization and UI refresh
- Component-composed feedback
- Object pooling for runtime slimes and floaters

### Physics and Input

- Slime selection uses 2D raycasts and `SlimeController` colliders.
- Dragging uses Input System `Pointer`, so mouse and touchscreen follow the same path.
- Preserve serialized drag bounds, spawn bounds, and physics settings when changing interaction code.

## Third-Party Dependencies

- DOTween - movement and feedback tweening
- Lean Pool - slime and floater pooling
- Cysharp UniTask - asynchronous initialization, authentication, and persistence
- Firebase Unity SDK 13.7.0 - Authentication and Firestore
- Google Play Games plugin / Games v2 - Android identity and server auth code
- Unity Input System 1.20.0 - pointer input
- UIEffect and TextMesh Pro - UI rendering and effects

## Current Development State

The active baseline is the Android version on `main`. Google Play Games login, Firebase UID-based saves, cloud restoration after app-data deletion, and Play Console internal installation have been device-tested. Editor gameplay intentionally bypasses Google Play login and uses local saves.

The offline reward, tutorial, exit popup, and audio systems have since shipped on `main`.

Current work is stabilization: gameplay bugs, save recovery, mobile UX, balance, and release preparation. Before considering a change complete, check the Unity Console and run the smallest relevant Play Mode scenario. Authentication, cloud recovery, touch behavior, AAB signing, and frame rate still require an Android device or internal-test build.

The `Android™` profile is the development build and is signed with the same custom keystore as the release profile, because Google Play Games sign-in rejects debug-keystore builds.

## Working Guidelines

- Prefer minimal, incremental edits and preserve existing public APIs and serialized references.
- Do not modify package, generated resolver, Firebase configuration, or Google Play Games files as incidental cleanup.
- Keep platform behavior explicit; Editor-local behavior must not silently replace Android cloud behavior.
- Treat Unity Play Mode and Android device results separately from static or `.csproj` checks.
- Preserve unrelated working-tree changes and inspect the exact Git diff before staging.
- Follow `Documentation/CODING_CONVENTION.md`. Its Law of Demeter section lists explicit exceptions for data structures and Unity framework APIs.
- Do not report a performance problem without measuring it on an Android device. See `Documentation/PLAYERPREFS_SAVE_PROFILING.md` for the method and for a hypothesis that measurement rejected.
