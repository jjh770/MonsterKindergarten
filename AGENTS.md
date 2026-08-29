# AGENTS.md

This file provides project guidance to coding agents working in this repository.

## Project Overview

Monster Kindergarten is a Unity 6 Android idle clicker and merge game. Grades 1–6 can spawn over time according to unlocks and spawn-weight upgrades. Players earn points through manual and automatic clicks, merge matching grades, and upgrade production, spawn interval, capacity, and higher-grade spawn weights.

The current content supports 20 slime grades: 1–10 on Ground and 11–20 on Sky. DisplayRoom unlocks at grade 3 and supports storage, inspection, and observation. Stored slimes produce no points and do not occupy main-stage capacity. Runtime data includes currency, individual slime IDs and locations, stage progress, and upgrades. Tutorials guide new features, and time away grants an offline auto-production reward.

## Build and Development

- Unity Editor: `6000.3.21f1`
- Primary target: Android
- Android application ID: `com.skku_say.Monster_Kindergarten`
- Startup scene: `Assets/01.Scenes/LoginScene.unity`
- Gameplay scene: `Assets/01.Scenes/GameScene.unity`
- Release profile: `Assets/Settings/Build Profiles/Android_Release.asset`
- Development profile: `Assets/Settings/Build Profiles/Android™.asset`
- Release profile version: `0.1.05` (Android Version Code `6`)
- Development profile version: `0.1.03` (Android Version Code `4`)
- Version snapshot: 2026-08-27. Profile-specific Player Settings override the project-wide version.

The release profile builds an AAB with Development Build disabled; the development profile builds an APK with Development Build enabled. Both include LoginScene followed by GameScene. There is no supported command-line Unity build in this repository. The user performs Unity Play Mode and device testing; do not run builds or add test scripts unless requested. Static checks do not verify Google Play Games, Firebase, touch, device performance, or store signing.

Generated `.csproj` files are not the source of truth for Unity package compatibility. A standalone `dotnet build` may fail on Firebase framework references even when the Unity project is valid; confirm compilation in the Unity Console.

## Runtime Modes

- Android is the only supported build target. WebGL support was removed on 2026-08-27 along with its Firestore attribute stub and browser focus plugin; do not reintroduce `UNITY_WEBGL` branches.
- Unity Editor and non-Android players use `LocalAccountRepository` with the fixed user ID `LocalPlayer`. Currency, slime state, and upgrades are stored locally.
- Android players use Google Play Games v2 authentication, exchange the server auth code for a Firebase Auth session, and use the Firebase UID as the save owner.
- Android game data uses `HybridRepository<T>`: PlayerPrefs saves immediately, Firebase writes are debounced by 0.6 seconds, and load resolves local and cloud data by `LastSaveTime`.
- When timestamps are equal or invalid, Firebase wins and refreshes the local copy. Keep `[FirestoreProperty]` on every cloud-persisted field, including `LastSaveTime`.

Do not change save keys, Firestore document ownership, Firebase UID handling, or serialized save fields without an explicit migration plan. Preserve existing player documents when testing schema changes.

## Architecture

### Folder Structure

Assets use numbered prefixes for project-owned content:

- `Assets/01.Scenes/` - login and gameplay scenes
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
- SlimeInstance is a domain object; SlimeInstanceSaveData owns persistence mapping. Save schemas are Currency 1, SlimeStatus 2, and Upgrade 1. Preserve deterministic legacy migration IDs and future-version rejection in slime repositories.
- Each manager delays its initialization by one `await UniTask.Yield()` so `OnDataInitialized` fires after every subscriber has wired up in `Start`. Do not remove it.

**Offline reward and tutorial**

