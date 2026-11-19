# AR Science

A phone AR app by Vibe Tribe: students pick a **module** (a lesson), find a table or floor, and place 3D
objects in the room. Two kinds of lessons exist today:

- **placement**: place shapes step by step ("place a cube, then two pyramids").
- **explore**: place a model (like a cell), drag to turn it, pinch to zoom inside, tap a part to read about it.

Built with **Unity 6000.6.4f1** and AR Foundation (Android + iOS).

## Get started (5 minutes)

1. Install **Unity 6000.6.4f1** from Unity Hub (add *Android Build Support* if you will build for phones).
2. In Unity Hub: **Add > Add project from disk** and pick this folder.
3. Open `Assets/Scenes/SampleScene.unity` and press **Play**.
   No phone needed: the editor uses **XR Simulation** (a pretend room). Hold the right mouse button and use
   WASD to move (Q/E for down/up). Left-click a highlighted surface to place things. To change the room:
   **Window > XR > AR Foundation > XR Environment**.
4. Try **Modules > Inside a Cell (demo)**: click a table to place the cell, left-drag to turn it,
   scroll the mouse wheel to zoom in, and click the nucleus.

> **First person to open the project after the November 2026 cleanup:** Unity rebuilds
> `Packages/packages-lock.json`. Commit that file, so everyone gets the same package versions
> (Test Plan risk 5).

## Where things are

| Folder | What it is | Will you edit it? |
| --- | --- | --- |
| `Assets/Resources/Modules/` | One `.json` file per lesson | **Yes, most often** |
| `Assets/ARScience/Models/` | Models for explore lessons (the demo cell lives here) | Yes, when adding models |
| `Assets/ARScience/Scripts/` | The app's code | Sometimes |
| `Assets/ARScience/Scripts/Core/` | The rules, as plain code with no AR (easy to test) | Sometimes |
| `Assets/ARScience/Tests/` | Automatic tests for the rules | When you change a rule |
| `Assets/ARScience/Editor/` | The "Check Modules" tool | Rarely |
| `Assets/ARScience/module.schema.json` | The lesson file format, for VS Code and the web editor | When the format changes |
| `Assets/Scenes/` | `SampleScene` is the app | Sometimes |
| `Assets/MobileARTemplateAssets/`, `Assets/Samples/` | Unity's AR template pieces our app uses (shapes, AR setup) | Usually not |

The code, in the order things happen:

- `AppBootstrap.cs`: starts the app and builds every screen (Home, Modules, Settings, Join, the lesson screen).
- `Core/ModuleDefinition.cs`: what a lesson `.json` can contain, and the rules it must follow.
- `Core/ScenarioRunner.cs`: placement lessons, "what should the student place next?"
- `Core/ModelRules.cs`: explore lessons, zoom limits, which parts show at which zoom, matching part names.
- `ARScenarioController.cs`: runs a lesson in AR. It picks what gets placed and gives the screen its text.
- `ModelControls.cs`: added to a placed explore model. Turn, zoom, tap a part.
- `UiBuilder.cs`: small helpers for making labels, buttons and boxes.
- `AppSettings.cs`: saved on/off settings.

## Add a new lesson (no code)

1. Copy a file in `Assets/Resources/Modules/` and rename it, e.g. `plant-cell.json`.
   Copy `shape-placement.json` for a placement lesson, or `cell-demo.json` for an explore lesson.
2. Change `id` (must be unique), `title`, `description`, then the `steps` or `models` (see below).
3. In Unity: **Tools > AR Science > Check Modules**. It tells you exactly what is wrong, if anything.
4. Press **Play**. Your lesson appears in the Modules list automatically.

**Tip:** open lesson files in **VS Code**. The `"$schema"` line at the top gives you autocomplete, and a red
underline whenever something is wrong, while you type.

### Placement steps

```json
{ "item": "pyramid", "count": 2, "prompt": "Place two pyramids", "scale": 0.5 }
```

- `item`: one of `cube`, `cylinder`, `pyramid`, `torus`, `wedge`, `arch`
- `count`: 1 to 5 (optional, default 1)
- `prompt`: what the student sees (optional)
- `scale`: size, 0.25 to 3 (optional, default 1)

### Explore models

```json
{
  "item": "celldemo",
  "title": "Animal cell",
  "minZoom": 0.5,
  "maxZoom": 4,
  "parts": [
    { "id": "Membrane", "name": "Cell membrane", "description": "The cell's skin.", "hideFromZoom": 2 },
    { "id": "Nucleus", "name": "Nucleus", "description": "The control center.", "showFromZoom": 2 }
  ]
}
```

- `item`: the model's prefab (see *Make your own model* below).
- `minZoom` / `maxZoom`: how small or big pinching can make it. 1 = the size it was placed at,
  so `minZoom` is 1 or less and `maxZoom` is 1 or more.
- `parts`: the tappable pieces. `id` is the name of a **child object in the prefab**. Case, spaces,
  `_` and `-` don't matter, so `"Cell Membrane"` matches an object named `cell_membrane`.
- `showFromZoom`: the part stays hidden until you zoom in this far. Use it for **inner parts**.
- `hideFromZoom`: the part disappears once you zoom in this far. Use it for **outer shells**, so you can see inside.
- More than one model in `models` adds a **Next model** button (e.g. animal cell, then plant cell).

## Make your own model (e.g. a real cell)

1. Drag the 3D file (`.fbx`, `.obj`, `.glb` via a plugin) into `Assets/ARScience/Models/`.
   Keep each model under 50 MB (requirement NF6).
