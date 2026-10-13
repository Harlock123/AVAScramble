using AVAScramble.Models;

namespace AVAScramble.Engine;

public sealed partial class GameEngine
{
    public const double ShipSpeed = 95;
    public const double LaserSpeed = 380;
    public const double FireEvery = 0.14;
    public const int MaxShots = 4;
    public const int MaxBombs = 2;
    public const double BombVx = 52;
    public const double BombVy = 20;
    public const double Gravity = 170;
    public const double FuelMax = 100;
    public const double FuelGain = 14;
    public const double ReadySeconds = 1.6;
    public const double DyingSeconds = 2.2;
    public const double MissionSeconds = 4;
    public const double ShipX0 = 40;

    public static int Points(GroundKind k, bool flying = false) => k switch
    {
        GroundKind.Rocket => flying ? 80 : 50,
        GroundKind.Fuel => 150,
        GroundKind.Base => 800,
        _ => 0,
    };

    public const int UfoPoints = 100;
    public const GroundKind FuelKind = GroundKind.Fuel;

    public int StageNumber { get; private set; } = 1;
    public int Lap { get; private set; }
    public Stage Stage { get; private set; } = null!;
    /// <summary>How far the stage has scrolled.</summary>
    public double Distance { get; private set; }
    public double Fuel { get; private set; } = FuelMax;
    public Ship Ship { get; } = new();
    public List<Shot> Shots { get; } = [];
    public List<Bomb> Bombs { get; } = [];
    public List<Target> Targets { get; } = [];
    public List<Flyer> Flyers { get; } = [];
    public string? LastDeath { get; private set; }
    internal List<string> LossLog { get; } = [];

    private static readonly Stage[] Stages = Enumerable.Range(1, Field.Stages).Select(Stage.Build).ToArray();
    private double _fireClock, _spawnClock = 1, _secondClock;
    private bool _bombPrev, _lowFuelWarned, _calm;

    public double FuelDrain => 2.3 + 0.3 * Lap;
    public double ScreenX(double worldX) => worldX - Distance;

    /// <summary>A safe height to fly at, over a world x: halfway down a cave, or well clear of the ground.</summary>
    public double SafeY(double worldX)
    {
        double g = Stage.HighestGround(worldX - 12, worldX + 12);
        if (Stage.Ceiling is not null) return (Stage.LowestCeiling(worldX - 12, worldX + 12) + g) / 2;
        return Math.Max(24, g - 44);
    }

    // ----- stages ---------------------------------------------------------------------------------

    private void StartStage(int number, bool keepFuel = false)
    {
        StageNumber = number;
        Stage = Stages[number - 1];
        Distance = 0;
        Targets.Clear();
        if (!_calm)
            foreach (var p in Stage.Placements)
                Targets.Add(new Target { Kind = p.Kind, X = p.X, Y = Stage.HighestGround(p.X - 5, p.X + 5) });
        Flyers.Clear();
        Shots.Clear();
        Bombs.Clear();
        Ship.Pos = new Vec(ShipX0, SafeY(ShipX0));
        Ship.Falling = false;
        if (!keepFuel) Fuel = FuelMax;
        _lowFuelWarned = false;
        _spawnClock = 1.5;
        SetPhase(Phase.Ready);
        EngineSound(false);
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Ready:
                if (PhaseTime < ReadySeconds) return;
                SetPhase(Phase.Playing);
                EngineSound(true);
                return;
            case Phase.Dying:
                MoveFlyers(dt);
                if (PhaseTime >= DyingSeconds) LoseLife();
                return;
            case Phase.Mission:
                Shots.Clear();
                Bombs.Clear();
                if (PhaseTime < MissionSeconds) return;
                Lap++;
                StartStage(1);
                return;
        }

        Distance += Field.ScrollSpeed * dt;
        if (Distance >= Stage.Length)
        {
            // On to the next stage; past the base without destroying it, round the base again.
            StartStage(StageNumber < Field.Stages ? StageNumber + 1 : Field.Stages, keepFuel: true);
            return;
        }
        // Ten points for every second still flying.
        _secondClock += dt;
        while (_secondClock >= 1) { _secondClock -= 1; AddScore(10); }
        if (!_calm) Fuel = Math.Max(0, Fuel - FuelDrain * dt);
        if (Fuel < 25 && !_lowFuelWarned) { _lowFuelWarned = true; Play(Sfx.LowFuel); }
        if (Fuel <= 0) Ship.Falling = true;

