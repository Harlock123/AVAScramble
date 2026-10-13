using AVAScramble.Models;

namespace AVAScramble.Engine;

/// <summary>The player's jet, in screen terms.</summary>
public sealed class Ship
{
    public Vec Pos { get; set; }
    /// <summary>Out of fuel: it drops.</summary>
    public bool Falling { get; set; }
    public const double HalfW = 7;
    public const double HalfH = 3.5;
}

/// <summary>A laser shot, straight ahead.</summary>
public sealed class Shot
{
    public Vec Pos { get; set; }
    public bool Dead { get; set; }
}

/// <summary>A bomb: thrown forward, then falling.</summary>
public sealed class Bomb
{
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public bool Dead { get; set; }
}

/// <summary>Something on the ground (in world terms): a rocket, a fuel tank, a mystery, or the base.</summary>
public sealed class Target
{
    public required GroundKind Kind { get; init; }
    /// <summary>World x, and the y of its base on the ground.</summary>
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>A rocket in flight: going up from where it stood.</summary>
    public bool Flying { get; set; }
    public double Vy { get; set; }
    public bool Dead { get; set; }
}

public enum FlyerKind
{
    /// <summary>Saucers that weave across in waves.</summary>
    Ufo,
    /// <summary>Meteors streaking through the caves: they can't be destroyed.</summary>
    Fireball,
}

/// <summary>A flyer, in screen terms.</summary>
public sealed class Flyer
{
    public required FlyerKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double BaseY { get; init; }
    public double Phase { get; init; }
    public double Speed { get; init; }
    public bool Dead { get; set; }
}

public enum EffectKind { Score, Blast, BigBlast }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    /// <summary>Screen position, or for things on the ground, world x (they scroll with it).</summary>
    public Vec Pos { get; set; }
    public bool OnGround { get; init; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: the stick (each axis −1 to 1), the laser and the bomb.</summary>
public record struct Controls(double Dx, double Dy, bool Fire = false, bool Bomb = false);
