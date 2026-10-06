namespace ControllerWheel;

/// <summary>Full controller input snapshot. Sticks normalised [-1,+1] in the SAME sense as
/// ControllerReader.Axis() (Y down-positive, like StickUpdate); triggers 0..255.</summary>
public struct ControllerState
{
    public float LeftStickX, LeftStickY, RightStickX, RightStickY;
    public byte  LeftTrigger, RightTrigger;          // L2 / R2 analog
    public bool  Cross, Circle, Square, Triangle;
    public bool  L1, R1, L3, R3;
    public bool  Create, Options, Ps;                // Share/Create · Options · PS(Guide)
    public bool  DpadUp, DpadDown, DpadLeft, DpadRight;
}
