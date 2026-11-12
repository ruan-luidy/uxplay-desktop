using System;
using System.Windows;
using System.Windows.Media;
using HandyControl.Themes;

namespace UxPlayDesktop.Services;

/// <summary>
/// Dark or light theme and the accent colour. HandyControl switches its own brushes through
/// <see cref="ThemeManager"/>; the brushes from Themes/Tokens.xaml get their theme variant in a single
/// dictionary added at the end of the application resources, replaced as a whole on every change.
/// </summary>
public static class ThemeService
{
    private static ResourceDictionary? _layer;
    private static bool _isDark = true;
    private static Color _accent = (Color)ColorConverter.ConvertFromString("#3778BF");

    public static bool IsDark => _isDark;

    public static event EventHandler? Changed;

    public static void Apply(bool dark)
    {
        _isDark = dark;
        Refresh();
    }

    public static void SetAccent(string hex)
    {
        try
        {
            _accent = (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            return;
        }

        Refresh();
    }

    private static void Refresh()
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        var accentBrush = new SolidColorBrush(_accent);
        accentBrush.Freeze();
        ThemeManager.Current.ApplicationTheme = _isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ThemeManager.Current.AccentColor = accentBrush;

        var layer = new ResourceDictionary();
        void Set(string key, Color color, double opacity = 1)
        {
            var brush = new SolidColorBrush(color) { Opacity = opacity };
            brush.Freeze();
            layer[key] = brush;
        }

        var dark = _isDark;
        // Layers brighten the backdrop in the dark theme and darken it in the light one
        var ink = dark ? Colors.White : Colors.Black;
        Set("Window.FallbackBrush", dark ? Color.FromRgb(0x1C, 0x1C, 0x1C) : Color.FromRgb(0xF3, 0xF3, 0xF3));
        Set("Pane.BackgroundBrush", Colors.White, dark ? 0.07 : 0.7);
        Set("Card.BackgroundBrush", ink, dark ? 0.078 : 0.045);
        Set("Divider.Brush", ink, dark ? 0.078 : 0.08);
        Set("Progress.TrackBrush", ink, 0.1);
        Set("Button.BackgroundBrush", ink, dark ? 0.09 : 0.06);
        Set("Button.HoverBrush", ink, dark ? 0.14 : 0.1);
        Set("Button.PressedBrush", ink, dark ? 0.055 : 0.035);
        Set("Nav.SelectedBrush", ink, dark ? 0.10 : 0.06);
        Set("Hover.RowBrush", ink, dark ? 0.06 : 0.05);
        Set("Switch.TrackBrush", ink, dark ? 0.2 : 0.22);
        Set("MutedTextBrush", dark ? Color.FromRgb(0x85, 0x85, 0x8C) : Color.FromRgb(0x78, 0x78, 0x80));
        Set("ScrollBar.ThumbBrush", dark ? Color.FromRgb(0x4A, 0x4A, 0x4A) : Color.FromRgb(0xC2, 0xC2, 0xC2));
        Set("ScrollBar.ThumbHoverBrush", dark ? Color.FromRgb(0x6F, 0x6F, 0x6F) : Color.FromRgb(0xA0, 0xA0, 0xA0));
        Set("ScrollBar.ThumbPressedBrush", dark ? Color.FromRgb(0x9A, 0x9A, 0x9A) : Color.FromRgb(0x80, 0x80, 0x80));

        Set("Flyout.BackgroundBrush", dark ? Color.FromRgb(0x2B, 0x2B, 0x2E) : Color.FromRgb(0xFB, 0xFB, 0xFC));
        Set("Flyout.BorderBrush", ink, dark ? 0.12 : 0.1);
        Set("Dialog.BackgroundBrush", dark ? Color.FromRgb(0x1F, 0x1F, 0x22) : Color.FromRgb(0xFB, 0xFB, 0xFC));
        Set("Dialog.FooterBrush", Colors.Black, dark ? 0.2 : 0.035);
        Set("Overlay.ScrimBrush", Colors.Black, dark ? 0.55 : 0.3);

        // Tone texts: the full colours disappear on white, so the light theme uses a deeper shade
        Set("Accent.SoftBrush", _accent, dark ? 0.18 : 0.14);
        Set("Accent.TextBrush", dark ? Mix(_accent, Colors.White, 0.3) : Mix(_accent, Colors.Black, 0.2));
        Set("Success.TextBrush", dark ? Color.FromRgb(0x3D, 0xCB, 0x5E) : Color.FromRgb(0x1E, 0x8C, 0x3A));
        Set("Warning.TextBrush", dark ? Color.FromRgb(0xE9, 0xAF, 0x20) : Color.FromRgb(0xD9, 0x77, 0x06));
        Set("Danger.TextBrush", dark ? Color.FromRgb(0xE5, 0x52, 0x5E) : Color.FromRgb(0xC2, 0x29, 0x35));

        var merged = app.Resources.MergedDictionaries;
        if (_layer != null)
        {
            merged.Remove(_layer);
        }
        merged.Add(layer);
        _layer = layer;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount));
}
