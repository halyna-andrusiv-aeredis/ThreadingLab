# Feature spec — Settings sound toggle (EVAL FIXTURE — do not implement)

> Frozen input for command evals. Deliberately small, generic, and Unity-typical so the
> same fixture works on any project regardless of `profile.yaml`. Never wire this into a
> real feature or `features/index.yaml`.

## Summary
A single **Sound On/Off** toggle in the Settings screen. When off, all game audio is
muted; the choice persists across sessions and is restored on next launch.

## Requirements
- **REQ-001** The Settings screen shows a Sound toggle reflecting the current mute state.
- **REQ-002** Toggling it mutes/unmutes all game audio immediately.
- **REQ-003** The chosen state persists and is restored on the next app launch.
- **REQ-004** If no stored value exists, the toggle defaults to **Sound On**.

## Acceptance criteria
- **AC-001** *Given* the Settings screen is open, *when* it appears, *then* the toggle
  matches the current mute state.
- **AC-002** *Given* sound is on, *when* the user toggles it off, *then* audio mutes
  within the same frame and no error is logged.
- **AC-003** *Given* the user set sound off, *when* the app is relaunched, *then* the
  toggle shows off and audio starts muted.
- **AC-004** *Given* a fresh install with no stored value, *when* Settings opens, *then*
  the toggle shows **on**.

## Notes / constraints
- Persistence uses the project's established save mechanism (see `profile.yaml → stack.save`).
- No new audio system; mute through the project's existing audio service.
