# Implementation Report

This section focuses on three implementation features in **Lumina**:  
1. how each image's **progress and difficulty are saved locally**,  
2. how the **shader converts any uploaded colour image into grayscale and gradually restores colour**,  
3. how the game calls the **Breathing API** and converts breathing data into painting speed.

## 1. Local Saving of Progress and Difficulty

The save system is implemented around `ImageProgressRepository`, which stores all painting progress in a local JSON file rather than relying on Unity scene state. This is important because the player may leave the game and later continue colouring the same picture from the exact previous state.

Each image is identified by an `imageId`, which is generated as a **SHA-1 hash of the original image bytes** when the file is scanned. This means the save key is tied to the file content rather than only the filename, so the game can reliably match progress to the correct image.

The repository stores the following data for each image:

- `imageId`
- `lockedDifficulty`
- `progress01`
- `gridX` and `gridY`
- `cells` array
- `lastUpdatedUtcTicks`

The core entry structure is:

```csharp
public class Entry
{
    public string imageId;
    public int lockedDifficulty;
    public float progress01;
    public int gridX;
    public int gridY;
    public float[] cells;
    public long lastUpdatedUtcTicks;
}
```

### Where the data is saved

The preferred save path is:

`UserContent/Lumina/Saves/luminate_image_progress_db.json`

If that folder is not writable, the code falls back to `Application.persistentDataPath`. This is handled in `Configure(...)`, so the game still works even if the preferred folder cannot be created.

```csharp
if (!string.IsNullOrEmpty(preferredDir) && EnsureWritable(preferredDir))
{
    _filePath = Path.Combine(preferredDir, FileName);
}
else
{
    _filePath = Path.Combine(Application.persistentDataPath, FileName);
}
```

### How progress is written

When the player leaves gameplay, `GameExitController` asks `Painter` for the current completion percentage and a copy of the cell array, then writes them into the repository:

```csharp
ImageProgressRepository.Set(
    imageId,
    entryController.CurrentDifficulty,
    painter.GridX,
    painter.GridY,
    cells,
    progress01
);
```

The important design choice is that the game does **not** save a screenshot or a painted texture. Instead, it saves a lightweight float array called `cells`, where each element stores the completion amount of one grid cell from `0` to `1`. This makes the save file small, easy to restore, and independent of the source image resolution.

### How difficulty is locked and restored

When the user selects an image and enters the game, `GameEntryController` checks whether that image already has saved progress. If it does, the previously used difficulty is restored automatically and the saved cell array is passed back into `Painter`.

```csharp
if (ImageProgressRepository.TryGet(imageId, out var entry) && entry != null && entry.progress01 > 0f)
{
    diff = (Difficulty)entry.lockedDifficulty;
    savedCells = entry.cells;
}

painter.BeginOrRestore(fullSprite, diff, savedCells);
```

This means difficulty becomes effectively **locked per image after progress exists**, which prevents a user from switching grid resolution halfway through a painting and breaking save consistency.

### Edge-case handling

Several safeguards are built into the save system:

- malformed JSON values are clamped in `Sanitize(...)`;
- invalid grid sizes are forced to at least `1 x 1`;
- missing `cells` arrays become empty arrays;
- saved progress is restored only if the array length matches the current grid size;
- the repository stores a **clone** of `cells` to avoid accidental mutation from outside the class.

So, the local persistence system is not just saving a percentage; it is saving the exact painting state and difficulty in a structured and recoverable way.

## 2. Shader: Turning Any Uploaded Colour Image into Grayscale, Then Restoring Colour

The visual effect is implemented using a custom Shader Graph material, `SG_GrayscaleToColor`, together with a runtime mask texture controlled by `Painter`.

### Why this design is useful

The game allows the player to upload arbitrary images from disk. Because these images are loaded at runtime, the system cannot depend on pre-authored grayscale assets. Instead, it takes the uploaded colour image as input and converts it into a grayscale version dynamically inside the shader.

