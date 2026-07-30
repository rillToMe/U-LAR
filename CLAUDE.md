# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6.3 (`6000.3.5f2`) URP project — an interactive simulation of building a network cable (UTP → strip → RJ45 → crimp), intended for AR. Single scene: `Assets/00_Scenes/MainScene.unity`. Gameplay code lives in `Assets/01_Scripts/` (~17 files, no namespaces, all in `Assembly-CSharp` — there are no `.asmdef` files).

Code comments and log strings are written in Indonesian in places; match the surrounding language rather than normalizing to English.

## Commands

Unity is installed outside the default Hub location:

```powershell
$UNITY = "E:/Software/Produktivity/Unity/UnityVersion/6000.3.5f2/Editor/Unity.exe"
$PROJ  = "E:/Project/01_Game_Development/Game/TypologiJaringanARR"
```

Compile check without opening the GUI (the Editor must not already have the project open — it holds an exclusive lock):

```powershell
& $UNITY -batchmode -quit -projectPath $PROJ -logFile -
```

Run tests (`com.unity.test-framework` 1.6.0 is installed but **no tests exist yet**; adding any requires creating a test asmdef first):

```powershell
& $UNITY -batchmode -runTests -projectPath $PROJ -testPlatform EditMode -testResults "$PROJ/Logs/results.xml" -logFile -
# single test: append -testFilter "Namespace.Class.Method"
```

There is no build script or CI. Player builds are done through **File → Build Profiles** in the Editor. When the Editor is open, compile errors are fastest to read from `Logs/AssetImportWorker0.log`.

## Architecture

### State → Controller → View triad

Each interactive part follows the same three-object split. Understanding this pattern explains most of the codebase:

- **`*Point`** (`CablePoint`, `RJ45Point`) — owns a serialized enum state and fires `event Action<TState> OnStateChanged` from the property setter. The setter early-returns when the value is unchanged, so the event is edge-triggered only. These are pure data holders with no rules.
- **`*Controller`** (`RJ45Controller`, `CableController`) — owns the **transition rules**. `RJ45Controller.TryInsert()` is the canonical example: it validates the *cross-object* precondition (cable must be `Stripped` **and** RJ45 must be `None`) before mutating state, and returns `bool` for success. Cross-object validation belongs here, not in `*Point` or in tools.
- **`*View`** (`CablePointView`, `RJ45View`) — subscribes to `OnStateChanged` in `OnEnable`, unsubscribes in `OnDisable`, and calls `Refresh(state)` again in `Start()` to catch the initial state (the event never fires for it). Views only `SetActive` child GameObjects; they never write state.

Two independent state machines advance in parallel on the same physical cable end:

```
CableState: Intact → Stripped → RJ45Mounted → Connected
RJ45State:  None   → Inserted → Crimped
```

Note the asymmetry: `RJ45Controller.TryInsert()` sets `RJ45State.Inserted` but leaves `CableState` at `Stripped` — it does **not** advance to `RJ45Mounted`. `CablePointView` already has visual handling for `RJ45Mounted`/`Connected`, so those states are wired for rendering but nothing sets them yet. Expect to add the crimp/connect transitions.

### Tool system

`ITool` is a one-method interface: `bool Execute(RaycastHit hit)`. Tools are MonoBehaviours sitting on their own scene GameObjects (`WireStripperTool`, `RJ45Tool` under `ToolsManager`).

