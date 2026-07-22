# Unity OSC Control Framework

OSC Control Framework for Unity.

OCF exposes your scripts' fields, properties and methods to [OSC](https://en.wikipedia.org/wiki/Open_Sound_Control) control, by reflecting over them at runtime. It is the control layer used by [GenUI](https://github.com/Theoriz/GenUI), which adds a generated user interface on top — but OCF works on its own if you only need OSC.

## Requirements

| Requirement | Notes |
|---|---|
| **Unity 2022.3** or later | |
| **com.theoriz.unityosc** 1.3.0 or later | OCF's OSC transport. Earlier versions still work but declare Unity 2019.4. |

The packages declare no UPM `dependencies`, so nothing installs UnityOSC for you and nothing warns you when it is too old — install it first.

## Installation

Add the following line to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.theoriz.unityosc": "https://github.com/Theoriz/UnityOSC.git",
    "com.theoriz.ocf": "https://github.com/Theoriz/OCF.git"
  }
}
```

Or in the Unity Editor, go to **Window > Package Manager > + > Add package from git URL** and enter:

```
https://github.com/Theoriz/UnityOSC.git
```

then

```
https://github.com/Theoriz/OCF.git
```

## Exposing members

Mark the members you want to control with `[OSCExposed]`:

```C#
public class MyScript : MonoBehaviour
{
    [OSCExposed] public float speed = 1f;
    [OSCExposed, Range(0f, 1f)] public float amount = 0.5f;
    [OSCExposed(readOnly = true)] public string status = "idle";
    [OSCExposed] public LightMode mode = LightMode.Spot;

    public List<string> palettes = new List<string> { "warm", "cool" };
    [OSCExposed(targetList = "palettes")] public string palette = "warm";

    [OSCExposed]
    public void Randomize() { /* ... */ }
}
```

`[OSCExposed]` takes two options: `readOnly`, and `targetList` for a member chosen from a list — see *Exposing a list*. An enum needs neither, since its type already names its members (*Exposing an enum*).

Then generate the Controllable, either way round:

- **From the component**, three-dots menu ▸ **Add Controllable**. It offers to generate the script, and once compilation finishes it adds the component and wires it up for you — nothing else to do.
- **From the Project window**, right-click the script ▸ **Assets ▸ Controllable ▸ Generate Controllable Script**. This only writes the script; add the generated component to the same GameObject and point its `controllableTargetScript` at your script yourself.

### How it works: the two-object mirror

The generator emits a *mirror* class next to your script:

```C#
public class MyScriptControllable : Controllable
{
    [OSCProperty]
    public float speed;

    [Range(0f, 1f)]
    [OSCProperty]
    public float amount;

    [OSCProperty(readOnly = true)]
    public string status;

    [OSCMethod]
    public void Randomize()
    {
        (controllableTargetScript as MyScript).Randomize();
    }

