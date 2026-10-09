namespace ControllerWheel;

/// <summary>The write-back guard for Settings controls that mirror <see cref="SystemConfig"/> fields something
/// outside Settings can also write (the tray, the open wheel, the crash window, the setup wizard).
/// Settings saves ~2 s after an edit by layering every editor's <c>ApplyTo</c> onto the live config, so a
/// control that wrote its field unconditionally would overwrite a newer outside value with the stale one it
/// is still showing.
/// <para>Each registered field keeps two snapshots taken when the control was last in step with the config
/// (<see cref="Capture"/> after a <c>Load</c>, <see cref="NoteSaved"/> after a save): what the control
/// reported, and what the config held. <see cref="ApplyTo"/> writes a field only when its control now
/// reports something else than it did then; an untouched control leaves the config's value alone, whatever
/// that value became in the meantime. When the user and an outside writer both changed a field before the
/// save, the user's value wins. <see cref="ChangedOutside"/> answers the other half: whether the config's
/// value moved away from what the control was last in step with, which <c>ApplyTo</c> alone can never
/// report, so the host's hot-reload staleness test must ask it and then re-<c>Load</c> the control.</para>
/// <para>Comparison is by value: a field holding a collection supplies a comparer and a copy function, so
/// an in-place edit of the shared list is still seen as a change. A field that is one decision across
/// several config keys (thickness and its auto-demotion flag, the sound switch and its theme) registers once
/// with a tuple, so the keys are written together or not at all.</para>
/// <para>One instance per editor; a control that has not been captured yet writes nothing.</para></summary>
public sealed class MirroredFields
{
    private readonly List<IField> _fields = new();

    /// <summary>Register one mirrored field. <paramref name="read"/> is the config's current value of it,
    /// <paramref name="control"/> what the control would write now, <paramref name="write"/> folds a control
    /// value into a config. Config values are only ever compared with config values and control values with
    /// control values, so a control that normalizes what it loads never reads as changed.
    /// <paramref name="comparer"/> and <paramref name="copy"/> are for reference-typed values (see
    /// <see cref="StringSequence"/> and <see cref="CopyStrings"/>).</summary>
    public MirroredFields Add<T>(string name, Func<SystemConfig, T> read, Func<T> control,
                                 Func<SystemConfig, T, SystemConfig> write,
                                 IEqualityComparer<T>? comparer = null, Func<T, T>? copy = null)
    {
        if (_fields.Any(f => f.Name == name)) throw new ArgumentException($"Field '{name}' is already registered.");
        _fields.Add(new Field<T>(name, read, control, write, comparer ?? EqualityComparer<T>.Default,
                                 copy ?? (v => v)));
        return this;
    }

    /// <summary>Every control has just been loaded from <paramref name="cfg"/>: snapshot all fields.</summary>
    public void Capture(SystemConfig cfg)
    {
        foreach (var f in _fields) f.Capture(cfg);
    }

    /// <summary>One field's control was just set from <paramref name="cfg"/> outside a full load (a partial
    /// refresh): re-snapshot only that field.</summary>
    public void Rebase(string name, SystemConfig cfg)
    {
        foreach (var f in _fields)
            if (f.Name == name) f.Capture(cfg);
    }

    /// <summary>Fold the controls the user moved since the last snapshot into <paramref name="cfg"/>; every
    /// other field keeps <paramref name="cfg"/>'s own value.</summary>
    public SystemConfig ApplyTo(SystemConfig cfg)
    {
        foreach (var f in _fields) cfg = f.Apply(cfg);
        return cfg;
    }

    /// <summary>True when a mirrored field of <paramref name="cfg"/> differs from the value its control was
    /// last in step with, i.e. something other than this editor wrote it.</summary>
    public bool ChangedOutside(SystemConfig cfg) => _fields.Any(f => f.ChangedOutside(cfg));

    /// <summary>A save just wrote <paramref name="saved"/>. Fields the user moved are now in step with it; an
    /// untouched field keeps its snapshots, so an outside value the save preserved is still reported by
    /// <see cref="ChangedOutside"/> until the host re-loads the control.</summary>
    public void NoteSaved(SystemConfig saved)
    {
        foreach (var f in _fields) f.NoteSaved(saved);
    }

    /// <summary>Order-sensitive value equality for a list of strings (null equals only null).</summary>
    public static IEqualityComparer<List<string>?> StringSequence { get; } = new StringSequenceComparer();

    /// <summary>A private copy of a list of strings, for snapshots of a list the control keeps editing.</summary>
    public static List<string>? CopyStrings(List<string>? list) => list is null ? null : new List<string>(list);

    private sealed class StringSequenceComparer : IEqualityComparer<List<string>?>
    {
        public bool Equals(List<string>? a, List<string>? b) =>
            ReferenceEquals(a, b) || (a is not null && b is not null && a.SequenceEqual(b));

        public int GetHashCode(List<string>? list)
        {
            var h = new HashCode();
            if (list is not null) foreach (var s in list) h.Add(s);
            return h.ToHashCode();
        }
    }

    private interface IField
    {
        string Name { get; }
        void Capture(SystemConfig cfg);
        SystemConfig Apply(SystemConfig cfg);
        bool ChangedOutside(SystemConfig cfg);
        void NoteSaved(SystemConfig saved);
    }

    private sealed class Field<T> : IField
    {
        private readonly Func<SystemConfig, T> _read;
        private readonly Func<T> _control;
        private readonly Func<SystemConfig, T, SystemConfig> _write;
        private readonly IEqualityComparer<T> _eq;
        private readonly Func<T, T> _copy;
        private bool _captured;
        private T _controlThen = default!;   // what the control reported when last in step
        private T _configThen = default!;    // what the config held then

        public Field(string name, Func<SystemConfig, T> read, Func<T> control,
                     Func<SystemConfig, T, SystemConfig> write, IEqualityComparer<T> eq, Func<T, T> copy)
        { Name = name; _read = read; _control = control; _write = write; _eq = eq; _copy = copy; }

        public string Name { get; }

        public void Capture(SystemConfig cfg)
        {
            _controlThen = _copy(_control());
            _configThen  = _copy(_read(cfg));
            _captured    = true;
        }

        public SystemConfig Apply(SystemConfig cfg)
        {
            if (!_captured) return cfg;
            var now = _control();
            return _eq.Equals(now, _controlThen) ? cfg : _write(cfg, now);
        }

        public bool ChangedOutside(SystemConfig cfg) => _captured && !_eq.Equals(_read(cfg), _configThen);

        public void NoteSaved(SystemConfig saved)
        {
            if (!_captured) return;
            var now = _control();
            if (_eq.Equals(now, _controlThen)) return;
            _controlThen = _copy(now);
            _configThen  = _copy(_read(saved));
        }
    }
}
