using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace UxPlayDesktop.Controls;

/// <summary>A pulsing dot for something that is live (mirroring in progress). It only animates while visible.</summary>
public partial class LiveDot : UserControl
{
    public LiveDot()
    {
        InitializeComponent();
        var pulse = (Storyboard)Resources["Pulse"];

        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                pulse.Begin(this, true);
            }
            else
            {
                pulse.Stop(this);
            }
        };
        Loaded += (_, _) =>
        {
            if (IsVisible)
            {
                pulse.Begin(this, true);
            }
        };
    }
}
