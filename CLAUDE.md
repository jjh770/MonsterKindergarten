# CLAUDE.md

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
- Release profile version: `0.1.09` (Android Version Code `10`)
- Development profile version: `0.1.09` (Android Version Code `10`)
- Version snapshot: 2026-09-09. No release build carries the gacha work yet. Profile-specific Player Settings override the project-wide version.

The release profile builds an AAB with Development Build disabled; the development profile builds an APK with Development Build enabled. Both include LoginScene followed by GameScene. There is no supported command-line Unity build in this repository. Static checks do not verify Google Play Games, Firebase, touch, device performance, or store signing.

The `Android™` profile is signed with the same custom keystore as the release profile, because Google Play Games sign-in rejects debug-keystore builds.

Generated `.csproj` files are not the source of truth for Unity package compatibility. A standalone `dotnet build` may fail on Firebase framework references even when the Unity project is valid; confirm compilation in the Unity Console.

## Runtime Modes

- Android is the only supported build target. WebGL support was removed on 2026-08-27 along with its Firestore attribute stub and browser focus plugin; do not reintroduce `UNITY_WEBGL` branches.
- Unity Editor and non-Android players use `LocalAccountRepository` with the fixed user ID `LocalPlayer`. Currency, slime state, and upgrades are stored locally.
- Android players use Google Play Games v2 authentication, exchange the server auth code for a Firebase Auth session, and use the Firebase UID as the save owner.
- Android game data uses `HybridRepository<T>`: PlayerPrefs saves immediately, Firebase writes are throttled to one every 5 seconds, and load resolves local and cloud data by `LastSaveTime`.
- When timestamps are equal or invalid, Firebase wins and refreshes the local copy. Keep `[FirestoreProperty]` on every cloud-persisted field, including `LastSaveTime`.
- `HybridRepository` refuses to resolve when either store failed to read, because an unread store may hold progress the first save would overwrite. A corrupt local copy still recovers from a readable cloud document; a local save newer than the app's schema blocks even when the cloud is readable. The mirror write that refreshes the local copy is best-effort and must never abort the load.

Do not change save keys, Firestore document ownership, Firebase UID handling, or serialized save fields without an explicit migration plan. Preserve existing player documents when testing schema changes.

## Architecture

### Folder Structure

Assets use numbered prefixes for project-owned content:

- `Assets/01.Scenes/` - login and gameplay scenes
- `Assets/02.Scripts/Core/` - application and Firebase initialization
- `Assets/02.Scripts/Ingame/` - click, spawn, merge, slime, feedback, and gameplay managers
- `Assets/02.Scripts/Ingame/Gacha/` - ticket drop judgement, the field that owns dropped tickets, the pull rules and the pull service
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
- `LoginScene` never signs in on its own. A full-screen button covers the scene and shows the waiting label, so a tap anywhere starts a manual Google Play sign-in. Every other graphic on that screen must have Raycast Target off: a decorative image drawn after the button swallows the tap, because the event bubbles to its own parent and never reaches the button behind it.
- `SceneManagerEx` keeps scene transitions between LoginScene and GameScene.

**Game data**

