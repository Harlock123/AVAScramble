namespace AVAScramble.Models;

/// <summary>A 2D point or vector in game pixels (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vec Normal => Length < 1e-9 ? new Vec(0, 0) : this * (1 / Length);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>
/// The screen in game pixels: 256 across and 176 down, the course scrolling past from right to left.
/// A place on the course is given by how far along its stage it is (world x); it's at the left edge of
/// the screen when the stage has scrolled that far.
/// </summary>
public static class Field
{
    public const int Width = 256;
    public const int Height = 176;
    public const double Step = 1.0 / 60;
    /// <summary>Course pixels a second.</summary>
    public const double ScrollSpeed = 42;
    /// <summary>The course is measured in columns this wide.</summary>
    public const int Column = 4;
    public const int Stages = 6;
}

/// <summary>Something standing on the ground.</summary>
public enum GroundKind
{
    /// <summary>Stands on the ground; in some stages it takes off as the ship comes near.</summary>
    Rocket,
    /// <summary>Hit it for fuel.</summary>
    Fuel,
    /// <summary>A mystery: worth 100, 200 or 300.</summary>
    Mystery,
    /// <summary>The enemy base at the very end.</summary>
    Base,
}

public sealed record Placement(GroundKind Kind, double X);

/// <summary>
/// One stage of the course: the height of the ground (and of the cave roof, where there is one) a
/// column at a time, and what stands on the ground. Worked out from the stage number, so it's the
/// same every time; there's always a way through.
/// </summary>
public sealed class Stage
{
    public required int Number { get; init; }
    public required string Name { get; init; }
    public required double[] Ground { get; init; }
    public double[]? Ceiling { get; init; }
    /// <summary>The city's tower blocks: where each starts and ends, so they're drawn as blocks.</summary>
    public List<(double X0, double X1, double Top)> Buildings { get; } = [];
    public List<Placement> Placements { get; } = [];
    public double Length => (Ground.Length - Field.Width / Field.Column - 2) * Field.Column;

    /// <summary>Rockets take off in these stages; elsewhere they stay put.</summary>
    public bool RocketsFly => Number is 1 or 4 or 6;

    public static readonly string[] Names = ["Mountains", "UFO Valley", "Meteor Caves", "The City", "The Maze", "The Base"];

    /// <summary>The ground's top at a world x (the lowest y the ship can be at is just above it).</summary>
    public double GroundAt(double x) => Sample(Ground, x);
    public double CeilingAt(double x) => Ceiling is null ? 0 : Sample(Ceiling, x);

    private static double Sample(double[] a, double x)
    {
        double f = Math.Clamp(x / Field.Column, 0, a.Length - 1.001);
        int i = (int)f;
        return a[i] + (a[i + 1] - a[i]) * (f - i);
    }

    /// <summary>The highest ground (smallest y) between two world xs.</summary>
    public double HighestGround(double x0, double x1)
    {
        double best = Field.Height;
        for (double x = x0; x <= x1; x += 2) best = Math.Min(best, GroundAt(x));
        return best;
    }

    public double LowestCeiling(double x0, double x1)
    {
        if (Ceiling is null) return 0;
        double best = 0;
        for (double x = x0; x <= x1; x += 2) best = Math.Max(best, CeilingAt(x));
        return best;
    }

    public static Stage Build(int number)
    {
        var rng = new Random(number * 4513 + 7);
        const int len = 1800;
        int cols = len / Field.Column + Field.Width / Field.Column + 4;
        var ground = new double[cols];
        double[]? ceiling = null;
        var stage = (Stage?)null;
        switch (number)
        {
            case 1:
            {
                // Mountains: peaks and valleys with flat shoulders to stand things on.
                double h = 150;
                for (int i = 0; i < cols; i++)
                {
                    double x = i * Field.Column;
                    double target = 148 - 34 * Math.Max(0, Math.Sin(x / 70 + 1)) * (0.6 + 0.4 * Math.Sin(x / 310)) + 8 * Math.Sin(x / 23);
                    h += Math.Clamp(target - h, -3, 3);
                    ground[i] = Math.Clamp(h, 96, 168);
                }
                break;
            }
            case 2:
                for (int i = 0; i < cols; i++)
                {
                    double x = i * Field.Column;
                    ground[i] = 158 - 10 * Math.Sin(x / 90) - 6 * Math.Sin(x / 37 + 2);
                }
                break;
            case 3:
                ceiling = new double[cols];
                for (int i = 0; i < cols; i++)
                {
                    double x = i * Field.Column;
                    ground[i] = 150 - 14 * Math.Sin(x / 60) - 8 * Math.Sin(x / 21);
                    ceiling[i] = 26 + 14 * Math.Sin(x / 75 + 1) + 6 * Math.Abs(Math.Sin(x / 13));
                }
                break;
            case 4:
            {
                // The city: tower blocks on flat ground.
                for (int i = 0; i < cols; i++) ground[i] = 166;
                stage = new Stage { Number = number, Name = Names[number - 1], Ground = ground };
                for (double x = 120; x < len + 200; x += 24 + rng.Next(30))
                {
                    double w = 16 + rng.Next(3) * 8;
                    double top = 108 + rng.Next(7) * 8;
                    stage.Buildings.Add((x, x + w, top));
                    for (int i = (int)(x / Field.Column); i <= (int)((x + w) / Field.Column) && i < cols; i++) ground[i] = top;
                    x += w;
                }
                break;
            }
            case 5:
            {
                // The maze: a tunnel that winds and narrows, roof and floor in steps.
                ceiling = new double[cols];
                double mid = 88, gap = 70;
                for (int i = 0; i < cols; i++)
                {
                    if (i % 12 == 0)
                    {
                        mid = Math.Clamp(mid + rng.Next(-2, 3) * 14, 54, 124);
                        gap = 54 + rng.Next(3) * 8;
                    }
                    ceiling[i] = mid - gap / 2;
                    ground[i] = mid + gap / 2;
                }
                // Ease the steps so they're slopes, not cliffs.
                for (int pass = 0; pass < 3; pass++)
                    for (int i = 1; i < cols - 1; i++)
                    {
                        ground[i] = (ground[i - 1] + ground[i] * 2 + ground[i + 1]) / 4;
                        ceiling[i] = (ceiling[i - 1] + ceiling[i] * 2 + ceiling[i + 1]) / 4;
                    }
                break;
            }
            default:
                for (int i = 0; i < cols; i++)
                {
                    double x = i * Field.Column;
                    ground[i] = x > len - 60 ? 150 : 152 - 12 * Math.Abs(Math.Sin(x / 48));
                }
                break;
        }
        stage ??= new Stage { Number = number, Name = Names[number - 1], Ground = ground, Ceiling = ceiling };

        // Things on the ground, on fairly level spots.
        double gapX = number == 5 ? 46 : 38;
        for (double x = 140; x < len - 80; x += gapX + rng.NextDouble() * 34)
        {
            double a = stage.GroundAt(x - 4), b = stage.GroundAt(x + 4);
            if (Math.Abs(a - b) > 6) continue;
            if (number == 4 && stage.Buildings.Any(bl => x + 5 > bl.X0 && x - 5 < bl.X1 && (x - 5 < bl.X0 || x + 5 > bl.X1))) continue;
            double roll = rng.NextDouble();
            var kind = roll < 0.38 ? GroundKind.Fuel : roll < 0.48 ? GroundKind.Mystery : GroundKind.Rocket;
            stage.Placements.Add(new Placement(kind, x));
        }
        if (number == 6) stage.Placements.Add(new Placement(GroundKind.Base, len - 20));
        return stage;
    }
}