### How the image enters the pipeline

When gameplay starts, `GameEntryController` loads the selected image file into a `Texture2D` and creates a `Sprite`. `Painter.BeginOrRestore(...)` then binds that texture to the material as `_MainTex`.

At the same time, `Painter` allocates a runtime `Texture2D` called `maskTex`, whose resolution matches the gameplay grid rather than the original image resolution. This mask is bound to the material as `_MaskTex`.

```csharp
runtimeMainMat.SetTexture(MainTexProp, sprite.texture);
AllocateMask(gridX, gridY);
runtimeMainMat.SetTexture(MaskTexProp, maskTex);
```

### How grayscale is generated

The shader graph samples `_MainTex`, extracts its RGB values, and computes luminance using a dot product with grayscale weights stored in `_GrayWeghts`:

- `0.299`
- `0.587`
- `0.114`

These are standard luminance weights. In other words, the shader does not simply average RGB channels; it computes a visually better grayscale intensity:

```text
gray = dot(colorRGB, GrayWeights)
```

That grayscale value is then replicated across R, G, and B to build a grayscale colour output.

### How colour is restored during painting

The key step is that the shader graph uses `_MaskTex` as the interpolation factor in a `Lerp` node:

- **A** = grayscale version of the image
- **B** = original colour image
- **T** = value sampled from `_MaskTex`

So the result is conceptually:

```text
outputColor = lerp(grayscaleColor, originalColor, maskValue)
```

This means:

- if `maskValue = 0`, the image stays fully grayscale;
- if `maskValue = 1`, the image is fully restored to colour;
- if `maskValue` is between `0` and `1`, the colour is only partially restored.

### How the mask is updated

`Painter` stores one float per grid cell. Whenever the player paints, covered cells are increased gradually:

```csharp
float before = cell[idx];
float after = Mathf.Clamp01(before + delta);
cell[idx] = after;
```

Then `ApplyMask()` writes those values into the runtime mask texture:

```csharp
float v = Mathf.Clamp01(cell[y * gridX + x]);
byte b = (byte)Mathf.RoundToInt(v * 255f);
maskTex.SetPixel(x, y, new Color32(b, 0, 0, 255));
```

Because the mask values increase gradually from `0` to `255`, the colour recovery also appears gradual rather than binary. This is what creates the "slow reveal" painting effect.

### Why this works for any uploaded image

This design is resolution-independent:

- the uploaded image is sampled directly as `_MainTex`;
- the grayscale conversion happens in the shader at runtime;
- the save system only stores grid-cell progress, not full image pixels.

As a result, the game can support arbitrary uploaded photos while keeping the painting logic simple and lightweight.

## 3. Breathing API: How It Is Called and How Painting Speed Is Calculated

The `Breath` component connects the Unity client to an external breathing backend through HTTP. It does not directly perform breathing analysis inside Unity. Instead, Unity acts as a lightweight consumer of processed breathing metrics.

### API endpoints

The component builds three URLs from a configurable `apiBaseUrl`:

- `/webhooks/breathing-volume`
- `/webhooks/breathing-regularity`
- `/webhooks/breathing-rate`

The default base URL is:

`http://127.0.0.1:8000`

### How the API is called

When the component is enabled, it starts a polling coroutine. Every `0.10` seconds by default, Unity sends `GET` requests using `UnityWebRequest.Get(...)`.

```csharp
yield return GetFloat(UrlBreathingVolume, "breathing_volume", v => vol = v);
yield return GetFloat(UrlBreathingRegularity, "breathing_regularity", v => r = v);
yield return GetFloat(UrlBreathingRate, "breathing_rate", v => rr = v);
```

Each response is expected to contain a simple JSON field such as:

```json
{ "breathing_volume": 0.034 }
```

The code then extracts the numeric value with `TryParseSingleFloat(...)`. If a request fails or times out, that update is skipped instead of crashing the game.

### What each breathing metric does