        MoveShip(dt, c);
        Fire(dt, c);
        MoveShots(dt);
        if (Phase != Phase.Playing) return;
        MoveBombs(dt);
        if (Phase != Phase.Playing) return;
        MoveTargets(dt);
        SpawnFlyers(dt);
        MoveFlyers(dt);
        Collide();
    }

    private void LoseLife()
    {
        Lives--;
        if (Lives <= 0) { GameOver(); return; }
        StartStage(StageNumber);
    }

    private void Die(string why)
    {
        LastDeath = why;
        LossLog.Add($"stage {StageNumber} at {Distance:0}: {why}");
        Effects.Add(new Effect { Kind = EffectKind.BigBlast, Pos = Ship.Pos, Life = 1.6 });
        Shots.Clear();
        Bombs.Clear();
        EngineSound(false);
        Play(Sfx.Death);
        SetPhase(Phase.Dying);
    }

    // ----- the ship -------------------------------------------------------------------------------

    private void MoveShip(double dt, Controls c)
    {
        var p = Ship.Pos;
        if (Ship.Falling)
        {
            // Out of fuel: it noses down and drops.
            Ship.Pos = p + new Vec(10 * dt, 55 * dt);
            return;
        }
        double dx = Math.Clamp(c.Dx, -1, 1), dy = Math.Clamp(c.Dy, -1, 1);
        p += new Vec(dx, dy) * (ShipSpeed * dt);
        Ship.Pos = new Vec(Math.Clamp(p.X, 10, 170), Math.Clamp(p.Y, 6, Field.Height - 6));
    }

    private void Fire(double dt, Controls c)
    {
        _fireClock -= dt;
        bool alive = !Ship.Falling;
        if (alive && c.Fire && _fireClock <= 0 && Shots.Count < MaxShots)
        {
            _fireClock = FireEvery;
            Shots.Add(new Shot { Pos = Ship.Pos + new Vec(9, 0.5) });
            Play(Sfx.Laser);
        }
        if (alive && c.Bomb && !_bombPrev && Bombs.Count < MaxBombs)
        {
            Bombs.Add(new Bomb { Pos = Ship.Pos + new Vec(2, 4), Vel = new Vec(BombVx, BombVy) });
            Play(Sfx.BombDrop);
        }
        _bombPrev = c.Bomb;
    }

    /// <summary>The box a thing on the ground takes up, in screen terms.</summary>
    public (double X0, double Y0, double X1, double Y1) Box(Target t)
    {
        double sx = ScreenX(t.X);
        return t.Kind switch
        {
            GroundKind.Rocket => (sx - 3, t.Y - 13, sx + 3, t.Y),
            GroundKind.Fuel => (sx - 5, t.Y - 11, sx + 5, t.Y),
            GroundKind.Mystery => (sx - 5, t.Y - 9, sx + 5, t.Y),
            _ => (sx - 14, t.Y - 18, sx + 14, t.Y),
        };
    }

    private static bool Inside(Vec p, (double X0, double Y0, double X1, double Y1) b, double pad = 0) =>
        p.X >= b.X0 - pad && p.X <= b.X1 + pad && p.Y >= b.Y0 - pad && p.Y <= b.Y1 + pad;

    /// <summary>Solid: the ground, the cave roof.</summary>
    private bool InRock(Vec p) => p.Y >= Stage.GroundAt(p.X + Distance) || (Stage.Ceiling is not null && p.Y <= Stage.CeilingAt(p.X + Distance));

    private void MoveShots(double dt)
    {
        foreach (var s in Shots)
        {
            s.Pos += new Vec(LaserSpeed * dt, 0);
            if (s.Dead || s.Pos.X > Field.Width + 4 || InRock(s.Pos)) { s.Dead = true; continue; }
            foreach (var t in Targets)
            {
                if (t.Dead || !Inside(s.Pos, Box(t), 1)) continue;
                s.Dead = true;
                Destroy(t);
                break;
            }
            if (s.Dead) continue;
            foreach (var f in Flyers)
            {
                if (f.Dead || (f.Pos - s.Pos).Length > 7) continue;
                s.Dead = true;
                if (f.Kind == FlyerKind.Ufo)
                {
                    f.Dead = true;
                    AddScore(UfoPoints);
                    Popup(f.Pos, UfoPoints.ToString());
                    Effects.Add(new Effect { Kind = EffectKind.Blast, Pos = f.Pos, Life = 0.5 });
                    Play(Sfx.Boom);
                }
                break;
            }
        }
        Shots.RemoveAll(s => s.Dead);
    }

    private void MoveBombs(double dt)
    {
        foreach (var b in Bombs)
        {
            b.Vel += new Vec(0, Gravity * dt);
            b.Pos += b.Vel * dt;
            if (b.Pos.X > Field.Width + 8 || b.Pos.Y > Field.Height + 8) { b.Dead = true; continue; }
            var hit = Targets.FirstOrDefault(t => !t.Dead && Inside(b.Pos, Box(t), 1));
            if (hit is null && !InRock(b.Pos)) continue;
            b.Dead = true;
            // The blast takes out anything on the ground close by.
            Effects.Add(new Effect { Kind = EffectKind.Blast, Pos = b.Pos with { X = b.Pos.X + Distance }, OnGround = true, Life = 0.5 });
            Play(Sfx.Boom);
            foreach (var t in Targets)
            {
                if (t.Dead) continue;
                var box = Box(t);
                if (t == hit || (b.Pos.X >= box.X0 - 7 && b.Pos.X <= box.X1 + 7 && b.Pos.Y >= box.Y0 - 6 && b.Pos.Y <= box.Y1 + 6)) Destroy(t);
            }
        }
        Bombs.RemoveAll(b => b.Dead);
    }

    private void Destroy(Target t)
    {
        t.Dead = true;
        var at = new Vec(ScreenX(t.X), t.Y - 6);
        int points = t.Kind == GroundKind.Mystery ? 100 * (1 + _rng.Next(3)) : Points(t.Kind, t.Flying);
        AddScore(points);
        Effects.Add(new Effect { Kind = t.Kind == GroundKind.Base ? EffectKind.BigBlast : EffectKind.Blast, Pos = at with { X = t.X }, OnGround = true, Life = t.Kind == GroundKind.Base ? 2 : 0.5 });
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at with { X = t.X }, OnGround = true, Life = 1, Text = points.ToString() });
        switch (t.Kind)
        {
            case GroundKind.Fuel:
                Fuel = Math.Min(FuelMax, Fuel + FuelGain);
                if (Fuel >= 25) _lowFuelWarned = false;
                Ship.Falling = false;
                Play(Sfx.Fuel);
                break;
            case GroundKind.Mystery: Play(Sfx.Mystery); break;
            case GroundKind.Base:
                Play(Sfx.BaseDown);
                EngineSound(false);
                SetPhase(Phase.Mission);
                break;
            default: Play(Sfx.Boom); break;
        }
    }

    // ----- things on the ground, and flyers ---------------------------------------------------------

    private void MoveTargets(double dt)
    {
        foreach (var t in Targets)
        {
            if (t.Dead) continue;
            double sx = ScreenX(t.X);
            if (sx < -30) { t.Dead = true; continue; }
            if (t.Kind != GroundKind.Rocket) continue;
            if (!t.Flying)
            {
                // In some stages rockets take off as the ship comes near.
                if (Stage.RocketsFly && sx > Ship.Pos.X + 16 && sx < Ship.Pos.X + 110 && _rng.NextDouble() < (0.7 + 0.2 * Lap) * dt)
                {
                    t.Flying = true;
                    t.Vy = -30;
                    Play(Sfx.RocketLaunch);
                }
                continue;
            }
            t.Vy = Math.Max(-130, t.Vy - 90 * dt);
            t.Y += t.Vy * dt;
            if (t.Y < -20) t.Dead = true;
        }
    }

    private void SpawnFlyers(double dt)
    {
        if (_calm) return;
        _spawnClock -= dt;
        if (_spawnClock > 0) return;
        double wx = Distance + Field.Width;
        switch (StageNumber)
        {
            case 2:
            {
                // A wave of saucers weaving in from the right.
                _spawnClock = 1.8 + _rng.NextDouble() * 0.9;
                double top = 26, bottom = Stage.HighestGround(wx - 20, wx + 80) - 30;
                double y = top + _rng.NextDouble() * Math.Max(10, bottom - top);
                for (int i = 0; i < 4; i++)
                    Flyers.Add(new Flyer { Kind = FlyerKind.Ufo, Pos = new Vec(Field.Width + 8 + i * 20, y), BaseY = y, Phase = i * 0.8 });
                Play(Sfx.Ufo);
                break;
            }
            case 3:
            {
                // Meteors streaking through the caves.
                _spawnClock = Math.Max(0.6, 1.25 - 0.1 * Lap) + _rng.NextDouble() * 0.5;
                double top = Stage.CeilingAt(wx) + 8, bottom = Stage.GroundAt(wx) - 8;
                double y = top + _rng.NextDouble() * Math.Max(4, bottom - top);
                Flyers.Add(new Flyer { Kind = FlyerKind.Fireball, Pos = new Vec(Field.Width + 6, y), BaseY = y, Speed = 110 + _rng.NextDouble() * 50 });
                break;
            }
            default:
                _spawnClock = 5;
                break;
        }
    }

    private void MoveFlyers(double dt)
    {
        foreach (var f in Flyers)
        {
            f.Age += dt;
            f.Pos = f.Kind == FlyerKind.Ufo
                ? new Vec(f.Pos.X - 72 * dt, f.BaseY + Math.Sin(f.Age * 3 + f.Phase) * 16)
                : f.Pos + new Vec(-f.Speed * dt, 0);
            if (f.Pos.X < -20) f.Dead = true;
        }
        Flyers.RemoveAll(f => f.Dead);
    }

    private void Collide()
    {
        var p = Ship.Pos;
        foreach (double ox in new[] { -Ship.HalfW, 0, Ship.HalfW })
        {
            double wx = p.X + ox + Distance;
            if (p.Y + Ship.HalfH >= Stage.GroundAt(wx))
            {
                Die(Ship.Falling ? "ran out of fuel" : StageNumber == 4 && Stage.GroundAt(wx) < 160 ? "flew into a building" : "crashed into the ground");
                return;
            }
            if (Stage.Ceiling is not null && p.Y - Ship.HalfH <= Stage.CeilingAt(wx))
            {
                Die("hit the roof");
                return;
            }
        }
        foreach (var t in Targets)
            if (!t.Dead && Inside(p, Box(t), Ship.HalfH)) { Die(t.Flying ? "hit by a rocket" : $"flew into a {(t.Kind == GroundKind.Fuel ? "fuel tank" : t.Kind.ToString().ToLowerInvariant())}"); return; }
        foreach (var f in Flyers)
            if ((f.Pos - p).Length < (f.Kind == FlyerKind.Ufo ? 8 : 6)) { Die(f.Kind == FlyerKind.Ufo ? "hit a UFO" : "hit by a meteor"); return; }
    }

    private void Popup(Vec at, string text) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = 1, Text = text });

    // ----- for the tests and screenshots ----------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Tick(default);
    }

    internal void Go()
    {
        if (Phase != Phase.Ready) return;
        SetPhase(Phase.Playing);
        EngineSound(true);
    }

    internal void JumpTo(int stage, double distance = 0)
    {
        StartStage(stage);
        Distance = distance;
        Targets.RemoveAll(t => t.X < distance);
        Ship.Pos = new Vec(ShipX0, SafeY(distance + ShipX0));
        Go();
    }

    /// <summary>Nothing on the ground, no flyers, and no fuel used.</summary>
    internal void Calm()
    {
        _calm = true;
        Targets.Clear();
        Flyers.Clear();
    }

    internal Target PutTarget(GroundKind kind, double screenX)
    {
        double wx = screenX + Distance;
        var t = new Target { Kind = kind, X = wx, Y = Stage.HighestGround(wx - 5, wx + 5) };
        Targets.Add(t);
        return t;
    }

    internal Flyer PutFlyer(FlyerKind kind, Vec at, double speed = 130)
    {
        var f = new Flyer { Kind = kind, Pos = at, BaseY = at.Y, Speed = speed };
        Flyers.Add(f);
        return f;
    }

    internal void PutShip(Vec at) => Ship.Pos = at;

    internal void SetFuel(double fuel) => Fuel = fuel;

    internal void Uncalm() => _calm = false;

    /// <summary>A plain flat course instead of the stage's own, for the tests.</summary>
    internal void FlatGround(int number = 5, double y = 160)
    {
        Stage = new Stage { Number = number, Name = "Flat", Ground = Enumerable.Repeat(y, 700).ToArray() };
        Targets.Clear();
    }
}
