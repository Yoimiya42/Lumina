# CHANGELOG
All notable changes to this project will be documented in this file.

---


## [v1.1.0] (Private Release)- 2026-02-09

### Added
- `MotionInput` integration: Use `fun-shapes/point-click.json`, in `data/config.json`:
  ```json
  {
    "mode": "fun-shapes/point-click",
    ...
  }
  ```
- `FunBreathing` integration: breathing data can now influence coloring speed.
### Potential Issues
- The built-in microphone still struggles to capture breathing data stably. Blowing or tapping can cause significant fluctuations and affect the coloring speed. 
- Gesture-based button interaction via MotionInput may feel less responsive. 
### Upcoming
- [ ] ❗Optimize the `FunBreathing` or configuration of the device for breathing detection.
- [ ] ❗Figure out the parameters and gestures for `point-click` to make the interaction more responsive and intuitive.
- [ ] Make a game tutorial video and post on Youtube.
- [ ] Record a video to demonstrate how to create albums and manage images.
- [ ] Better UI layouts and color schemes.
- [ ] Color completion animation

### Download
- Windows (x64): `Lumina_v1.1.0.zip`

### How to run (Windows)
1. Unzip the file
2. Double-click `Lumina.bat`
3. Add/remove images in `Lumina/UserContent/Lumina/Images/` (It will appear after the first run), create albums via creating subfolders.

---

## [v1.0.1] - 2026-02-09

### Fixed
- Optimize the formula for calculating coloring speed using breathing data.
---

## [v1.0.0] - 2026-02-09 

### Overview
This release contains the standalone core game only.

### Included
- Core gameplay systems
- Windows standalone build

### Not Included
- MotionInput integration
- FunBreathing integration

### Download
- Windows (x64): `Lumina_v1.0.0_windows_x64.zip`

### How to run (Windows)
1. Unzip the file
2. Double-click `Lumina.exe`
3. Add/remove images in `UserContent/Lumina/Images/` (It will appear after the first run), create albums via creating subfolders.