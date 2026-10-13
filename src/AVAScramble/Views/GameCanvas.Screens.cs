using Avalonia;
using Avalonia.Media;
using AVAScramble.Engine;
using AVAScramble.Models;

namespace AVAScramble.Views;

public sealed partial class GameCanvas
{
    /// <summary>
    /// Along the top: the score, the best score and spare ships; the six stages, with this one lit;
    /// and the fuel gauge.
    /// </summary>
    private void DrawHud(DrawingContext dc)
    {
        var g = Engine;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;
        Text(dc, "1UP", new Point(20, 14), 26, Orange);
        Text(dc, (playing ? g.Score : 0).ToString(), new Point(70, 14), 30, White);
        Text(dc, "HI-SCORE", new Point(ScreenWidth / 2 - 10, 14), 26, Orange, right: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(ScreenWidth / 2 + 4, 14), 30, White);
        if (playing)
            for (int i = 0; i < Math.Min(g.Lives - (g.Phase == Phase.Dying ? 0 : 1), 6); i++)
                DrawPanelSprite(dc, Sprites.Ship, ScreenWidth - 60 - i * 40, 6);

        // The stages.
        string[] labels = ["1ST", "2ND", "3RD", "4TH", "5TH", "BASE"];
        for (int i = 0; i < labels.Length; i++)
        {
            var box = new Rect(20 + i * 62, 34, 58, 22);
            bool now = playing && i + 1 == g.StageNumber, done = playing && i + 1 < g.StageNumber;
            dc.FillRectangle(Brush(now ? 0xFFFF8A1Eu : done ? 0xFF5A3A1Au : 0xFF1A1A2Au), box);
            dc.DrawRectangle(null, Pen(0xFF8A8AA6, 1), box);
            Text(dc, labels[i], box.Center, 22, now ? Brush(0xFF000006) : Dim, center: true);
        }
        // How far through this stage.
        if (playing)
        {
            double frac = Math.Clamp(g.Distance / g.Stage.Length, 0, 1);
            dc.FillRectangle(Yellow, new Rect(20 + (g.StageNumber - 1) * 62, 57, 58 * frac, 3));
        }

        // The fuel.
        Text(dc, "FUEL", new Point(430, 45), 26, Orange);
        var gauge = new Rect(480, 36, 230, 18);
        dc.DrawRectangle(null, Pen(0xFF8A8AA6, 1.5), gauge);
        double fuel = playing ? g.Fuel / GameEngine.FuelMax : 1;
        uint colour = fuel < 0.25 ? ((int)(_clock * 4) % 2 == 0 ? 0xFFFF3D3Du : 0xFF9A1E1Eu) : fuel < 0.5 ? 0xFFFFE14Au : 0xFF3CD84Au;
        dc.FillRectangle(Brush(colour), new Rect(gauge.X + 2, gauge.Y + 2, (gauge.Width - 4) * fuel, gauge.Height - 4));
        Text(dc, "ARROWS  Z FIRE  X BOMB  P  M", new Point(ScreenWidth - 20, 45), 20, Dim, right: true);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double left, double top)
    {
        const double px = 2;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(left + c * px, top + r * px, px + 0.2, px + 0.2));
            }
    }

    private static void Panel(DrawingContext dc, double top, double bottom) =>
        dc.FillRectangle(Brush(0xD0000008), new Rect(P(24, top), P(Field.Width - 24, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 6, 170);
        Text(dc, "AVA SCRAMBLE", P(cx, 22), 100, Orange, center: true);
        Text(dc, "AFTER THE 1981 ARCADE CLASSIC", P(cx, 37), 28, Dim, center: true);
        Text(dc, "FLY THE COURSE TO THE ENEMY BASE AND DESTROY IT.", P(cx, 48), 28, White, center: true);
        Text(dc, "YOUR FUEL RUNS DOWN: HIT THE FUEL TANKS TO KEEP GOING.", P(cx, 56), 28, Yellow, center: true);

        double y = 72;
        void Row(Action<double, double> icon, string text, string points)
        {
            // The icons smaller than life, so the base fits its row.
            var c = P(66, y);
            using (dc.PushTransform(Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateScale(0.62, 0.62) * Matrix.CreateTranslation(c.X, c.Y)))
                icon(66, y);
            Text(dc, text, P(82, y), 26, White);
            Text(dc, points, P(196, y), 26, Yellow, right: true);
            y += 12;
        }
        Row((x, yy) => DrawTarget(dc, new Target { Kind = GroundKind.Rocket, X = 0, Y = yy + 6 }, x), "ROCKET (ON THE GROUND / FLYING)", "50 / 80");
        Row((x, yy) => DrawTarget(dc, new Target { Kind = GroundKind.Fuel, X = 0, Y = yy + 5 }, x), "FUEL TANK: MORE FUEL", "150");
        Row((x, yy) => DrawTarget(dc, new Target { Kind = GroundKind.Mystery, X = 0, Y = yy + 4 }, x), "MYSTERY", "100-300");
        Row((x, yy) => DrawSprite(dc, Sprites.Ufo[(int)(_clock * 8) % 2], x - 7, yy - 4), "UFO", "100");
        Row((x, yy) =>
        {
            for (int i = 4; i >= 0; i--)
                dc.DrawEllipse(Brush(i == 0 ? 0xFFFFFFFFu : 0xFFFF8A1Eu), null, P(x - 2 + i * 3, yy), (3 - i * 0.4) * Scale, (3 - i * 0.4) * Scale);
        }, "METEOR: CAN'T BE SHOT", "");
        Row((x, yy) => DrawTarget(dc, new Target { Kind = GroundKind.Base, X = 0, Y = yy + 13 }, x), "THE BASE", "800");
        Text(dc, "10 POINTS FOR EVERY SECOND IN THE AIR", P(cx, 146), 26, Cyan, center: true);
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 158), 32, Yellow, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 167), 22, Dim, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 6, 168);
        Text(dc, "TOP PILOTS", P(cx, 20), 64, Orange, center: true);
        Text(dc, "RANK    SCORE   NAME  STAGE", P(cx, 34), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Orange : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}   {e.Stage,3}", P(cx, 46 + i * 10.5), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 160), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(48, 40), P(208, 130)));
        double cx = Field.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 52), 48, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 66), 42, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 16 + i * 16, 92);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 80, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4) dc.FillRectangle(Yellow, new Rect(at.X - 18, at.Y + 36, 36, 6));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 122), 24, Dim, center: true);
    }
}