- `GameManager` also computes the offline reward from `CurrencyManager.LastSaveTime` and per-slime auto-production, gated by a minimum interval, a maximum accrual window, and an efficiency factor. Gameplay stays inactive until the reward popup is dismissed.
- `TutorialProgress` stores completion per user ID in PlayerPrefs and treats existing progress as completion of the main tutorial. DisplayRoom remains incomplete until its final dialogue; these local flags are not cloud-synced.
- `TutorialManager` owns execution and shared presentation; MainTutorialSequence, HigherGradeSpawnTutorialSequence, and DisplayRoomTutorialSequence own their steps. `GameplaySaveGate` blocks progress saves during the main tutorial, not DisplayRoom transfers.
- DisplayRoom guidance requires both gameplay activation and `SpawnManager.IsInitialized`. `SpawnManager.Initialized` fires after restoration and first spawn. If a stored slime already exists, resume entry/info guidance without requesting another transfer.
- Higher-grade spawn guidance calls `BottomPanelSwitcher.TryShowSystemUpgradePanel()` before focusing the carousel. Selecting an upgrade does not make a hidden panel visible.
- Offline reward is not offline play. Android cold start still requires Google Play/Firebase login before GameScene loads; do not describe offline play as supported.
- Offline elapsed time uses the device's `DateTime.UtcNow`, with a 60-second minimum, an 8-hour cap, and 50% efficiency. Revisit server-authoritative settlement before adding rankings, competition, or paid-currency dependencies.

**Spawn and merge loop**

- `SpawnManager` applies saved upgrades, restores individual slimes in both locations, and creates a Grade 1 slime if no MainStage slime remains, even when DisplayRoom contains slimes. Timed spawning uses the unlocked weighted pool.
- `SlimeSpawner` uses Lean Pool and tracks active slime instances.
- `Clicker` uses Unity's Input System pointer API for both mouse and touch input.
- `SlimeController` handles manual clicks, dragging, point rewards, and overlap-based merge requests.
- `MergeManager` validates same-grade merges and promotes the surviving slime.
- `AutoClicker` maintains an independent timer for each eligible, non-dragged main-stage slime; DisplayRoom is excluded from manual, automatic, and offline production.

**Feedback**

Feedback components implement `IFeedback` and are discovered from a slime's child objects. Existing effects include color, scale, sound, and point floaters. Keep new feedback behavior component-based where possible.

`AudioManager` owns scene-authored BGM and SFX sources through an Audio Mixer, stores per-device BGM/SFX volume preferences, and mutes on application pause. `GameExitManager` dispatches registered back handlers before falling back to upgrade closing and the exit popup.

**Gameplay UI**

- `UpgradeUI` derives its closed position from the actual panel width and applies `Screen.safeArea` insets. Layout refresh is event-driven through rect-size, focus, and pause callbacks; do not restore a fixed movement distance or per-frame layout polling.
- `GameExitManager` depends on the public `UpgradeUI.TryClose()` API. Preserve that API and its close-first behavior when changing the upgrade panel.
- `BottomPanelSwitcher` owns bottom-panel selection and presentation; `StageUI` owns the Ground/Sky button, and `SpaceToggleButtonUI` owns the DisplayRoom/MainStage button label and click event. `DisplayRoomUI` orchestrates transfers and space changes.
- `HudVisibility` owns the position of TopHudRoot and BottomHudRoot. Send mode requests `Top`, observation requests `All`, and requests are stacked per owner so overlapping presentations stay safe. Never cache or restore those roots' positions elsewhere; a second owner that remembers a displaced position leaves the HUD off screen.
- The upgrade drawer is not a HUD part. `UpgradeUI` computes its own hidden position from panel width and safe-area insets, so hide it through `SetToggleVisible()` instead of moving its transform.
- Keep static UI, audio sources, and references authored in scenes/prefabs rather than constructing their hierarchy at runtime.
- `Clicker.PushMode(owner, mode, priority)` / `ReleaseMode(owner)` arbitrate world input: Space < Selection < Tutorial < Modal. Same-owner updates keep their position; release only the owner's request on completion or teardown.
- `StageManager.PlayDisplayRoomTransfer()` starts a space transfer and `StageManager.TryRelocateSlime()` finishes it: save location, reposition, refresh presentation, and restore the pre-transfer position on failure. UI owns only the policy around it - toast text, input restore, popup closing. Do not reimplement the completion half in a caller.
- Never place a `Button` or other `Selectable` under a `Slider`, `Scrollbar`, `ScrollRect`, or any `IDragHandler`. `Slider.OnInitializePotentialDrag` clears the drag threshold, so any finger movement starts a parent drag and cancels the child's click. The spawn gauge keeps its `Slider` on a dedicated `SpawnBar` child for this reason.