`ToolManager` holds the active tool as **two parallel fields** — a `ToolState` enum plus a `MonoBehaviour` reference — and exposes the behaviour via `currentToolBehaviour as ITool`. The `MonoBehaviour` field type exists so the tool is assignable in the Inspector (Unity can't serialize an interface field). Consequence: the enum and the behaviour can drift out of sync, and a non-`ITool` MonoBehaviour dropped into that slot silently yields `CurrentTool == null`. Always set both together via `SetCurrentTool(state, tool)`. When adding a tool, add its `ToolState` enum member **and** the `ITool` MonoBehaviour.

Tools resolve their own targets from the `RaycastHit`, and differ in how deep they look — `WireStripper` uses `GetComponent<CablePoint>()` on the hit collider, while `RJ45Tool` uses `GetComponentInParent<RJ45Controller>()`. Tools return `false` rather than throwing when the target or precondition doesn't match.

### Input — two competing paths (important)

There are **two** separate pointer-input components in the scene, using two different Input System styles, and both raycast on the same press:

| | `CableInput` | `ToolInput` |
|---|---|---|
| Action source | instantiates the generated `GameInput` class directly | serialized `InputActionReference` fields |
| Style | polls `WasPressedThisFrame` / `IsPressed` in `Update()` | event-driven, `action.performed +=` |
| Does | drags a `CablePoint` along a horizontal `Plane` at the endpoint's height | dispatches to `ToolManager.CurrentTool.Execute(hit)` |

A single click reaches both. When changing click behaviour, check both files — and be aware that dragging and tool-use are not mutually exclusive; there is no shared input arbitration or "click consumed" concept yet. If you add one, that is the natural place for it.

`ToolInput` also **gates on `CablePoint` before dispatching**: it requires `hit.collider.GetComponent<CablePoint>()` to be non-null, and only then calls the tool. Since `RJ45Tool` searches for `RJ45Controller` via `GetComponentInParent`, an RJ45 collider only reaches the tool if `CablePoint` happens to sit on that same collider object. This gate is the first thing to check when a tool "does nothing" on click.

`Assets/01_Scripts/Input/GameInput.cs` is **generated** from `GameInput.inputactions` — edit the `.inputactions` asset, never the `.cs`. The action map is `Gameplay` with `PointerPosition` (Vector2) and `PointerPress` (Button), each bound to both mouse and touchscreen. `activeInputHandler: 1` means the old input manager is off — use the Input System exclusively.

### Cable rendering

`CableController` drives `com.unity.splines` (2.8.4) directly: every `LateUpdate` it overwrites knot 0 and knot 1 of the `SplineContainer`'s spline from `pointA.Position` / `pointB.Position`. `LateUpdate` matters — it must run after `CableInput` has moved the endpoints in `Update`. The spline is a slave to the two endpoints; don't expect edits to the spline asset to persist at runtime.

`CableController` and `CablePointView` are **not present in `MainScene`** — they exist only inside `Assets/02_Prefabs/Cable.prefab`. The scene currently wires up the RJ45 and tool flow; the cable/spline flow has to be tested via the prefab.

## AR status

Despite the project name, **AR is not integrated**. The project was created from a Vuforia template: `Assets/Editor/Migration/AddVuforiaEnginePackage.cs` is `[InitializeOnLoad]` and will show a dialog offering to add Vuforia 11.4.4 from a scoped registry, but `Packages/manifest.json` contains no Vuforia entry and no `.tgz` is present in that folder. Don't assume Vuforia APIs are available; if that dialog is dismissed the project stays non-AR. Android is the intended target (`AndroidTargetArchitectures: 2` = ARM64).

## Conventions

- Asset folders are numbered by kind: `00_Scenes`, `01_Scripts`, `02_Prefabs`, `03_Models`, `04_Materials`, `07_Audio`.
- Fields are `[SerializeField] private` with `[Header("…")]` grouping; dependencies are wired in the Inspector rather than looked up or injected in code. Adding a serialized field means the scene/prefab must be re-wired by hand.
- Views null-check each visual GameObject before `SetActive` — optional visuals are expected.
- Editor-only test hooks are wrapped in `#if UNITY_EDITOR` with `[ContextMenu("Test/…")]` (see `RJ45Controller.TestTryInsert`), which is how state transitions are exercised manually in the absence of tests.
- Committing scene/prefab changes means committing YAML asset files — keep `.unity` and `.prefab` edits in focused commits, since they don't merge well.
- `Assets/01_Scripts/RJ45/RJ34View.cs` declares `class RJ45View` — the filename is a typo and does not match the class. Unity requires the filename to match for MonoBehaviours to be attached; the existing scene reference is serialized and works, but the component cannot be reliably added to new objects until the file is renamed. Rename the `.cs` and its `.meta` together to preserve the GUID.
