namespace LazyChromeWindowBridge.Core;

/// <summary>Canonical keys permitted for one bounded Chrome key dispatch.</summary>
public enum BrowserKey
{
    PageDown = 1, PageUp, Enter, Tab, Escape, Space, Home, End,
    ArrowUp, ArrowDown, ArrowLeft, ArrowRight, Backspace, Delete,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9
}

[Flags]
public enum BrowserKeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8
}

/// <summary>One allowlisted key and zero or more standard modifiers. It is not a macro or text input API.</summary>
public sealed record BrowserKeyChord(BrowserKey Key, BrowserKeyModifiers Modifiers = BrowserKeyModifiers.None)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Key)) throw new ArgumentException("Unknown browser key.");
        const BrowserKeyModifiers allowed = BrowserKeyModifiers.Ctrl | BrowserKeyModifiers.Shift | BrowserKeyModifiers.Alt | BrowserKeyModifiers.Meta;
        if ((Modifiers & ~allowed) != 0) throw new ArgumentException("Unknown browser key modifier.");
    }
}
