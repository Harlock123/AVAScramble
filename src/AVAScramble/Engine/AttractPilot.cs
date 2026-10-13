using AVAScramble.Models;

namespace AVAScramble.Engine;

/// <summary>
/// The computer player for attract mode and the tests. It flies the corridor the course leaves, looking
/// ahead: low over open ground, so as to bomb what's there, and down the middle of caves and tunnels.
/// It works out where a bomb dropped now would land, allowing for the course scrolling under it, and
/// drops one when that's on a fuel tank, a rocket or a mystery (fuel first when it's running low). It
/// keeps the laser going and dodges up or down out of the way of anything coming at it.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private bool _bombHeld;
    private double _lowOffset = 34;

    public Controls Decide(GameEngine g)
    {
        if (g.Phase != Phase.Playing || g.Ship.Falling) return default;
        var s = g.Stage;
        var ship = g.Ship.Pos;
        double wx = ship.X + g.Distance;
        if (_rng.NextDouble() < 0.01) _lowOffset = 28 + _rng.NextDouble() * 14;

        // The way ahead: near (must clear now) and further on (get ready for).
        double nearGround = s.HighestGround(wx - 10, wx + 24), farGround = s.HighestGround(wx - 10, wx + 80);
        double nearRoof = s.LowestCeiling(wx - 10, wx + 24), farRoof = s.LowestCeiling(wx - 10, wx + 80);
        double ty;
        if (s.Ceiling is not null)
        {
            // In a cave or a tunnel: the middle of the way a little ahead, kept clear of the rock close by.
            const double lead = 26;
            double centre = (s.CeilingAt(wx + lead) + s.GroundAt(wx + lead)) / 2;
            double lo = s.LowestCeiling(wx - 9, wx + 12) + 7, hi = s.HighestGround(wx - 9, wx + 12) - 7;
            ty = lo <= hi ? Math.Clamp(centre, lo, hi) : (lo + hi) / 2;
        }
        else
        {
            // Over open ground: low enough to bomb, rising in good time for what's ahead.
            ty = Math.Min(farGround - _lowOffset, nearGround - 14);
            ty = Math.Max(ty, 14);
        }
        double tx = 52;

        // Out of the way of what's coming: up or down, whichever has more room.
        foreach (var (pos, vel, r) in Threats(g))
            foreach (double t in new[] { 0.0, 0.25, 0.5, 0.8 })
            {
                var at = pos + vel * t;
                if (Math.Abs(at.X - ship.X) > 16 + r || Math.Abs(at.Y - ty) > 12 + r) continue;
                double up = at.Y - r - 14, down = at.Y + r + 14;
                bool upOk = up > nearRoof + 8;
                bool downOk = down < nearGround - 10;
                ty = upOk && (!downOk || Math.Abs(up - ship.Y) < Math.Abs(down - ship.Y)) ? up : downOk ? down : ty;
                if (!upOk && !downOk) tx = Math.Max(14, ship.X - 30);
            }

        // A bomb now: where would it come down, in course terms?
        bool bomb = false;
        if (g.Bombs.Count < GameEngine.MaxBombs)
        {
            foreach (var t in g.Targets)
            {
                if (t.Dead || t.Flying || g.ScreenX(t.X) < ship.X) continue;
                double top = t.Y - 8;
                double c = ship.Y + 4 - top;
                if (c >= 0) continue;
                double tLand = (-GameEngine.BombVy + Math.Sqrt(GameEngine.BombVy * GameEngine.BombVy - 4 * (GameEngine.Gravity / 2) * c)) / GameEngine.Gravity;
                double landX = wx + 2 + (GameEngine.BombVx + Field.ScrollSpeed) * tLand;
                if (Math.Abs(landX - t.X) < 5 && (t.Kind == GameEngine.FuelKind || g.Fuel > 30)) { bomb = true; break; }
            }
        }
        _bombHeld = bomb && !_bombHeld;
        double dx = (tx - ship.X) / 12, dy = (ty - ship.Y) / 8;
        return new Controls(Math.Clamp(dx, -1, 1), Math.Clamp(dy, -1, 1), Fire: true, Bomb: _bombHeld);
    }

    private static IEnumerable<(Vec Pos, Vec Vel, double R)> Threats(GameEngine g)
    {
        foreach (var f in g.Flyers)
            yield return (f.Pos, f.Kind == FlyerKind.Ufo ? new Vec(-72, 0) : new Vec(-f.Speed, 0), f.Kind == FlyerKind.Ufo ? 9 : 6);
        foreach (var t in g.Targets)
            if (t.Flying) yield return (new Vec(g.ScreenX(t.X), t.Y - 6), new Vec(-Field.ScrollSpeed, t.Vy), 8);
    }
}
