using Avalonia.Input;

namespace AVAScramble.Engine;

/// <summary>
/// Keyboard and gamepad boiled down to the controls: the stick, the laser and the bombs. Of two
/// opposite keys held, the latest wins.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key)
    {
        _held.Remove(key);
        _held.Add(key);
    }

    public void OnKeyUp(Key key) => _held.Remove(key);
    public bool Has(Key key) => _held.Contains(key);

    public void Update() => _pad.Update();

    private int Axis(Key[] minus, Key[] plus)
    {
        for (int i = _held.Count - 1; i >= 0; i--)
        {
            if (minus.Contains(_held[i])) return -1;
            if (plus.Contains(_held[i])) return 1;
        }
        return 0;
    }

    private static readonly Key[] LeftKeys = [Key.Left, Key.A, Key.NumPad4];
    private static readonly Key[] RightKeys = [Key.Right, Key.D, Key.NumPad6];
    private static readonly Key[] UpKeys = [Key.Up, Key.W, Key.NumPad8];
    private static readonly Key[] DownKeys = [Key.Down, Key.S, Key.NumPad2];

    public Controls Controls
    {
        get
        {
            double dx = Axis(LeftKeys, RightKeys), dy = Axis(UpKeys, DownKeys);
            if (dx == 0) dx = _pad.DPadLeft ? -1 : _pad.DPadRight ? 1 : Math.Abs(_pad.LeftStickX) > 0.2f ? _pad.LeftStickX : 0;
            if (dy == 0) dy = _pad.DPadUp ? -1 : _pad.DPadDown ? 1 : Math.Abs(_pad.LeftStickY) > 0.2f ? _pad.LeftStickY : 0;
            bool fire = Has(Key.Z) || Has(Key.Space) || Has(Key.LeftCtrl) || Has(Key.RightCtrl) || _pad.ButtonA || _pad.RightTrigger > 0.4f;
            bool bomb = Has(Key.X) || Has(Key.LeftShift) || Has(Key.RightShift) || Has(Key.LeftAlt) || _pad.ButtonB || _pad.ButtonX || _pad.LeftTrigger > 0.4f;
            return new Controls(dx, dy, fire, bomb);
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.Space) || Has(Key.D1) || _pad.ButtonStart || _pad.ButtonA;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
