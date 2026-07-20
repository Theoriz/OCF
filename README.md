# Unity OSC Control Framework

OSC Control Framework for Unity.

OCF exposes your scripts' fields, properties and methods to [OSC](https://en.wikipedia.org/wiki/Open_Sound_Control) control, by reflecting over them at runtime. It is the control layer used by [GenUI](https://github.com/Theoriz/GenUI), which adds a generated user interface on top — but OCF works on its own if you only need OSC.

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

    [OSCExposed]
    public void Randomize() { /* ... */ }
}
```

`readOnly` is the only option `[OSCExposed]` takes.

Then generate the Controllable: right-click the script in the Project window and choose **Assets ▸ Controllable ▸ Generate Controllable Script**, or use the **Add Controllable** entry in the component's three-dots menu. Add the generated component to the same GameObject and point its `TargetScript` at your script.

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
        (TargetScript as MyScript).Randomize();
    }
}
```

`[Header]`, `[Range]` and `[Tooltip]` are carried over from your script and honoured by the UI.

The mirror re-declares each exposed member with `[OSCProperty]` (fields) or `[OSCMethod]` (methods), and `Controllable` binds the two **by name** at `Awake`.

> [!IMPORTANT]
> The names must match exactly. A mismatch fails silently — the member simply is not controllable.

Values flow both ways: incoming OSC and UI edits are written through to your script, and `Controllable.Update` polls your script for changes made in code.

You can also write a mirror by hand instead of generating it, which is what the extra `[OSCProperty]` options below need.

### `[OSCProperty]` options

| Option | Type | Default | Effect |
|---|---|---|---|
| `readOnly` | bool | `false` | Value is displayed but cannot be edited. |
| `showInUI` | bool | `true` | Set `false` to control the member over OSC only, with no widget. |
| `includeInPresets` | bool | `true` | Set `false` to leave the member out of saved presets. |
| `targetList` | string | — | Name of a `List<string>` field on the mirror; renders a dropdown whose selection is written to this member. See *Exposing a list*. |
| `enumName` | string | — | Type name of an enum; renders a dropdown of its values. See *Exposing an enum*. |

`readOnly` is also reachable from the automatic workflow — write `[OSCExposed(readOnly = true)]` and the generator forwards it. The other four options have no `[OSCExposed]` equivalent, so a member that needs one must be declared in a hand-written mirror.

### Reserved names

A generated Controllable **inherits from `Controllable`**, so an `[OSCExposed]` member that reuses one of `Controllable`'s member names will *shadow* the real one and break it:

- `Save` — the preset Save button stops saving.
- `id` — this is the name OCF registers and addresses your controllable by.
- `name` — `Controllable` is a `MonoBehaviour`, so Unity's own members are shadowable too.

The names most likely to collide:

- **Preset methods:** `Save`, `SaveAs`, `Load`, `Show`, `LoadWithName`
- **Controllable state:** `id`, `debug`, `folder`, `targetDirectory`, `sourceScene`, `usePanel`, `usePresets`, `hasPresets`, `closePanelAtStart`, `currentPreset`, `presetList`, `BarColor`, `TargetScript`
- **From Unity:** `name`, `tag`, `transform`, `gameObject`, `enabled`

`id`, `debug`, `name` and `Save` are the ones that bite in practice. This is not the full list — every public member of `MonoBehaviour` is reserved too (`Invoke`, `StartCoroutine`, `GetComponent`, `ToString`, …). `Controllable.IsReservedMemberName(string)` is the source of truth.

The framework enforces this for you: the generator refuses to emit a colliding member and logs an error naming it, and a hand-written mirror that collides logs a warning at `Awake` and is ignored in favour of the built-in. Just rename your member.

## OSC control

Every exposed member gets an address:

```
/OCF/{id}/{property}    set a value
/OCF/{id}/{method}      invoke a method
```

`{id}` defaults to the target script's type name, and can be overridden with the `id` field on the Controllable. Messages that do not match a registered controllable are ignored.

Methods with parameters are reachable over OSC (their arguments map to the message arguments) but get no UI widget.

To consume your own OSC messages — anything not addressed to `/OCF/` — subscribe to the receiver directly:

```C#
using UnityOSC;

OSCMaster.Receivers["myReceiver"].messageReceived += (OSCMessage m) => Debug.Log(m.Address);
```

## Presets

`Controllable` can save and restore the state of all its `[OSCProperty]` members to a file. The generated panel exposes **Save**, **Save As** and **Show** buttons plus a preset dropdown, and the same methods are reachable over OSC.

**Selecting a preset loads it.** Setting `currentPreset` — from the dropdown or over OSC (`/OCF/{id}/currentPreset "myPreset.pst"`) — loads that preset immediately. `Load` (reload the current preset) and, on the ControllableMaster, `LoadAll` remain callable over OSC but have no button, being marked `[OSCMethod(showInUI = false)]`. Any `[OSCMethod]` can set `showInUI = false` to stay OSC-callable without a UI button.

To load a specific file, use the `LoadWithName` method:

```
/OCF/{id}/LoadWithName "myPreset.pst"
```

| Argument | Type | Meaning |
|---|---|---|
| `fileName` | string | Case-sensitive file name. |

To keep a member out of saved presets, set `includeInPresets = false` on its `[OSCProperty]`.

The last-used preset is remembered across runs: on enable, `Controllable` reloads whichever preset was active when it was last disabled. This selection is stored in a plain-text file, `_lastUsedPreset.txt`, sitting alongside the `.pst` presets in the preset folder — it holds just the preset name and is not itself a preset.

## Exposing a list

To pick a value from a list of strings, hand-write a mirror with a `List<string>` field and point a string member at it with `targetList`:

```C#
public class MyScriptControllable : Controllable
{
    public List<string> options = new List<string> { "red", "green", "blue" };

    [OSCProperty(targetList = "options")]
    public string selected;
}
```

The dropdown writes the selected entry into `selected`, which is mirrored to your script.

## Exposing an enum

```C#
[OSCProperty(enumName = "MyNamespace.MyEnum, Assembly-CSharp")]
public string mode;
```

> [!WARNING]
> `enumName` is resolved with `Type.GetType`, which only searches the calling assembly and the core library. A bare enum name will **not** be found and logs an error — pass an assembly-qualified name (`"Namespace.EnumType, AssemblyName"`). Scripts in a Unity project with no assembly definition are in `Assembly-CSharp`.