2. Drag it into a scene, and give each tappable piece a clear name: `Nucleus`, `Mitochondrion`...
   - Keep parts **next to each other, not inside each other**. Hiding a part also hides everything inside it.
   - Size is in **meters**. 0.15 = 15 cm, a good size for a desk. Put the bottom of the model at height 0,
     so it sits on the table instead of sinking into it.
   - Taps need colliders. If a part has none, the app gives it a box automatically. For exact taps,
     add your own (Add Component > Mesh Collider).
3. Drag it from the Hierarchy into `Assets/ARScience/Models/` to make a **prefab**, then delete it from the scene.
   Make a plain prefab, **not** a variant of the template shapes: those have grab controls that would fight
   with turning and zooming.
4. In `SampleScene`, select **Object Spawner** and add your prefab to its **Object Prefabs** list.
5. In your lesson use `"item": "<prefab name in lower case>"`, and list the parts. Then **Check Modules**:
   it also checks that every part `id` really exists in the prefab.

## Add a new shape to place

1. Make a prefab named `SomethingVariant` (for example `MagnetVariant`).
2. In `SampleScene`, select **Object Spawner** and add the prefab to its **Object Prefabs** list.
3. Use it in a lesson as `"item": "magnet"` (name without `Variant`, lower case). No code needed.

## Add a screen or a setting

- **Screen:** in `AppBootstrap.cs`, copy `BuildJoin()` as a starting point, register it in `BuildUi()`,
  and open it with `Show("YourName")` from a button.
- **Setting:** in `AppSettings.cs`, copy the `ShowPlanes` property. Then add a toggle in `BuildSettings()`.

## Handy functions

Positions on screen are fractions: `(0, 0)` is the bottom-left, `(1, 1)` the top-right, `(0.5, 0.5)` the middle.

| Function | What it does |
| --- | --- |
| `m_Ui.Label(parent, "Hi", 40, new Vector2(0.5f, 0.8f), new Vector2(900, 100))` | Text at a spot, size 40 |
| `m_Ui.Button(parent, "Go", new Vector2(0.5f, 0.3f), () => Show(Modules))` | A button that runs code when tapped |
| `m_Ui.Panel("Name", parent, true)` | A full-screen background (`false` = see-through) |
| `m_Ui.Box("Card", parent, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.5f), true)` | A rectangle by its corners |
| `UiBuilder.Disable(button)` | Greys a button out |
| `UiBuilder.IsOverUi(screenPosition)` | Is this point on a button? (Touches there aren't for the AR view.) |
| `ModuleCatalog.LoadAll()` | Every lesson file |
| `ModuleCatalog.Validate(module, items)` | A list of problems, empty = good |
| `ModelRules.ClampZoom(zoom, model)` | Keeps a zoom inside the model's limits |
| `ModelRules.IsVisibleAt(part, zoom)` | Should a part show at this zoom? |
| `ModelRules.FindPart(model, "Nucleus")` | The part with that id, or `null` |
| `m_Controller.Progress += ...` | Runs your code whenever the lesson changes (placed, switched model) |
| `m_Controller.PartTapped += part => ...` | Runs your code when a part is tapped (e.g. play a sound, open a quiz) |

## Tests

**Window > General > Test Runner > EditMode > Run All.** Each test is a few lines in
`Assets/ARScience/Tests/ARScienceTests.cs`: it builds a tiny lesson in code and checks one rule
(zoom limits, part lookup, lesson-file checks, step order). The test plan's "TBD" Unity logic tool is this,
the Unity Test Framework.

To add one: copy a test, rename it to say what it proves, and change the numbers. Run the tests
before you push.

## Build for a phone

**File > Build Profiles**, choose **Android** (or iOS), **Switch Platform**, plug in the phone, and press **Build And Run**.
The first build takes longest. After that, Unity reuses its work, so do not delete the `Library` folder
unless something is broken.

## Where upcoming features plug in

From the Sprint 2 requirements, the shortest path for each:

- **Quizzes (F9):** add a third scenario type (or a `quiz` list on a module). `PartTapped` and
  `Progress` are the hooks for "answer after exploring". Add the rule checks to `ModuleCatalog.Validate`
  and the schema, and write tests for scoring.
- **Lessons from the web editor (F6–F8):** `ModuleCatalog.LoadAll()` is the only place lessons are read.
  Add "download JSON from the API and save it on the phone" next to it, and everything else keeps working.
  The web editor can check its output with `module.schema.json` (any JSON-schema library, e.g. Ajv).
- **3D models from the web (F7):** today models are prefabs built into the app. Downloaded models need
  a runtime loader (e.g. glTFast). Keep the part-name rule, so the JSON stays the same.
- **Sessions (F11):** the host's changes (model, step, selected part) are exactly the events the
  controller already raises. Send those over the network rather than inventing new ones.

## Why it is faster now (Nov 2026 cleanup)

Nothing you use changed: same scene, same scripts, same module files. Things the app never used were removed:

- Packages: In-App Purchasing, Analytics, Unity Services, AI Assistant, AI Inference (Sentis), AI Navigation,
  Timeline, 2D Tilemap, Multiplayer Center, Learn tutorials, Unity Version Control (we use GitHub), and
  the profiler/code-coverage bundle. (The Test Framework stays, for the tests above.)
- Engine modules: terrain, cloth, vehicles, wind, video, and similar.
- 148 template/sample files that nothing referenced (VR controllers, teleporting, climbing, demo scenes,
  tutorial pages, unused sprites and fonts). They were found by tracing every reference from our scenes.
- Phone builds use IL2CPP **"Faster (smaller) builds"**, which Unity recommends for faster build times.

Need one back? **Window > Package Manager**, then find it in the *Unity Registry* and **Install**.