- `CurrencyManager`, `SlimeManager`, and `UpgradeManager` own their domains and repository selection.
- `GameManager` waits for all three managers, then raises `OnAllDataInitialized` for gameplay systems.
- Repository interfaces separate local PlayerPrefs storage from Firebase Firestore storage.
- SlimeInstance is a domain object; SlimeInstanceSaveData owns persistence mapping. Save schemas are Currency 2, SlimeStatus 6, and Upgrade 1. Preserve deterministic legacy migration IDs. All six repositories reject a stored version higher than the app supports.
- `IRepository<T>.Load()` returns `SaveLoadResult<T>`: `Loaded`, `NotFound`, or `Failed` with a reason. Never collapse a read failure into a default value - a session that starts from defaults overwrites the progress it could not read. Repositories decide whether the document was read faithfully; managers decide whether it can become a valid domain state, and report anything unusable the same way.
- `SaveDataLoadGuard.Report()` locks saving and returns to LoginScene with per-reason guidance; `LoginScene` clears the lock. Managers must not raise `OnDataInitialized` after reporting, and must never leave initialization hanging instead.
- Values that the writer cannot produce are treated as tampering and block the session: unrestorable or duplicate slime entries, an out-of-range `HighestGrade`, negative/NaN/infinite currency, a currency array longer than the app knows about or missing entirely, and upgrade entries outside their enum range. Values that a balance change can legitimately produce are absorbed instead: an upgrade level above the spec's `MaxLevel` is clamped, and a saved entry whose upgrade is no longer in the spec table is ignored.
- A currency array *shorter* than the enum is the opposite case: it was written before a currency type existed, so the missing slots are zero-filled instead of blocked. That is what let `ECurrencyType.GachaTicket` be added without touching existing documents. A longer array is still tampering, because a stored version above the app's is already refused by the repository, so the version matching while the length does not cannot happen honestly.
- The three save documents are created and deleted together, so `GameManager` blocks entry when only some of them exist. Values inside the valid range - a raised `HighestGrade`, an inflated currency total - are indistinguishable from legitimate progress and are out of scope for client-side checks.
- Firestore leaves absent fields at their C# defaults, so a property initializer hides a missing field. `CurrencySaveData.Currencies` deliberately has none. `UpgradeSaveData.Entries` has one, so its guard checks for an empty list rather than null. That check must exclude the `NotFound` default, whose list is legitimately empty; without the exclusion every new account is blocked.
- Each manager delays its initialization by one `await UniTask.Yield()` so `OnDataInitialized` fires after every subscriber has wired up in `Start`. Do not remove it.
- The save path follows the same discipline as the load path. A repository must not swallow a write exception: `HybridRepository` is the only layer that knows both stores, so it decides. All three Firebase repositories used to catch and log inside their own `Save`, which left the layer above unable to tell that the cloud had stopped accepting writes at all. The local repositories followed the same rule as of 2026-09-08, and `HybridRepository` catches a local failure itself rather than letting it abort the method - the cloud write is scheduled after that line, so an escaping exception would leave the save in neither store.
- Losing the network does not produce a write exception. The Firestore SDK queues the mutation, retries, and flushes it on reconnect; the queued value arrives intact because every save writes the whole document. Verified on device on 2026-09-07 with airplane mode, and again by watching the value land after reconnecting. So anything that does reach the catch is a permanent rejection - a rules denial, an expired session, an exhausted quota - and needs no failure counter, threshold, or time window.
- `CloudSaveGuard` collects those failures and, unlike `SaveDataLoadGuard`, does not lock the session. A read failure risks overwriting progress the session never saw; a write failure destroys nothing, because the local save still succeeds. Only the cloud copy goes stale, and that surfaces on reinstall or a device change - so tell the player and let them keep playing.
- The notice must not tell the player to check their network. That is the one cause that never produces it, and sending them to restart a working connection wastes the one thing they can act on: not deleting the app while progress lives on this device alone.
- `CloudSaveGuard.ReportSuccess()` is what makes the repeating notice stop. Deploying corrected rules or recovering a session resumes saving immediately, and without the success path the alert would keep firing over a problem that is already fixed.
- The cloud write is throttled, not debounced, and the difference is not interchangeable. A debounce restarts its timer on every save, so it only writes once requests go quiet for the whole interval - and currency changes on every click and every auto-production tick, so with enough slimes that quiet never arrives. Raising a debounce interval does not reduce writes, it stops them. The throttle keeps one scheduled write and swaps the payload under it, so each interval sends the latest state exactly once. Free-plan document writes are capped per project, not per account, so a handful of testers can exhaust a day's quota. `FlushPendingSave()` sends the scheduled write immediately when the app pauses or quits, so the interval never swallows the last few seconds; it does not await the network, because the Firestore SDK records the mutation locally and delivers it on the next launch.

**Offline reward and tutorial**

