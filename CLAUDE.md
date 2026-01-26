# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Monster Kindergarten is a Unity 6 (6000.3.2f1) idle clicker game where players click on bouncing monster targets to deal damage and earn gold. The game features physics-based movement, drag-to-move interactions, and a multi-layered feedback system.

## Build and Development

This is a Unity project. Open with Unity Hub or Unity Editor (version 6000.3.2f1). There is no command-line build configured. Test by entering Play Mode in the Unity Editor.

## Architecture

### Folder Structure
Assets use numbered prefixes for organization:
- `01.Scenes/` - Unity scenes (SampleScene is the main gameplay scene)
- `02.Scripts/` - Game code organized by feature
- `03.Prefabs/` - Reusable game objects
- `04.Images/` through `11.Sounds/` - Asset categories

### Core Systems

**GameManager** (`02.Scripts/GameManager.cs`) - Singleton tracking game state: AutoDamage, ManualDamage, Gold.

**Click System** (`02.Scripts/Ingame/Click/`):
- `IClickable` interface - implemented by clickable targets
- `ClickInfo` struct - carries click data (type, damage, position)
- `EClickType` enum - Manual (player) vs Auto (passive)
- `Clicker` - handles player mouse input, raycasts to find IClickable targets
- `AutoClicker` - passive damage system (currently disabled)
- `ClickTarget` - the interactive monster; handles physics movement, bouncing, drag-to-move, and triggers feedback

**Feedback System** (`02.Scripts/Ingame/Feedback/`):
- `IFeedback` interface - all feedback types implement `Play(ClickInfo)`
- `ColorFlashFeedback` - flashes sprite color on click
- `ScaleTweeningFeedback` - DOTween scale animation
- `SoundFeedback` - plays audio (manual clicks only)
- `DamageFloaterFeedback` - placeholder for damage numbers

Feedbacks are attached as child GameObjects of ClickTarget and auto-discovered via `GetComponentsInChildren<IFeedback>()`.

### Key Patterns
- **Singleton**: GameManager.Instance
- **Interface-based extensibility**: Add new IClickable or IFeedback implementations without modifying existing code
- **Component composition**: Feedback behaviors are modular components on the ClickTarget prefab

### Physics Layers
- Layer 6: "ClickTarget" - used for raycast detection

## Third-Party Dependencies
- **DOTween** (Plugins/Demigiant/) - tweening/animation
- **UIEffect** (com.coffee.ui-effect) - UI visual effects
- **TextMesh Pro** - text rendering

## Current Development
Branch `jh/feature/drag-drop` - implementing drag-and-drop mechanics. Merge system for combining same-level monsters is scaffolded but commented out in ClickTarget.cs.
