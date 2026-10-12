# Awavio design

[Back to the project](../README.md)

Awavio is a small desktop companion with a visible personality: a radiant eye, a deep ink background, vivid violet and aqua accents, and a countdown that is easy to read at a glance.

The name hints at being awake without turning the app into a technical control panel. The visual identity carries that idea from the executable icon to the duration window, floating widget and GitHub artwork.

## The eye

The eye is the primary brand mark and the session indicator.

- **Idle:** ready to begin.
- **Active:** open and paired with the remaining time.
- **Needs attention:** paired with a readable recovery message.

Color supports these states; the text and eye shape also communicate what is happening. The mark should remain recognizable at tray-icon sizes.

## Color and hierarchy

The palette uses ink as the foundation, violet for character, aqua for an active session, and coral for recovery.

| Role | Color |
| --- | --- |
| Ink background | `#101322` |
| Raised surface | `#1A1E32` |
| Violet primary | `#8B6CFF` |
| Aqua active | `#57E4CE` |
| Coral attention | `#FF8C7A` |
| Warm-white text | `#F4F3FF` |
| Muted text | `#A7ACC4` |

Typography uses Segoe UI and Segoe UI Semibold, with semibold duration numerals. The interface uses one intentional dark theme.

The duration window gives the time controls and the start action the clearest hierarchy. Presets make common durations easy to choose; days, hours and minutes remain available for exact input.

The floating widget puts the eye and countdown together in a compact rounded form. It can be moved without adding a full taskbar window.

## Interaction

The widget's gestures serve different jobs:

| Gesture | Purpose |
| --- | --- |
| Click | Start or add time |
| Hold | Deliberately end a running session |
| Drag | Move the widget |
| Right-click | Open settings and additional actions |

A visible hold-progress bar makes ending a session intentional. Dragging cancels that hold action. The widget avoids taking keyboard focus from the user's current work.

The duration dialog retains native Windows controls and keyboard actions: **Enter** confirms and **Escape** cancels. The desktop menu uses explicit labels for lid behavior, display behavior and battery rules.

## Product language

Use short, concrete labels. Say **Start session**, **Add time**, **End session** and **Needs attention**. A session is a timed request to keep Windows awake; it is not a promise that every sleep or shutdown condition can be blocked.

Recovery text should explain what failed and offer a direct action. Ending a session should explain that normal sleep settings apply again.

## Images

The project README uses:

- `images/hero.png` — brand artwork for the project overview.
- `images/duration.png` — the native duration-window preview.
- `images/widget-idle.png` — the native idle-widget preview.
- `images/widget-active.png` — the native active-widget preview.

Application previews use representative state, so a timer shown in an image is illustrative. Brand artwork should not be presented as evidence of a working feature.

When refreshing images, render or capture the current Windows UI, check small-text readability, and keep unrelated desktop content out of the frame.