**Options, progress reset, and account deletion**

- `OptionsUI` provides volume sliders plus separate confirmations for progress reset and game-account deletion.
- `GameDataResetService` deletes only the current UID's Currency, SlimeStatus, and Upgrade documents on Android, plus that user's local progress and tutorial flags. Editor deletes local progress only; authentication accounts and audio preferences remain.
- `GameAccountDeletionService` runs the same data deletion first, then deletes the Firebase Auth user. It keeps a per-UID pending marker so LobbyScene can resume an interrupted deletion before GameScene entry. Never delete authentication before its Firestore documents.
- Reset locks gameplay/saves, invalidates old debounced writes by ResetGeneration, waits for pending Firestore writes, then deletes. A local pending marker resumes interrupted resets in LobbyScene before GameScene entry.
- A reset timeout does not cancel the server operation. Do not resume the old game while the result is uncertain. Return to login without automatic sign-in after reset.
- Other devices' local saves are not invalidated and can restore old cloud progress later. Account-wide reset generations are not implemented.

### Key Patterns

- Scene-level singleton managers. Declare as `public static T Instance { get; private set; }` and guard `Awake` with `if (Instance != null && Instance != this) { Destroy(gameObject); return; }` before assigning. The `Instance != this` check keeps a re-entered `Awake` from destroying the already registered instance.
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

Current work on `feature/phase-2-display-room` includes Phase 2/2-B DisplayRoom, observation UX, tutorials, options, and follow-up fixes. Next feature phase is Phase 3 (collection book); collection, gacha, and special-slime gameplay are not implemented yet.

As of 2026-08-27, a local `0.1.05` AAB, `Builds/Release/0.1.05/build-info.txt`, and `release-notes.txt` exist. The AAB was built from HEAD `6f2b2e7` plus uncommitted release-profile and project-setting changes, so HEAD alone does not identify the full build source. Existing Unity confirmations do not establish this AAB's Android login/save validation or store upload.

A structural cleanup pass landed before that build: `HudVisibility` took over HUD hiding, `SpaceToggleButtonUI` took the space button out of `DisplayRoomUI`, `StageManager.TryRelocateSlime()` absorbed the duplicated transfer completion, singleton declarations and `Awake` guards were unified, WebGL support was removed, and the spawn gauge `Slider` moved onto a `SpawnBar` child so its sibling buttons stop losing clicks. These have static-check evidence and Editor confirmation of the send/observation/transfer paths only.

The responsive UpgradeUI/Safe Area work and drag-merge target feedback have implementation and static-check evidence, but no recorded multi-resolution Play Mode or Android device validation. Re-run those scenarios before treating them as release-verified.

The `Android™` profile is the development build and is signed with the same custom keystore as the release profile, because Google Play Games sign-in rejects debug-keystore builds.

## Working Guidelines

- Prefer minimal, incremental edits and preserve existing public APIs and serialized references.
- Check the current branch, working tree, app version, and release profile before starting a version-scoped change. Work directly on `main` only when the user explicitly chooses that flow.
- Do not modify package, generated resolver, Firebase configuration, or Google Play Games files as incidental cleanup.
- Keep platform behavior explicit; Editor-local behavior must not silently replace Android cloud behavior.
- Treat Unity Play Mode and Android device results separately from static or `.csproj` checks.
- Preserve unrelated working-tree changes and inspect the exact Git diff before staging.
- Record release handoffs under `Builds/Release/<version>/build-info.txt`; the `Builds/` directory is intentionally ignored by Git.
- Use `Feat :`, `Fix :`, `Chore :`, or `Docs :` commit subjects with concise Korean bullets when a body is useful.
- Follow `Documentation/CODING_CONVENTION.md`. Its Law of Demeter section lists explicit exceptions for data structures and Unity framework APIs.
- Do not report a performance problem without measuring it on an Android device. See `Documentation/PLAYERPREFS_SAVE_PROFILING.md` for the method and for a hypothesis that measurement rejected.