    protected override void PollTargetScript()
    {
        var target = controllableTargetScript as MyScript;
        if (target == null) return;

        if (speed != target.speed) { speed = target.speed; RaiseScriptValueChanged("speed"); }
        // ...one line per exposed member; vectors and colors compare component by component
    }
}
```

`[Header]`, `[Range]` and `[Tooltip]` are carried over from your script and honoured by the UI.

The mirror re-declares each exposed member with `[OSCProperty]` (fields) or `[OSCMethod]` (methods), and `Controllable` binds the two **by name** at `Awake`.

> [!IMPORTANT]
> The names must match exactly. A mismatch fails silently — the member simply is not controllable.

Values flow both ways: incoming OSC and UI edits are written through to your script, and `Controllable.Update` polls your script for changes made in code.

That poll runs every frame, in `PollTargetScript`. The generated override above compares the mirror against your script directly — no reflection and no allocation. A mirror without that override falls back to `Controllable`'s implementation, which reads every exposed member through reflection and boxes every `float`, `int`, `bool`, `Vector` and `Color` once per frame whether or not it changed.

> [!TIP]
> To give a mirror the typed override, right-click the component and choose **Update Controllable**.

You can also write a mirror by hand instead of generating it, which is what the extra `[OSCProperty]` options below need. A hand-written mirror runs the reflection poll unless you override `PollTargetScript` yourself, following the shape above.

### `[OSCProperty]` options

| Option | Type | Default | Effect |
|---|---|---|---|
| `readOnly` | bool | `false` | Value is displayed but cannot be edited, and is left out of presets — nothing can write it back. |
| `showInUI` | bool | `true` | Set `false` to control the member over OSC only, with no widget. |
| `includeInPresets` | bool | `true` | Set `false` to leave the member out of saved presets. |
| `targetList` | string | — | Name of a `List<string>` whose entries this member is chosen from; renders a dropdown. See *Exposing a list*. |

`readOnly` and `targetList` are also reachable from the automatic workflow — write `[OSCExposed(readOnly = true)]` or `[OSCExposed(targetList = "myList")]` and the generator forwards them. `includeInPresets` and `showInUI` have no `[OSCExposed]` equivalent, so a member that needs one must be declared in a hand-written mirror.

### Reserved names

A generated Controllable **inherits from `Controllable`**, so an `[OSCExposed]` member that reuses one of `Controllable`'s member names will *shadow* the real one and break it. Since 2.0.0 every member `Controllable` declares carries a `controllable` prefix, which keeps the framework's own names out of your way. Fields and events spell it lower case and methods spell it `Controllable`, following the capital-letter rule every method name obeys:

- **Controllable state:** `controllableId`, `controllableDebug`, `controllableFolder`, `controllableTargetDirectory`, `controllableSourceScene`, `controllableUsePanel`, `controllableUsePresets`, `controllableClosePanelAtStart`, `controllableCurrentPreset`, `controllablePresetList`, `controllableBarColor`, `controllableTargetScript`
- **Preset methods:** `ControllableSave`, `ControllableSaveAs`, `ControllableLoad`, `ControllableShow`, `ControllableLoadWithName`
- **Events:** `controllableUiValueChanged`, `controllableValueChanged`, `controllableScriptValueChanged`
- **From Unity:** `name`, `tag`, `transform`, `gameObject`, `enabled`

The prefix shrinks the problem but does not close it. `Controllable` is a `MonoBehaviour`, so Unity's own members stay reserved and cannot be renamed — `name` is the one that bites in practice, and every public member of `MonoBehaviour` is reserved too (`Invoke`, `StartCoroutine`, `GetComponent`, `ToString`, …). `Controllable.IsReservedMemberName(string)` is the source of truth.

The framework enforces this for you: the generator refuses to emit a colliding member and logs an error naming it, and a hand-written mirror that collides logs a warning at `Awake` and is ignored in favour of the built-in. Just rename your member.

## OSC control

Every exposed member gets an address:

```
/OCF/{id}/{property}    set a value
/OCF/{id}/{method}      invoke a method
```

`{id}` defaults to the target script's type name, and can be overridden with the `controllableId` field on the Controllable. Messages that do not match a registered controllable are ignored.

Methods with parameters are reachable over OSC (their arguments map to the message arguments) but get no UI widget.

To consume your own OSC messages — anything not addressed to `/OCF/` — subscribe to the receiver directly:

```C#
using UnityOSC;

