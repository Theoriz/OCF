# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Vector4 support: `TypeConverter.StringToVector4`, and `Vector4` handling in `setFieldProp`, `setMethodProp` and `getData`.

### Changed

- The last-used-preset marker file was renamed from `_temp.pst` to `_lastUsedPreset.txt`; legacy files are migrated automatically on load.

### Removed

- Preset tweening: removed the `TweenCurves` class and the `duration` / `tweenStyle` parameters from `LoadWithName` and `loadData`; presets now apply instantly. OSC callers degrade gracefully (extra `LoadWithName` args are ignored, not an error); saved `.pst` files are unaffected.

## [1.2.0] - 2026-07-17

### Added

- Controllable.PresetMethodNames and ControllableMasterControllable.AllPresetMethodNames: the preset method names, so consumers identify preset methods by name instead of by displayed label.
- Controllable.IsReservedMemberName: reports whether a name is already used by a member of Controllable, and so may not be reused by an [OSCExposed] member.
- ControllableGenerator refuses to emit an [OSCExposed] member whose name collides with a member of Controllable, and logs an error naming it. A hand-written mirror that collides logs a warning during Awake.
- README documents exposing members, the mirror pattern, the [OSCProperty] options, reserved names, OSC addressing and presets. It previously covered installation only.
- EditMode and PlayMode tests covering the name constants, the reserved-name query and member name collisions.

### Changed

- Controllable's own [OSCMethod] members (Save, SaveAs, Load, Show, LoadWithName) are always bound to Controllable's implementation and never to a target script's same-named method. **This is a behavior change:** a target script that relied on its own Save being invoked by the preset button will no longer be called. Rename the method to expose it.
- Methods on the target script are matched on signature, not on name alone.

### Fixed

- Any public method named Save, SaveAs, Load, Show or LoadWithName on a target script silently replaced Controllable's own — no [OSCExposed] and no matching signature required — so preset saving stopped working for that controllable with no warning at any layer.
- A mirror declaring an [OSCProperty] named currentPreset threw an ArgumentException during Awake.
- A mirror overloading one of Controllable's [OSCMethod] members threw an ArgumentException during Awake.
- Looking up a target script method no longer binds a mismatched signature, and no longer throws an AmbiguousMatchException when the target declares overloads.

## [1.1.0] - 2026-07-16

### Added

- EditMode and PlayMode tests covering TypeConverter and the Controllable mirror.
- ControllableMaster.RefreshIP to refresh the local IP address on demand.
- ControllableMaster.maxConnectAttempts to cap incremental port scanning.
- Warning when an [OSCProperty] has no matching member on the target script.
- RegisteredControllables is reset on domain reload.

### Changed

- The local IP address is resolved once on Start instead of every frame.
- Incremental connect is capped and logs once when it gives up, instead of rebinding the port and re-registering the Zeroconf service every frame.
- [Range] is enforced in setFieldProp, so values coming from OSC and presets are clamped like values typed in the UI.
- Color preset values are saved at full precision. Existing presets still load.
- Controllable.Update caches its reflection data and compares values with null-safe equality instead of string conversion.
- TypeConverter is now a static class.
- ControllableMaster removes its OSC receiver and unsubscribes when disabled.

### Removed

- ipRefreshDelay, replaced by the one-time lookup on Start and RefreshIP.
- No-op setter on ClassAttributInfo.Name.

### Fixed

- controllableValueChanged fired twice for every target change; it now fires once.
- Preset file names were split on "/" only, so on Windows the preset list held full paths and included the temp file.
- OSC address parsing no longer swallows an error and continues with an empty property name.
- A null TargetScript no longer causes a NullReferenceException during Awake.
- setMethodProp read the wrong OSC argument for multi-parameter methods.
- Enum resolution and malformed vector/color strings no longer throw.
- getFloat and getInt parse with invariant culture.
- Preset writes no longer leave a file handle open.
- TweenCurves.Instance no longer throws when absent; a linear tween is used instead.
- Show handles a null preset and non-Windows platforms.
- Generated Controllable scripts compile for non-void methods, namespaced and generic types, escaped Header/Tooltip text and invariant [Range] values.


## [1.0.0] - 2026-04-21

### Added

- Set up repository and files for UPM support.
