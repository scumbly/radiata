namespace ControllerWheel;

/// <summary>How a controller is physically attached. Only the raw-HID backend can answer; XInput reports
/// <see cref="Unknown"/>.</summary>
public enum ControllerTransport { Unknown, Usb, Bluetooth }

/// <summary>
/// Abstraction over a physical controller the overlay listens to. The two <c>Trigger</c> inputs
/// summon the wheels; the stick/d-pad/face-buttons drive selection. Which physical buttons count as the
/// triggers — and the raw HID layout — are NOT part of this contract: the implementation
/// (<c>ControllerReader</c>) resolves the gesture configured for the connected controller kind
/// (Core/TriggerConfig.cs + TriggerInterpreter) and raises the two Trigger events. More implementations
/// go behind this seam — see docs/CONTROLLERS.md.
/// </summary>
public interface IController : IDisposable
{
    /// <summary>Human-readable device + trigger summary, e.g. "DualSense Edge — rear Fn buttons".</summary>
    string TriggerDescription { get; }

    /// <summary>How the open pad is attached, for user-facing readouts. <see cref="ControllerTransport.Unknown"/>
    /// when nothing is open, or when the backend genuinely can't tell (XInput doesn't expose the transport).</summary>
    ControllerTransport Transport { get; }

    /// <summary>True when the controller is found, false when lost.</summary>
    event Action<bool>? Connected;

    /// <summary>The two wheel-summon triggers (left/right). Pressed = true, released = false.</summary>
    event Action<bool>? TriggerLeftChanged;
    event Action<bool>? TriggerRightChanged;

    /// <summary>Normalised stick axes in [-1, +1]: lx, ly, rx, ry.</summary>
    event Action<float, float, float, float>? StickUpdate;

    /// <summary>Battery level (0–100%) and whether it's charging. Fires only when the value changes.</summary>
    event Action<int, bool>? BatteryChanged;

    /// <summary>D-pad hat nibble: 0=Up,1=UpRight,2=Right,3=DownRight,4=Down,5=DownLeft,6=Left,7=UpLeft,8=Neutral.</summary>
    event Action<int>? DPadChanged;

    event Action<bool>? CrossChanged;
    event Action<bool>? CircleChanged;
    event Action<bool>? TriangleChanged;
    event Action<bool>? SquareChanged;

    /// <summary>Shoulder buttons (L1/R1) — cycle the game-browser launcher filter; undo (L1) / redo (R1)
    /// in edit mode.</summary>
    event Action<bool>? L1Changed;
    event Action<bool>? R1Changed;

    /// <summary>Stick-clicks (L3/R3). Held together with a wheel's Fn, these enter in-wheel edit mode
    /// (the aiming stick is the opposite hand from the Fn). Universal across gamepads.</summary>
    event Action<bool>? L3Changed;
    event Action<bool>? R3Changed;

    /// <summary>Analog triggers (L2/R2) as digital press/release, thresholded with hysteresis. Used as
    /// chord trigger inputs.</summary>
    event Action<bool>? L2Changed;
    event Action<bool>? R2Changed;

    /// <summary>PS/Guide ("Home"), and the Create/Share + Options/Menu buttons ("View"/"Menu"). Chord
    /// modifiers for the alternate trigger modes.</summary>
    event Action<bool>? PsChanged;
    event Action<bool>? CreateChanged;
    event Action<bool>? OptionsChanged;

    /// <summary>Touchpad finger 0: x, y normalised to [0,1] and whether a finger is in contact. Raw
    /// stream; gestures (edge-swipe) are interpreted upstream. Fires only when contact or position changes.</summary>
    event Action<float, float, bool>? TouchpadChanged;

    /// <summary>Touchpad finger 1 (second touch point) — same shape as <see cref="TouchpadChanged"/>.
    /// Used to detect two-finger gestures (both-edge swipe).</summary>
    event Action<float, float, bool>? Touchpad2Changed;

    /// <summary>Begin reading the device.</summary>
    void Start();
}