- **breathing_volume** is the main control signal.
- **breathing_regularity** optionally adds a quality bonus.
- **breathing_rate** optionally adds a tempo bonus if the breathing rate falls inside a target BPM range.

Only `breathing_volume` is required for the core speed update. The others refine the result.

### Step 1: normalize breathing volume

The raw breathing volume may vary greatly between users, so the code performs simple auto-calibration using running minimum and maximum values:

```csharp
_vMin = Mathf.Lerp(_vMin, Mathf.Min(_vMin, breathingVolume), calibLerp);
_vMax = Mathf.Lerp(_vMax, Mathf.Max(_vMax, breathingVolume), calibLerp);
float v01 = Mathf.Clamp01((breathingVolume - _vMin) / (_vMax - _vMin));
```

This converts the raw signal into a normalized `v01` value between `0` and `1`.

### Step 2: gate painting on and off

The same normalized volume is also used to determine whether painting is allowed. The code uses **hysteresis**:

```csharp
if (!_paintGateState && v01 >= breathOnThreshold01) _paintGateState = true;
else if (_paintGateState && v01 <= breathOffThreshold01) _paintGateState = false;
```

By default, painting turns on at `0.20` and turns off at `0.12`. Using two thresholds prevents rapid flickering if the breathing signal hovers near the boundary.

### Step 3: compute the speed multiplier

The main multiplier is computed from the normalized volume:

```csharp
float m = minMultiplier + (maxMultiplier - minMultiplier) * Mathf.Pow(v01, gamma);
```

Here:

- `minMultiplier = 0.2`
- `maxMultiplier = 2.0`
- `gamma = 0.75`

Because `gamma < 1`, low breathing signals are still given some sensitivity instead of feeling unresponsive.

Then two optional bonuses may be applied:

```csharp
float bonus = (1f - regularityWeight) + regularityWeight * Mathf.Clamp01(regularity01);
m *= bonus;
```

and:

```csharp
float bpm = breathingRateBps * 60f;
if (bpm >= targetBpmMin && bpm <= targetBpmMax)
    m *= (1f + Mathf.Max(0f, bpmBonus));
```

So the final multiplier is affected by:

- stronger breathing volume,
- more regular breathing,
- a breathing rate inside the target range.

### Step 4: convert multiplier into painting speed

The multiplier is not used directly as fill amount. First, it is smoothed over time:

```csharp
float alpha = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothTauSec));
_smoothedMultiplier = Mathf.Lerp(_smoothedMultiplier, _targetMultiplier, alpha);
```

Then it is converted into `secondsPerCell`:

```csharp
float effSeconds = baseSecondsPerCell / Mathf.Max(0.01f, _smoothedMultiplier);
painter.SetSecondsPerCell(effSeconds);
```

This means:

- a **larger multiplier** produces a **smaller `secondsPerCell`**,
- a smaller `secondsPerCell` means each cell fills faster.

Finally, `Painter` converts that value into per-frame fill speed:

```csharp
float delta = (1f / Mathf.Max(0.1f, secondsPerCell)) * Time.deltaTime;
```

So the full chain is:

`Breathing API -> normalized volume -> multiplier -> smoothed multiplier -> secondsPerCell -> per-frame cell fill delta`

### Edge-case handling

This API integration includes several practical safeguards:

- if `painter` is missing, `Breath` disables itself early;
- if `breathOffThreshold01 >= breathOnThreshold01`, the code corrects it automatically;
- failed HTTP requests simply skip that cycle;
- the last valid regularity and rate values are reused if no new value arrives;
- smoothing avoids abrupt speed jumps;
- the final multiplier is clamped to avoid extreme speeds.

## Conclusion

These three systems work together as the main implementation backbone of the feature. The local save system preserves image-specific difficulty and painting state, the shader makes any uploaded image compatible with the grayscale-to-colour gameplay loop, and the Breathing API translates external breathing data into a calm, progressive colouring speed.