- `GameManager` also computes the offline reward from `CurrencyManager.LastSaveTime` and per-slime auto-production, gated by a minimum interval, a maximum accrual window, and an efficiency factor. Gameplay stays inactive until the reward popup is dismissed.
- Computing the reward and presenting it are separate. While `TutorialManager.IsRunning`, presentation waits for `TutorialManager.Finished`; the spotlight overlay and the popup's blocker each cover the screen, so showing both locks input in every direction. Compute at resume and keep the larger pending reward - play continues while presentation waits, and every currency change refreshes `LastSaveTime`, so recomputing later would shrink it. The count-up start value is re-read at presentation. Entering GameScene never defers, because no sequence has begun yet.
- `TutorialProgress` stores completion per user ID in PlayerPrefs and treats existing progress as completion of the main tutorial. DisplayRoom remains incomplete until its final dialogue; these local flags are not cloud-synced.
- `TutorialManager` owns execution and shared presentation; MainTutorialSequence, HigherGradeSpawnTutorialSequence, DisplayRoomTutorialSequence, and GachaTutorialSequence own their steps. `GameplaySaveGate` blocks progress saves during the main tutorial, not DisplayRoom transfers.
- `TutorialIds.Gacha` registers with `completeByDefault: false`, unlike `HigherGradeSpawn` which completes itself for anyone already past its unlock. Gacha shipped after players were already past Lv.7, and completing by unlock would skip the guidance and the free ticket for every one of them. That is also why it starts on gameplay activation and spawn initialisation, not only on the unlock popup.
- The tutorial marks itself complete at the pull, not at the closing dialogue. The pull is the irreversible point; a kill during the dialogue would otherwise hand out a second free ticket on the next launch. It also must not abort when the field is full with no mergeable pair - aborting leaves the flag unset, which keeps drops switched off. It shows the button position and finishes instead.
- DisplayRoom guidance requires both gameplay activation and `SpawnManager.IsInitialized`. `SpawnManager.Initialized` fires after restoration and first spawn. If a stored slime already exists, resume entry/info guidance without requesting another transfer.
- Higher-grade spawn guidance calls `BottomPanelSwitcher.TryShowSystemUpgradePanel()` before focusing the carousel. Selecting an upgrade does not make a hidden panel visible.
- Offline reward is not offline play. Android cold start still requires Google Play/Firebase login before GameScene loads; do not describe offline play as supported.
- Offline elapsed time comes from `ServerClock.TrustedUtcNow`, not the device clock, with a 60-second minimum, an 8-hour cap, and 50% efficiency. `LoginScene` syncs before entering GameScene and refuses entry when it fails; `GameManager` re-syncs on resume but keeps the previous offset when that fails, so an honest player returning offline still gets paid.
- `ServerClock` writes its marker to a dedicated `TimeSync/{userId}` document, never to a progress document. Writing to `Currency` creates that document for a brand-new account, which then fails the "three save documents are created together" rule and blocks entry.
- That marker needs a Firestore rule allowing the owner to write only `ServerSyncTime`, and only when it equals `request.time`. Without the rule, login succeeds and GameScene entry is blocked; without the `request.time` check, a client can write its own timestamp and the whole defence is bypassed.
- Values inside their valid range still cannot be checked on the client. Revisit server-authoritative settlement before adding rankings, competition, or paid-currency dependencies.

**Gacha**

