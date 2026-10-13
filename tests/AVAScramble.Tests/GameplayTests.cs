using AVAScramble.Engine;
using AVAScramble.Models;

namespace AVAScramble.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scramble-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>A stage under way with nothing on the ground, no flyers, and no fuel used.</summary>
    private GameEngine Calm(int stage = 2, double distance = 200, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(stage, distance);
        g.Calm();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Tick(c);
    }

    [Fact]
    public void Every_stage_leaves_room_to_fly_and_has_things_to_hit()
    {
        for (int n = 1; n <= Field.Stages; n++)
        {
            var s = Stage.Build(n);
            Assert.InRange(s.Length, 1700, 1900);
            for (double x = 0; x < s.Length + Field.Width; x += 2)
            {
                Assert.InRange(s.GroundAt(x), 60, Field.Height);
                if (s.Ceiling is not null) Assert.True(s.GroundAt(x) - s.CeilingAt(x) >= 40, $"stage {n} at {x}");
            }
            Assert.Contains(s.Placements, p => p.Kind == GroundKind.Fuel);
            Assert.True(s.Placements.Count > 15, $"stage {n}");
        }
        Assert.Contains(Stage.Build(6).Placements, p => p.Kind == GroundKind.Base);
        Assert.NotNull(Stage.Build(3).Ceiling);
        Assert.NotEmpty(Stage.Build(4).Buildings);
    }

    [Fact]
    public void The_course_scrolls_and_the_stages_follow_one_another()
    {
        var g = Calm(stage: 1);
        double d = g.Distance;
        Steps(g, 60);
        Assert.Equal(d + Field.ScrollSpeed, g.Distance, 3);
        g.JumpTo(1, g.Stage.Length - 5);
        g.Calm();
        Steps(g, 30);
        Assert.Equal(2, g.StageNumber);
        Assert.Equal(Phase.Ready, g.Phase);
    }

    [Fact]
    public void The_laser_destroys_flyers_and_things_on_the_ground_but_not_meteors()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutShip(new Vec(40, 80));
        var ufo = g.PutFlyer(FlyerKind.Ufo, new Vec(150, 80));
        for (int i = 0; i < 60 && !ufo.Dead; i++) { ufo.Pos = new Vec(150, 80); g.Tick(new Controls(0, 0, Fire: true)); }
        Assert.True(ufo.Dead);
        Assert.Equal(GameEngine.UfoPoints, g.Score - Score10s(g));
        Assert.Contains(Sfx.Laser, heard);

        var meteor = g.PutFlyer(FlyerKind.Fireball, new Vec(200, 80), speed: 0);
        Steps(g, 40, new Controls(0, 0, Fire: true));
        Assert.False(meteor.Dead);
    }

    /// <summary>The points for time in the air, so the others can be told apart.</summary>
    private static int Score10s(GameEngine g) => g.Score / 10 % 10 == 0 ? g.Score - g.Score / 100 * 100 : 0;

    [Fact]
    public void Bombs_fall_forward_and_hit_what_they_land_on()
    {
        var g = Calm();
        g.FlatGround();
        g.PutShip(new Vec(40, 60));
        var tank = g.PutTarget(GroundKind.Fuel, 120);
        g.SetFuel(50);
        // Drop where it'll land on the tank: try each moment until one does.
        bool hit = false;
        for (int i = 0; i < 240 && !hit; i++)
        {
            g.Tick(new Controls(0, 0, Bomb: i % 2 == 0));
            hit = tank.Dead;
        }
        Assert.True(hit);
        Assert.True(g.Fuel > 50, "fuel from the tank");
        Assert.True(g.Score >= GameEngine.Points(GroundKind.Fuel));
        // Only two bombs in the air at once.
        g.PutShip(new Vec(40, 20));
        for (int i = 0; i < 10; i++) g.Tick(new Controls(0, 0, Bomb: i % 2 == 0));
        Assert.Equal(GameEngine.MaxBombs, g.Bombs.Count);
    }

    [Fact]
    public void Fuel_runs_down_and_with_none_left_the_ship_drops()
    {
        var g = Calm();
        g.FlatGround();
        g.Uncalm();
        double f = g.Fuel;
        Steps(g, 60);
        Assert.Equal(f - g.FuelDrain, g.Fuel, 1);
        g.SetFuel(0.1);
        for (int i = 0; i < 60 * 6 && g.Phase == Phase.Playing; i++) g.Tick(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Equal("ran out of fuel", g.LastDeath);
    }

    [Fact]
    public void Hitting_the_ground_or_the_roof_is_fatal()
    {
        var g = Calm(stage: 1);
        for (int i = 0; i < 60 * 5 && g.Phase == Phase.Playing; i++) g.Tick(new Controls(0, 1));
        Assert.Equal("crashed into the ground", g.LastDeath);

        g = Calm(stage: 3);
        for (int i = 0; i < 60 * 5 && g.Phase == Phase.Playing; i++) g.Tick(new Controls(0, -1));
        Assert.Equal("hit the roof", g.LastDeath);
        Steps(g, (int)(60 * GameEngine.DyingSeconds) + 2);
        Assert.Equal(GameEngine.StartingLives - 1, g.Lives);
        Assert.Equal(0, g.Distance, 0); // back to the start of the stage
        Assert.Equal(GameEngine.FuelMax, g.Fuel);
    }

    [Fact]
    public void Rockets_take_off_in_the_mountains_and_bring_the_ship_down()
    {
        var g = new GameEngine(2, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(1, 100);
        g.Calm();
        g.PutShip(new Vec(40, 40));
        var rocket = g.PutTarget(GroundKind.Rocket, 100);
        for (int i = 0; i < 60 * 4 && !rocket.Flying; i++) { g.Tick(default); rocket.X = g.Distance + 100; }
        Assert.True(rocket.Flying);
        double y = rocket.Y;
        Steps(g, 10);
        Assert.True(rocket.Y < y);

        // In the caves they stay put.
        g = Calm(stage: 3, distance: 100);
        var still = g.PutTarget(GroundKind.Rocket, 100);
        for (int i = 0; i < 60 * 3; i++) { g.Tick(default); still.X = g.Distance + 100; }
        Assert.False(still.Flying);
    }

    [Fact]
    public void Ufos_weave_in_waves_and_meteors_streak_through_the_caves()
    {
        var g = new GameEngine(3, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(2, 300);
        for (int i = 0; i < 60 * 3 && g.Flyers.Count == 0; i++) g.Tick(default);
        Assert.Equal(4, g.Flyers.Count(f => f.Kind == FlyerKind.Ufo));
        var ufo = g.Flyers[0];
        double y0 = ufo.Pos.Y, x0 = ufo.Pos.X;
        Steps(g, 20);
        Assert.True(ufo.Pos.X < x0);
        Assert.NotEqual(y0, ufo.Pos.Y);

        g.JumpTo(3, 300);
        for (int i = 0; i < 60 * 3 && g.Flyers.Count == 0; i++) g.Tick(default);
        Assert.Contains(g.Flyers, f => f.Kind == FlyerKind.Fireball);

        var c = Calm(stage: 3);
        c.PutShip(new Vec(40, 90));
        c.PutFlyer(FlyerKind.Fireball, new Vec(120, 90), speed: 140);
        for (int i = 0; i < 120 && c.Phase == Phase.Playing; i++) c.Tick(default);
        Assert.Equal("hit by a meteor", c.LastDeath);
    }

    [Fact]
    public void A_mystery_is_worth_100_200_or_300()
    {
        var g = Calm();
        g.FlatGround();
        int before = g.Score;
        var m = g.PutTarget(GroundKind.Mystery, 140);
        g.PutShip(new Vec(40, m.Y - 4));
        for (int i = 0; i < 60 && !m.Dead; i++) { m.X = g.Distance + 140; g.Tick(new Controls(0, 0, Fire: true)); }
        Assert.True(m.Dead);
        int gained = g.Score - before;
        Assert.Contains(gained - gained % 100, new[] { 100, 200, 300 });
    }

    [Fact]
    public void Destroying_the_base_completes_the_mission_and_sends_the_ship_round_again()
    {
        var g = Calm(stage: 6, distance: 300);
        g.FlatGround(6);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        var b = g.PutTarget(GroundKind.Base, 130);
        g.PutShip(new Vec(40, b.Y - 10));
        for (int i = 0; i < 60 && !b.Dead; i++) { b.X = g.Distance + 130; g.Tick(new Controls(0, 0, Fire: true)); }
        Assert.True(b.Dead);
        Assert.Equal(Phase.Mission, g.Phase);
        Assert.Contains(Sfx.BaseDown, heard);
        Steps(g, (int)(60 * GameEngine.MissionSeconds) + 2);
        Assert.Equal(1, g.StageNumber);
        Assert.Equal(1, g.Lap);
    }

    [Fact]
    public void Missing_the_base_means_flying_the_last_stage_again()
    {
        var g = Calm(stage: 6);
        g.JumpTo(6, g.Stage.Length - 10);
        g.Calm();
        Steps(g, 30);
        Assert.Equal(6, g.StageNumber);
        Assert.True(g.Distance < 10);
    }

    [Fact]
    public void Points_for_time_in_the_air_and_one_extra_ship_at_10000()
    {
        var g = Calm();
        int s = g.Score;
        Steps(g, 60 * 3 + 1);
        Assert.Equal(s + 30, g.Score);
        Assert.Equal(3, g.Lives);
        g.AddScore(10000);
        Assert.Equal(4, g.Lives);
        g.AddScore(20000);
        Assert.Equal(4, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_gets_well_into_the_course_and_the_game_ends_in_the_table()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        var pilot = new AttractPilot(1);
        for (int i = 0; i < 60 * 60 * 4 && g.State == GameState.Playing && g.StageNumber < 3; i++) g.Tick(pilot.Decide(g));
        Assert.True(g.StageNumber >= 3, $"reached stage {g.StageNumber}; lost: {string.Join("; ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Tick(new Controls(0, 1));
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        if (g.HighScores.Qualifies(g.Score))
        {
            Assert.Equal(GameState.EnterInitials, g.State);
            g.Initials!.Finish();
            g.Idle(2);
        }
        Assert.Equal(GameState.HighScores, g.State);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Tick(pilot.Decide(g));
            return $"{g.StageNumber} {g.Distance:0.00} {g.Score} {g.Lives} {g.Fuel:0.00} {g.Ship.Pos} {g.Flyers.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 20)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Engine, heard);
        Assert.Contains(Sfx.Laser, heard);
        g.Idle((int)(60 * (GameEngine.AttractSeconds - 18)));
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.EngineStop, heard.Last(s => s is Sfx.Engine or Sfx.EngineStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_holds_everything()
    {
        var g = Calm();
        var at = g.Ship.Pos;
        double d = g.Distance;
        g.Pause();
        Steps(g, 30, new Controls(1, 0));
        Assert.Equal(at, g.Ship.Pos);
        Assert.Equal(d, g.Distance);
        g.Resume();
        Steps(g, 30, new Controls(1, 0));
        Assert.True(g.Ship.Pos.X > at.X);
    }

    [Fact]
    public void Every_sound_and_sprite_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        Assert.All(Enum.GetValues<Sfx>().Where(s => s != Sfx.EngineStop).Select(s => s.ToString()), f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
        foreach (var s in new[] { Sprites.Ship, Sprites.Rocket, Sprites.Ufo[0] })
            Assert.All(s.Rows.SelectMany(r => r).Where(ch => ch != '.'), ch => Assert.True(Sprites.Colours.ContainsKey(ch), $"colour {ch}"));
    }
}