OSCMaster.Receivers["myReceiver"].messageReceived += (OSCMessage m) => Debug.Log(m.Address);
```

## Presets

`Controllable` can save and restore the state of its `[OSCProperty]` members to a file. Members marked `readOnly` or `includeInPresets = false` are left out. The generated panel exposes **Save**, **Save As**, **Load** and **Show** buttons plus a preset dropdown, and the same methods are reachable over OSC.

The ControllableMaster panel carries the global buttons instead: **Save All**, **Save As All**, **Load All** and **Open Presets Folder**. The last one reveals the presets root in your file browser, works whether or not any preset has been saved, and is also on the `ControllableMaster` component in the Inspector so you can reach the folder without entering Play mode.

```
/OCF/ControllableMaster/ControllableOpenPresetsFolder
```

> [!NOTE]
> **Show** reveals a single preset file and does nothing while no preset is selected. **Open Presets Folder** always opens the folder.

**Selecting a preset loads it.** Setting `controllableCurrentPreset` — from the dropdown or over OSC (`/OCF/{id}/controllableCurrentPreset "myPreset.pst"`) — loads that preset immediately. **Load** reloads the current preset, and **Load All** does it for every controllable. Any `[OSCMethod]` can set `showInUI = false` to stay OSC-callable without a UI button.

To load a specific file, use the `ControllableLoadWithName` method:

```
/OCF/{id}/ControllableLoadWithName "myPreset.pst"
```

| Argument | Type | Meaning |
|---|---|---|
| `fileName` | string | Case-sensitive file name. |

To keep a member out of saved presets, set `includeInPresets = false` on its `[OSCProperty]`.

The last-used preset is remembered across runs: on enable, `Controllable` reloads whichever preset was active when it was last disabled. This selection is stored in a plain-text file, `_lastUsedPreset.txt`, sitting alongside the `.pst` presets in the preset folder — it holds just the preset name and is not itself a preset.

### Where presets are stored

Each `Controllable` gets its own folder under a shared root:

```
<root>/<folder or scene name>/<controllable id>/myPreset.pst
```

`folder` is the `Controllable`'s own field; when it is empty the scene name is used instead.

The root is picked once per run, first match winning:

| # | Source | Set where |
|---|---|---|
| 1 | `-presetsPath "<absolute path>"` | Command line, e.g. `MyApp.exe -presetsPath "D:/Shows/Venue A/Presets"` |
| 2 | `customPresetDirectory` | Inspector, on `ControllableMaster` |
| 3 | `<application folder>/Presets/` | Default |
| 3 | `<Documents>/<product name>/Presets/` | Default, when `useDocumentsDirectory` is ticked on `ControllableMaster` |

**Paths must be absolute.** A relative path is ignored with an error in the console and the default is used, because it would otherwise resolve against the working directory, which is not the executable folder when the app is launched from a shortcut.

If the chosen folder cannot be created or written to, OCF logs one error naming it and falls back to the default rather than failing to start.

> [!NOTE]
> On Android neither override applies: presets always live under `Application.persistentDataPath`, the only writable location.

## Exposing a list

To pick a value from a list of strings, keep the `List<string>` on your own script and point a string member at it by name:

```C#
public class MyScript : MonoBehaviour
{
    public List<string> options = new List<string> { "red", "green", "blue" };

    [OSCExposed(targetList = "options")]
    public string selected = "red";
}
```

Generate the Controllable as usual. The dropdown writes the selected entry into `selected`, and the list is read live, so entries added at runtime appear the next time the dropdown refreshes.

The list may also live on the mirror, which is what a hand-written one does — `targetList` is looked up on the mirror first and on your script second:

```C#
public class MyScriptControllable : Controllable
{
    public List<string> options = new List<string> { "red", "green", "blue" };

    [OSCProperty(targetList = "options")]
    public string selected;
}
```

## Exposing an enum

Declare the field with its real enum type and mark it `[OSCExposed]` — nothing else is needed, and the enum can live anywhere, including nested in another type or inside an assembly definition:

```C#
public enum LightMode { None = 0, Spot = 5, Wash = 12 }

[OSCExposed] public LightMode mode = LightMode.Spot;
```

The generated mirror declares the same enum type and the panel renders a dropdown of its members.

Over OSC the member can be set either way:

```
/OCF/MyScript/mode "Wash"   ← member name, case-insensitive
/OCF/MyScript/mode 12       ← the member's declared value
```

A value naming no member logs a warning listing the valid names and leaves the member alone. Presets store the member name.

> [!NOTE]
> A `[Flags]` enum has no widget: one dropdown cannot represent a combination of members. It is controllable over OSC — by combined value, or by the comma-separated form `"Red, Blue"` — and saved in presets.