- Everything gacha unlocks together at `UnlockGrades.Gacha` (Grade 7): ticket drops, the pull button, and the auto-spawn toggle. Ask `SlimeManager.IsGachaUnlocked` rather than comparing grades at a call site.
- `GachaTicketDropper` only judges. It runs its own 60-second tick because spawn interval shrinks with upgrades, so hanging the roll on spawning would make a spawn upgrade a drop-rate upgrade. One timer covers the whole field: a per-slime timer scatters its phase on pool reuse and turns "every slime rolls each minute" into something else, and a merge would have to decide whose timer survives.
- `GachaTicketField` turns a successful roll into state. The two are split because a ticket outlives the roll by days. It also owns the objects on the field and the collect animation.
- A ticket's stage is decided from the dropping slime's grade at drop time and then frozen. Merging that slime into the sky later does not move a ticket that already fell.
- Pending tickets live in the `SlimeStatus` document as two counts, not as a list with coordinates. Slimes do not save positions either - they are re-scattered on restore - so a ticket has no reason to. Showing several as separate objects is a screen rule, satisfied by creating that many on restore.
- `_maxObjectsPerStage` caps the objects, never the saved count. Idling for days accumulates hundreds; the overflow stays in the save and appears as the field empties. The cap is a judgement, not a spec value.
- Tickets are collected through a UI button, never a world collider. `Clicker.TrySelect` raycasts with no layer mask and takes the first hit, so a collider on a ticket swallows the slime underneath it. UI is judged on a separate path, so an overlapping tap collects the ticket and clicks the slime.
- Collection decrements the save and grants the currency together, at the end of the flight. Doing either at the tap opens a window where the app can die between them and lose a ticket. The object stays in its list while flying, because removing it early makes the object count disagree with the still-unchanged save and `Restore` invents a replacement.
- `GachaService` owns the order for a pull: unlock, ticket, empty MainStage slot, and a drawable candidate are all checked before the ticket is spent. If the spawn still fails, the ticket is refunded. Same discipline as `MergeManager` moving the save before the irreversible grade update.
- `NormalGachaPool` draws from highest-unlocked minus 1 through minus 4 at weights 10/20/30/50. The highest grade itself is never a candidate, so gacha cannot pre-empt meeting a new grade through merging. Candidates below Grade 1 drop out and the rest renormalise.
- `GachaResultDirector` presents the result; it never creates it. The slime is spawned and saved first, then hidden for the animation and revealed at the end, so a kill mid-animation costs nothing. When the result belongs to the other stage it is never revealed and flies off that way instead - without this, a Lv.15 player pulling on Ground sees nothing at all, because every candidate is a sky grade.

**Spawn and merge loop**

- `SpawnManager` applies saved upgrades, restores individual slimes in both locations, and creates a Grade 1 slime if no MainStage slime remains, even when DisplayRoom contains slimes. Timed spawning uses the unlocked weighted pool.
- The player's auto-spawn setting and the tutorials' spawn pause are separate axes. Tutorials own `SetSpawningPaused`, and three of them call `SetSpawningPaused(false)` when they end; sharing one flag would let a tutorial silently switch a player's setting back on. `SpawnManager` reads `SlimeManager.IsAutoSpawnEnabled` directly rather than caching it, so the two cannot drift. The check sits after the editor spawn shortcuts and before the timer, so manual spawning still works and the gauge resumes where it stopped.
- That setting is stored inverted, as `AutoSpawnDisabled`. Both Firestore and JSON leave an absent field at the C# default, so an `AutoSpawnEnabled` field would read as "off" for every pre-v6 document and stop their spawning. Filling it in the migration function is not a fix either: that function does not know which version it came from, so a later v7 would switch a player's deliberate "off" back on.
- `SlimeSpawner` uses Lean Pool and tracks active slime instances. `GetActiveTargets()` hands that list out as `IReadOnlyList`, because only `Spawn` and `Despawn` may change it. That does not make iteration safe: spawning or despawning inside a loop over it still throws, so never do either from within one.
- `Clicker` uses Unity's Input System pointer API for both mouse and touch input.
- `SlimeController` handles manual clicks, dragging, point rewards, and overlap-based merge requests.
- `MergeManager` validates same-grade merges and promotes the surviving slime. It moves the save state first and only then raises the highest grade and updates the visuals, because `SlimeManager.MergeSlime()` throws on an entry the save does not have. Ordering the irreversible grade update after that check is what keeps a failure from leaving the save and the screen disagreeing, so no rollback is needed.
- Auto production is split: each `SlimeController` owns its own timer so the timer's lifetime matches the pooled object, and `AutoClicker` owns the single tick loop, the eligibility rule, and `SetPaused`. Keep the rule there - tutorials pause every slime at once through it, and `GameManager`'s offline reward reproduces the same rule. `OnSpawn` re-scatters the timer's phase, never its period, because the offline reward divides by `AutoClickInterval` as the average. DisplayRoom is excluded from manual, automatic, and offline production.

**Feedback**

Feedback components implement `IFeedback` and are discovered from a slime's child objects. Existing effects include color, scale, sound, and point floaters. Keep new feedback behavior component-based where possible.

`AudioManager` owns scene-authored BGM and SFX sources through an Audio Mixer, stores per-device BGM/SFX volume preferences, and mutes on application pause. `GameExitManager` dispatches registered back handlers before falling back to upgrade closing and the exit popup.

**Gameplay UI**

