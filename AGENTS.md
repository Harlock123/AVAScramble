# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A side-scrolling shooter in the style of Konami's 1981 arcade game Scramble, in C# on .NET 10 and Avalonia 12.
One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). Everything in it is original:
the six stages and the pixel art are made in code, and the sounds and tunes are synthesised by
`tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAScramble.slnx                     # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAScramble.slnx                      # every test runs headless: no window opens
dotnet run --project src/AVAScramble              # play it; --no-audio for silence
SCRAMBLE_SHOTS=$PWD/Images dotnet test tests/AVAScramble.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                      # rebuild src/AVAScramble/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAScramble/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract, play, high scores) and the phases of a game.
  - `GameEngine.Play.cs` steps a stage: the scroll, the jet, its laser and bombs, fuel, and what stands on the
    ground or flies.
  - `Entities.cs` has the jet, the things in the air and `Controls`.
  - `AttractPilot.cs` is the computer player, used by the attract mode and the tests.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVAScramble/Models/`: `Field.cs` (the 256x176 screen, the fixed step, and the stages, each worked out
  from its number so it is the same every time) and `Sprites.cs` (the pixel art).
- `src/AVAScramble/Views/`: `GameCanvas` (split over partial files) scales the game to the 1024x768 frame, draws
  it and the strip along the top, and runs the 60 Hz timer.
- `src/AVAScramble/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVAScramble.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: gameplay, screenshot and Shift-release
  tests.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` advances in fixed 1/60 s steps (`Field.Step`), independent of the frame
  rate, so a seed always plays the same game. Draw random numbers from the engine's seeded `_rng`, never from
  `Random.Shared`. The stage layouts have their own seeded `Random`, so keep them independent of the game seed.
- **Engine first, drawing second.** Gameplay belongs in `Engine/` and `Models/`, where tests can reach it
  without a window. `Views/` only reads the engine and draws it.
- **Input goes through `InputHandler`.** `Controls` here holds `double` `Dx`/`Dy`, so the stick steers by how far
  it is pushed. Up/down must produce the same sign the engine expects (negative is up the screen). Of two
  opposite keys held, the latest wins. Test new controls through `InputHandler`, not by setting `Controls`
  directly.
  - Keys are tracked by `PhysicalKey`, because on some Linux setups Avalonia reports the release of Shift as
    CapsLock. `ShiftReleaseTests` covers this.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVAScramble`); tests keep
  the table in a temporary folder, and the screenshot tests use a silent `SoundManager`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
