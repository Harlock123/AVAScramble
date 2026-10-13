using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AVAScramble.Engine;
using AVAScramble.Models;

namespace AVAScramble.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), IPen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAScramble/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static IPen Pen(uint argb, double thickness)
    {
        if (!PenCache.TryGetValue((argb, thickness), out var p))
        {
            p = new ImmutablePen(argb, thickness);
            PenCache[(argb, thickness)] = p;
        }
        return p;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Orange = Brush(0xFFFF8A1E);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);

    /// <summary>Each stage's rock: fill and the bright edge along it.</summary>
    private static readonly (uint Fill, uint Edge)[] Rock =
    [
        (0xFF7A3A16, 0xFFFF8A1E), (0xFF1E5A2A, 0xFF3CD84A), (0xFF4A1E6A, 0xFFB050F0),
        (0xFF1E3A7A, 0xFF4FD7FF), (0xFF6A1E2A, 0xFFE83A3A), (0xFF5A5A1E, 0xFFFFE14A),
    ];

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private static void Polygon(DrawingContext dc, uint fill, IPen? edge, IReadOnlyList<Point> pts, bool closed = true)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], closed);
            for (int i = 1; i < pts.Count; i++) ctx.LineTo(pts[i]);
            ctx.EndFigure(closed);
        }
        dc.DrawGeometry(closed ? Brush(fill) : null, edge, geo);
    }

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0xFF000006), new Rect(0, 0, ScreenWidth, ScreenHeight));
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            DrawStars(dc, g.Distance);
            DrawCourse(dc, g.Stage, g.Distance);
            switch (g.State)
            {
                case GameState.Title: DrawTitle(dc); break;
                case GameState.HighScores: DrawHighScores(dc); break;
                default: DrawPlay(dc); break;
            }
        }
        DrawHud(dc);
    }

    // ----- the course -----------------------------------------------------------------------------

    /// <summary>Stars drifting by slower than the course, twinkling.</summary>
    private void DrawStars(DrawingContext dc, double distance)
    {
        for (int i = 0; i < 60; i++)
        {
            double x = ((i * 97.3 - distance * 0.3) % Field.Width + Field.Width) % Field.Width;
            double y = (i * 53.7) % (Field.Height - 20);
            if ((i + (int)(_clock * 3)) % 7 == 0) continue;
            uint c = (i % 3) switch { 0 => 0xFFFFFFFFu, 1 => 0xFF8AB4FFu, _ => 0xFFFFE14Au };
            Fill(dc, c, x, y, 0.7, 0.7);
        }
    }

    private static void DrawCourse(DrawingContext dc, Stage s, double distance)
    {
        var (fill, edgeColour) = Rock[(s.Number - 1) % Rock.Length];
        var edge = Pen(edgeColour, 2.5);
        var ground = new List<Point>();
        for (double x = -4; x <= Field.Width + 4; x += 2) ground.Add(P(x, s.GroundAt(x + distance)));
        var poly = new List<Point>(ground) { P(Field.Width + 4, Field.Height + 4), P(-4, Field.Height + 4) };
        Polygon(dc, fill, null, poly);
        Polygon(dc, 0, edge, ground, closed: false);
        if (s.Ceiling is not null)
        {
            var roof = new List<Point>();
            for (double x = -4; x <= Field.Width + 4; x += 2) roof.Add(P(x, s.CeilingAt(x + distance)));
            var rp = new List<Point>(roof) { P(Field.Width + 4, -4), P(-4, -4) };
            Polygon(dc, fill, null, rp);
            Polygon(dc, 0, edge, roof, closed: false);
        }
        // Lit windows in the city's towers.
        foreach (var (x0, x1, top) in s.Buildings)
        {
            double sx0 = x0 - distance, sx1 = x1 - distance;
            if (sx1 < -4 || sx0 > Field.Width + 4) continue;
            for (double wy = top + 5; wy < 162; wy += 7)
                for (double wx = sx0 + 3; wx < sx1 - 3; wx += 6)
                    if (Terrain((int)(wx + distance), (int)wy)) Fill(dc, 0xFFFFE14A, wx, wy, 2, 3);
        }
    }

    private static bool Terrain(int a, int b) => ((a * 73856093) ^ (b * 19349663)) % 5 != 0;

    // ----- things on the ground, flyers, the ship ---------------------------------------------------

    private void DrawTarget(DrawingContext dc, Target t, double sx)
    {
        switch (t.Kind)
        {
            case GroundKind.Rocket:
                DrawSprite(dc, Sprites.Rocket, sx - 2.5, t.Y - 13);
                if (t.Flying)
                {
                    double f = 3 + (int)(_clock * 20) % 2 * 2;
                    Fill(dc, 0xFFFF8A1E, sx - 1, t.Y, 2, f);
                    Fill(dc, 0xFFFFE14A, sx - 0.5, t.Y, 1, f - 1);
                }
                break;
            case GroundKind.Fuel:
            {
                // A fuel tank: a drum with FUEL on it.
                var r = new Rect(P(sx - 5, t.Y - 11), P(sx + 5, t.Y));
                dc.DrawRectangle(Brush(0xFFE84AC0), null, r, 2 * Scale, 2 * Scale);
                dc.FillRectangle(Brush(0xFF821A66), new Rect(r.X, r.Bottom - 2 * Scale, r.Width, 2 * Scale));
                Text(dc, "FUEL", new Point(r.Center.X, r.Center.Y - 2), 20, White, center: true);
                break;
            }
            case GroundKind.Mystery:
            {
                var r = new Rect(P(sx - 5, t.Y - 9), P(sx + 5, t.Y));
                dc.FillRectangle(Brush(0xFF3A62E0), r);
                dc.DrawRectangle(null, Pen(0xFF9AB4FF, 2), r);
                Text(dc, "?", new Point(r.Center.X, r.Center.Y - 1), 30, (int)(_clock * 4) % 2 == 0 ? Yellow : White, center: true);
                break;
            }
            case GroundKind.Base:
            {
                // The base: a dome on a block, a mast and a flag.
                Fill(dc, 0xFF8A8E9A, sx - 14, t.Y - 10, 28, 10);
                Fill(dc, 0xFFC8CCD8, sx - 14, t.Y - 10, 28, 1.5);
                dc.DrawEllipse(Brush(0xFFE83A3A), null, P(sx, t.Y - 10), 9 * Scale, 7 * Scale);
                Fill(dc, 0xFF000006, sx - 14, t.Y - 10, 28, 0.01);
                Fill(dc, 0xFFC8CCD8, sx - 0.5, t.Y - 26, 1, 10);
                Fill(dc, (int)(_clock * 3) % 2 == 0 ? 0xFFFFE14Au : 0xFFFF8A1Eu, sx + 0.5, t.Y - 26, 6, 4);
                break;
            }
        }
    }

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        foreach (var t in g.Targets)
        {
            if (t.Dead) continue;
            double sx = g.ScreenX(t.X);
            if (sx < -20 || sx > Field.Width + 20) continue;
            DrawTarget(dc, t, sx);
        }
        foreach (var f in g.Flyers)
        {
            if (f.Kind == FlyerKind.Ufo) DrawSprite(dc, Sprites.Ufo[(int)(f.Age * 8) % 2], f.Pos.X - 7, f.Pos.Y - 4);
            else
            {
                // A meteor and its tail.
                for (int i = 6; i >= 0; i--)
                {
                    uint c = i == 0 ? 0xFFFFFFFFu : i < 3 ? 0xFFFFE14Au : 0xFFFF6A1Au;
                    double r = 3.2 - i * 0.35;
                    dc.DrawEllipse(Brush(c), null, P(f.Pos.X + i * 3, f.Pos.Y + Math.Sin(f.Age * 20 + i) * 0.6), r * Scale, r * Scale);
                }
            }
        }
        foreach (var s in g.Shots) Fill(dc, (int)(_clock * 20) % 2 == 0 ? 0xFFFFE14Au : 0xFFFFFFFFu, s.Pos.X - 4, s.Pos.Y - 0.5, 6, 1);
        foreach (var b in g.Bombs)
        {
            dc.DrawEllipse(Brush(0xFFC8CCD8), null, P(b.Pos), 1.6 * Scale, 1.6 * Scale);
            dc.DrawEllipse(Brush(0xFF5A5E6A), null, P(b.Pos) + new Point(1.5, 1.5), 0.8 * Scale, 0.8 * Scale);
        }

        if (g.Phase is not (Phase.Dying or Phase.Mission))
        {
            var p = g.Ship.Pos;
            DrawSprite(dc, Sprites.Ship, p.X - 8, p.Y - 4.5);
            // The exhaust flickers (sputters when the fuel's out).
            if (!g.Ship.Falling || (int)(_clock * 8) % 3 == 0)
            {
                double len = 3 + (int)(_clock * 24) % 3;
                Fill(dc, 0xFFFF8A1E, p.X - 8 - len, p.Y - 0.5, len, 1.5);
                Fill(dc, 0xFFFFE14A, p.X - 8 - len * 0.5, p.Y, len * 0.5, 0.8);
            }
        }

        foreach (var fx in g.Effects)
        {
            var at = fx.OnGround ? fx.Pos with { X = g.ScreenX(fx.Pos.X) } : fx.Pos;
            double t = fx.Age / fx.Life;
            switch (fx.Kind)
            {
                case EffectKind.Blast or EffectKind.BigBlast:
                {
                    double big = fx.Kind == EffectKind.BigBlast ? 2.6 : 1;
                    for (int i = 0; i < 10; i++)
                    {
                        double a = i * Math.PI / 5 + t;
                        double r0 = (1.5 + t * 8) * big, r1 = (4 + t * 15) * big;
                        uint c = ((i + (int)(_clock * 20)) % 3) switch { 0 => 0xFFFFFFFFu, 1 => 0xFFFFE14Au, _ => 0xFFFF6A1Au };
                        dc.DrawLine(Pen(c, 2.5), P(at.X + Math.Cos(a) * r0, at.Y + Math.Sin(a) * r0), P(at.X + Math.Cos(a) * r1, at.Y + Math.Sin(a) * r1));
                    }
                    break;
                }
                case EffectKind.Score:
                    Text(dc, fx.Text, P(at.X, at.Y - 6 - fx.Age * 8), 28, Yellow, center: true);
                    break;
            }
        }

        switch (g.State)
        {
            case GameState.Paused: Banner(dc, "PAUSED", "ESC TO QUIT, ANY OTHER KEY TO RESUME"); break;
            case GameState.GameOver: Banner(dc, "GAME  OVER", null); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
            default:
                if (g.Phase == Phase.Ready)
                    Banner(dc, $"{Ordinal(g.StageNumber)} STAGE: {g.Stage.Name.ToUpperInvariant()}", g.State == GameState.Attract ? "DEMONSTRATION" : "READY");
                else if (g.Phase == Phase.Mission)
                    Banner(dc, "MISSION COMPLETE!", "THE BASE IS DESTROYED.  ROUND AGAIN, FASTER");
                else if (g.State == GameState.Attract && (int)(_clock * 2) % 2 == 0)
                    Text(dc, "DEMONSTRATION", P(Field.Width / 2.0, 12), 34, Red, center: true);
                else if (g.Fuel < 25 && g.Phase == Phase.Playing && (int)(_clock * 4) % 2 == 0)
                    Text(dc, "LOW FUEL!", P(Field.Width / 2.0, 12), 34, Red, center: true);
                break;
        }
    }

    public static string Ordinal(int n) => n switch { 1 => "1ST", 2 => "2ND", 3 => "3RD", 6 => "LAST", _ => $"{n}TH" };

    private void Banner(DrawingContext dc, string title, string? sub)
    {
        Fill(dc, 0xC8000000, 20, 64, Field.Width - 40, sub is null ? 22 : 32);
        Text(dc, title, P(Field.Width / 2.0, 75), 52, Yellow, center: true);
        if (sub is not null) Text(dc, sub, P(Field.Width / 2.0, 89), 28, White, center: true);
    }

    /// <summary>Draws a sprite with its top-left at a game point.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, double px = 1)
    {
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[c];
                if (ch == '.') continue;
                var a = P(x + c * px, y + r * px);
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