- The game is portrait-only. Auto Rotation is on with every orientation but Portrait disallowed, in the project settings and in both Android build profiles. Re-enabling any of them, `PortraitUpsideDown` included, breaks the layout: a 180-degree flip moves the notch without changing screen size, so `SafeAreaFitter` never re-applies and the insets stay on the wrong edge.
- `UpgradeUI` derives its closed position from the actual panel width and applies `Screen.safeArea` insets. Layout refresh is event-driven through rect-size, focus, and pause callbacks; do not restore a fixed movement distance or per-frame layout polling.
- `BottomPanelSwitcher.RefreshLayout` places the panel switch button, the auto-spawn toggle and the gacha button at runtime, so their authored positions are overwritten. It computes where the bottom-left corner belongs and converts to `anchoredPosition` by adding `size * pivot`, because `anchoredPosition` addresses the pivot. Without that conversion, changing a pivot in the Inspector slides the button by half its size and correcting the authored value only hides it until the resolution or safe area changes.
- `GameExitManager` depends on the public `UpgradeUI.TryClose()` API. Preserve that API and its close-first behavior when changing the upgrade panel.
- `BottomPanelSwitcher` owns bottom-panel selection and presentation; `StageUI` owns the Ground/Sky button, and `SpaceToggleButtonUI` owns the DisplayRoom/MainStage button label and click event. `DisplayRoomUI` orchestrates transfers and space changes.
- `HudVisibility` owns the position of TopHudRoot and BottomHudRoot. Send mode requests `Top`, observation requests `All`, and requests are stacked per owner so overlapping presentations stay safe. Never cache or restore those roots' positions elsewhere; a second owner that remembers a displaced position leaves the HUD off screen.
- The upgrade drawer is not a HUD part. `UpgradeUI` computes its own hidden position from panel width and safe-area insets, so hide it through `SetToggleVisible()` instead of moving its transform.
- Keep static UI, audio sources, and references authored in scenes/prefabs rather than constructing their hierarchy at runtime.
- `Clicker.PushMode(owner, mode, priority)` / `ReleaseMode(owner)` arbitrate world input: Space < Selection < Tutorial < Modal. Same-owner updates keep their position; release only the owner's request on completion or teardown.
- `StageManager.PlayDisplayRoomTransfer()` starts a space transfer and `StageManager.TryRelocateSlime()` finishes it: save location, reposition, refresh presentation, and restore the pre-transfer position on failure. UI owns only the policy around it - toast text, input restore, popup closing. Do not reimplement the completion half in a caller.
- Never place a `Button` or other `Selectable` under a `Slider`, `Scrollbar`, `ScrollRect`, or any `IDragHandler`. `Slider.OnInitializePotentialDrag` clears the drag threshold, so any finger movement starts a parent drag and cancels the child's click. The spawn gauge keeps its `Slider` on a dedicated `SpawnBar` child for this reason.
- Text outlines and similar variants are material properties, so make them as material presets on the one font asset rather than baking the font again. `MulmaruMono` was baked three times for two outline looks and each copy carried its own 4096 atlas, 35 MB apiece; the presets that replaced them are 3 KB and reference the shared atlas. A preset appears in a `TMP_Text`'s Material Preset dropdown only while it sits beside the font asset and points at that atlas, and both the Font Asset and the Material Preset fields have to be set - changing the font alone silently reverts to the plain material.

**Scene transitions and loading**

