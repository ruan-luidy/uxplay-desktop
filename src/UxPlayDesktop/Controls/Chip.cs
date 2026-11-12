using System.Windows;
using System.Windows.Controls;
using UxPlayDesktop.Models;

namespace UxPlayDesktop.Controls;

/// <summary>
/// Small status pill: the tone's colour at low opacity behind the text in the full colour.
/// The look lives in Themes/Surfaces.xaml.
/// </summary>
public class Chip : ContentControl
{
    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(nameof(Tone), typeof(Tone), typeof(Chip), new PropertyMetadata(Tone.Neutral));

    static Chip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Chip), new FrameworkPropertyMetadata(typeof(Chip)));
    }

    public Tone Tone
    {
        get => (Tone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }
}
