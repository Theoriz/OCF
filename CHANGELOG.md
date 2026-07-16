# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/).

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