- `FadeCurtainUI` is one prefab (`LoadingOverlayCanvas`) shared by both scenes. Two components would need their background colour and fade time matched by hand, and one mismatch makes the screen jump at the scene boundary. Put the component on an always-active parent with `_root` as its child; on `_root` itself it cannot cover again after being revealed.
- The curtain's canvas order belongs to the code, not the prefab: `Awake` sets `overrideSorting` and the `sortingOrder` ceiling. An authored value loses that race - `DialoguePresentation` computes `reference.sortingOrder + 100` at runtime and overtook the curtain's old 20, drawing the tutorial dialogue over it and letting taps through a screen that looked covered. Nothing belongs above the curtain, so it coordinates numbers with nothing; passing it takes a deliberate move to the ceiling.
- `CoverAsync()` and `RevealAsync()` return only once the screen has reached that state, and a transition in flight yields to a new request by fade generation. Never early-return from an awaited transition without reaching the state: the caller `await`s it and proceeds believing it succeeded, which loaded GameScene over an uncovered screen whenever login beat the fade.
- `_minimumCoveredSeconds` holds the curtain for a floor duration, because Editor logins and local loads finish in a few frames and it would otherwise flicker past. `LoginScene` carried the mirror of it while sign-in was automatic; once the player has to tap to continue, the screen is necessarily seen and that timer only delayed the response to the tap, so it was removed. Measure such a floor from scene start and it expires before a human reacts - it can only make an answered tap feel slow.
- `LoginScene` drives the curtain directly. GameScene's `LoadingOverlayRevealer` waits for `SpawnManager.Initialized` rather than `OnAllDataInitialized`, because the latter is the signal telling `SpawnManager` to start restoring and subscriber order can leave an empty field visible for a frame. It reveals immediately when `SpawnManager` is missing, but has no timeout: a load that stalls instead of failing leaves the curtain up with its animation still running and the back button covered. Read failures return to LoginScene, so only a stall reaches this.
- Canvas order has no single authority. GameScene authors 0 / 5 / 10 / 1000, the gacha result overlay is a nested canvas with `overrideSorting` at 500, `DialoguePresentation` computes its own at runtime (reference + 100), and the curtain claims the ceiling. The gacha overlay has to clear the tutorial spotlight, which is why it sits above that computed 100. A nested canvas needs its own `GraphicRaycaster` to be hit at all - the parent's does not reach into it - and the field's tickets live on a separate world-space canvas. Check an authored value against the runtime ones before changing it; a canvas lowered to sit under something else has twice been overtaken by a number computed elsewhere.

**Options and progress reset**

- `OptionsUI` provides volume sliders and explicit confirmations for progress reset and game-account deletion.
- `SaveRecoveryUI` is the same reset reached from LoginScene, for a session that `SaveDataLoadGuard` blocked before the options screen was reachable. `LoginScene` opens it only for `Unreadable`: a network failure needs a retry and a future schema version needs an app update, so offering reset there would delete progress that is still intact. The panel owns its presentation; `LoginScene` owns which failure qualifies and the login-then-reset order.
- `GameDataResetService` deletes only the current UID's Currency, SlimeStatus, and Upgrade documents on Android, plus that user's local progress and tutorial flags. Editor deletes local progress only; authentication accounts and audio preferences remain.
- Reset locks gameplay/saves, invalidates old debounced writes by ResetGeneration, waits for pending Firestore writes, then deletes. A local pending marker resumes interrupted resets in LoginScene before GameScene entry.
- A reset timeout does not cancel the server operation. Do not resume the old game while the result is uncertain. Return to login after reset; the login screen never signs in by itself, so nothing re-enters the game unattended.
- Other devices' local saves are not invalidated and can restore old cloud progress later. Account-wide reset generations are not implemented.

### Key Patterns

- Scene-level singleton managers. Declare as `public static T Instance { get; private set; }` and guard `Awake` with `if (Instance != null && Instance != this) { Destroy(gameObject); return; }` before assigning. The `Instance != this` check keeps a re-entered `Awake` from destroying the already registered instance.

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

The offline reward, tutorial, exit popup, audio, Phase 2/2-B DisplayRoom, Phase 3's normal-slime collection book, game-account deletion, the save-layer hardening described above, and the scene-transition curtain have since shipped on `main`. Phase 4's gacha ticket is complete on `feature/phase-4-gacha-ticket` but not merged: the drop, the pending-ticket save, collection, the pull, the auto-spawn toggle, the tutorial, and the full-screen result presentation. Special slimes are Phase 5 and are not implemented. Auto ticket collection (12 collected) and offline tickets (15 collected) are specified but not built.

The save-layer rules were verified as follows. Editor Play Mode covered every path reachable without Firestore - corrupt data, future schema versions, partial save sets, the recovery flow from corrupt save through reset and tutorial to the first save that recreates all three documents, and new-account entry after each change. A development APK on device covered the three cloud-only paths: a missing `Currencies` field, a missing `Entries` field, and deferring the reward across a background/resume during the DisplayRoom tutorial.

