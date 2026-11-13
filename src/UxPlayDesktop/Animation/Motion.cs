using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace UxPlayDesktop.Animation;

/// <summary>Small entrance animations shared by the views.</summary>
public static class Motion
{
    /// <summary>
    /// Slides the element up a few pixels every time it becomes visible (a page coming into view). It only moves:
    /// fading a large element makes WPF draw it on a separate layer every frame, which stutters on weak GPUs.
    /// </summary>
    public static readonly DependencyProperty RiseOnShowProperty =
        DependencyProperty.RegisterAttached("RiseOnShow", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnRiseOnShowChanged));

    public static bool GetRiseOnShow(DependencyObject element) => (bool)element.GetValue(RiseOnShowProperty);

    public static void SetRiseOnShow(DependencyObject element, bool value) => element.SetValue(RiseOnShowProperty, value);

    private static void OnRiseOnShowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.IsVisibleChanged -= OnIsVisibleChanged;
        if (e.NewValue is true)
        {
            element.IsVisibleChanged += OnIsVisibleChanged;
        }
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is UIElement element && e.NewValue is true)
        {
            RiseIn(element);
        }
    }

    public static void RiseIn(UIElement element, double distance = 10)
    {
        var move = new TranslateTransform(0, distance);
        element.RenderTransform = move;
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(distance, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
