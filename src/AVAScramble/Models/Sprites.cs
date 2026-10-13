namespace AVAScramble.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. The jet and the rocket are drawn row by row; the
/// saucer is painted from ellipses into a pixel grid. Each letter is a colour (see <see cref="Colours"/>); '.' is clear.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A pixel grid to paint shapes into.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly char[,] _px = Fill(width, height);

        private static char[,] Fill(int w, int h)
        {
            var g = new char[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) g[x, y] = '.';
            return g;
        }

        public Canvas Ellipse(double cx, double cy, double rx, double ry, char c)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) _px[x, y] = c;
                }
            return this;
        }

        public Canvas Box(int x0, int y0, int x1, int y1, char c)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++) _px[x, y] = c;
            return this;
        }

        public Canvas Dot(int x, int y, char c) => Box(x, y, x, y, c);

        public Sprite Done()
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = _px[x, y];
                rows[y] = new string(chars);
            }
            return new Sprite(rows);
        }
    }

    public static readonly IReadOnlyDictionary<char, uint> Colours = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F4F8, // white
        ['m'] = 0xFFA8ACB8, // silver
        ['d'] = 0xFF5C6070, // dark grey
        ['r'] = 0xFFE83A3A, // red
        ['R'] = 0xFF9A1E1E, // dark red
        ['o'] = 0xFFFF8A1E, // orange
        ['y'] = 0xFFFFE14A, // yellow
        ['b'] = 0xFF3A7AE8, // blue
        ['c'] = 0xFF4FD7FF, // cyan
        ['g'] = 0xFF3CD84A, // green
        ['p'] = 0xFFE84AC0, // pink
    };

    /// <summary>The jet, facing right.</summary>
    public static readonly Sprite Ship = S(
        "...ww...........",
        "...wmw..........",
        "...wmmw.........",
        "..wwmmmwwwwww...",
        "rwbbmmmmmmmccww.",
        "rwbbmmmmmmmmmmmw",
        "..wwmmmmmwwwww..",
        "...wmmw.........",
        "...ww...........");

    /// <summary>A rocket standing on its pad (pointing up).</summary>
    public static readonly Sprite Rocket = S(
        "..w..",
        ".wrw.",
        ".wmw.",
        ".wmw.",
        ".wmw.",
        ".wbw.",
        ".wmw.",
        ".wmw.",
        "rwmwr",
        "rwmwr",
        "r.d.r",
        "..d..",
        ".ddd.");

    /// <summary>A saucer, two frames.</summary>
    public static readonly Sprite[] Ufo =
    [
        new Canvas(14, 8).Ellipse(7, 5, 6.8, 2.6, 'm').Ellipse(7, 3, 3.2, 2.4, 'c').Dot(3, 5, 'y').Dot(7, 5, 'r').Dot(11, 5, 'y').Done(),
        new Canvas(14, 8).Ellipse(7, 5, 6.8, 2.6, 'm').Ellipse(7, 3, 3.2, 2.4, 'c').Dot(3, 5, 'r').Dot(7, 5, 'y').Dot(11, 5, 'r').Done(),
    ];
}