Tampered values that fall inside their valid range remain undetectable on the client; revisit that together with the offline reward's device-clock dependency.

`Builds/Release/` carries `build-info.txt` handoffs through `0.1.06` and again from `0.1.09`; `0.1.07` and `0.1.08` have artifacts without one. `0.1.09` (Android Version Code 10, built 2026-09-07) is the first release build to contain the save-layer work, the server-time offline reward, the scene-transition curtain, the tap-to-continue login, the bookmark collection book, and the cloud-save notice. It went to the Play Console closed testing track and passed every item in its build-info on 2026-09-08. The one that mattered most was reinstall-and-restore: progress came back intact, so IL2CPP stripping does not touch Firestore's reflection-based serialization and no `link.xml` is needed. Walk that path again if the stripping level changes.

Phase 3's collection book has been checked on device in both of its forms, most recently the bookmark rework of 2026-09-07 - including the check its entries need and the others do not, that dragging the horizontal `ScrollRect` strip to scroll does not select an entry. Portrait layout was checked across resolutions in the Game view and in the Device Simulator. The responsive UpgradeUI/Safe Area work, drag-merge target feedback, and the DisplayRoom send/observation/transfer paths were checked on the `0.1.09` release build on 2026-09-08, so they now carry device evidence rather than static checks alone. The collection book's DisplayRoom badge landed after that build and has none yet.

`0.1.09` shipped to closed testing ahead of Phase 4 rather than after it, to close the gap between the work on `main` and the last release build. Everything after it lives on `feature/phase-4-gacha-ticket`, so no release build has run the gacha work, the write throttle, the pause flush, the loading timeout, or the DisplayRoom badge. Two save schemas moved on that branch - Currency to 2 and SlimeStatus to 6 - so loading an existing document is the check that matters most on the next release build. Sign-in on that track failed for accounts that had never played the game before; the cause was Play Games Services tester registration, not the build - see `Documentation/GOOGLE_PLAY_FIREBASE_INTEGRATION.md` 6.4-1 before suspecting signing, device state, or the client.

## Working Guidelines

- Make the smallest edit that resolves the stated request, and preserve existing public APIs and serialized references. One concern per change: do not bundle adjacent refactors, cleanups, or "while I am here" improvements into the same pass, even when they touch the same file. When a fix needs several layers, split it into steps, deliver the first, and wait for the user before starting the next.
- The user performs all verification. Do not run builds, `dotnet build`, Play Mode, or device tests. When a change is ready, hand over the exact steps to check it — what to open, what to do, what a pass and a failure look like.
- Check the current branch, working tree, app version, and release profile before starting a version-scoped change. Work directly on `main` only when the user explicitly chooses that flow.
- Do not modify package, generated resolver, Firebase configuration, or Google Play Games files as incidental cleanup.
- `firestore.rules` in the repository is the source of truth; deploy it with `firebase deploy --only firestore:rules` from the project root. Never edit the rules in the Firebase console - the repository copy would go stale and the next deploy would silently revert the change. Code that reaches a new collection needs its rule in the same change, or login succeeds and the feature fails with a permission error that is hard to trace from the client.
- Keep platform behavior explicit; Editor-local behavior must not silently replace Android cloud behavior.
- Treat Unity Play Mode and Android device results separately from static or `.csproj` checks.
- Preserve unrelated working-tree changes and inspect the exact Git diff before staging.
- Some source files mix CRLF and LF within the same file. A multi-line string replacement that assumes one line terminator silently matches nothing, so edit by lines and keep each line's own ending.
- Record release handoffs under `Builds/Release/<version>/build-info.txt`; the `Builds/` directory is intentionally ignored by Git.
- Use `Feat :`, `Fix :`, `Chore :`, or `Docs :` commit subjects with concise Korean bullets when a body is useful.
- Follow `Documentation/CODING_CONVENTION.md`. Its Law of Demeter section lists explicit exceptions for data structures and Unity framework APIs.
- Do not report a performance problem without measuring it on an Android device. See `Documentation/PLAYERPREFS_SAVE_PROFILING.md` for the method and for a hypothesis that measurement rejected.
