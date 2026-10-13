using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAScramble.Engine;
using AVAScramble.Models;
using AVAScramble.Views;
using Controls = AVAScramble.Engine.Controls;

namespace AVAScramble.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set SCRAMBLE_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scramble-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("SCRAMBLE_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAScramble-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>The pilot flies a stage until the moment's right (or a while has gone by).</summary>
    private static void Fly(GameEngine g, int stage, double distance, int seed, Func<GameEngine, bool> until, int max = 60 * 30)
    {
        g.StartGame();
        g.JumpTo(stage, distance);
        var pilot = new AttractPilot(seed);
        for (int i = 0; i < max && g.State == GameState.Playing && g.StageNumber == stage; i++)
        {
            g.Tick(pilot.Decide(g));
            if (i > 90 && g.Phase == Phase.Playing && until(g)) break;
        }
    }

    [AvaloniaFact]
    public void The_title_and_the_mountains()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        Fly(g, 1, 300, 2, x => x.Bombs.Count > 0 && x.Targets.Any(t => t.Flying) && x.Shots.Count > 0);
        Shot(w, c, "mountains");
        w.Close();
    }

    [AvaloniaFact]
    public void The_ufos_and_the_caves()
    {
        var (w, c, g) = Open(seed: 3);
        Fly(g, 2, 400, 3, x => x.Flyers.Count(f => f.Pos.X < 220) >= 4);
        Shot(w, c, "ufos");
        Fly(g, 3, 400, 4, x => x.Flyers.Count(f => f.Pos.X < 240) >= 2);
        Shot(w, c, "caves");
        w.Close();
    }

    [AvaloniaFact]
    public void The_city_and_the_maze()
    {
        var (w, c, g) = Open(seed: 5);
        Fly(g, 4, 500, 5, x => x.Targets.Any(t => !t.Dead && x.ScreenX(t.X) is > 120 and < 220));
        Shot(w, c, "city");
        Fly(g, 5, 500, 6, x => x.Ship.Pos.X > 50);
        Shot(w, c, "maze");
        w.Close();
    }

    [AvaloniaFact]
    public void The_base()
    {
        var (w, c, g) = Open(seed: 6);
        g.StartGame();
        g.JumpTo(6, g.Stage.Length - Field.Width + 30);
        g.PutShip(new Vec(60, 110));
        g.SetFuel(18);
        g.Tick(new Controls(0, 0, Bomb: true));
        for (int i = 0; i < 12; i++) g.Tick(new Controls(0, 0, Fire: true));
        Shot(w, c, "base");
        w.Close();
    }
}
