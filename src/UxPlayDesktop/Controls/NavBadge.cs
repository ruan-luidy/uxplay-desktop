using System.Windows;

namespace UxPlayDesktop.Controls;

/// <summary>
/// A counter on a side navigation item (errors on the Logs page). Zero is hidden;
/// <see cref="IsAlertProperty"/> paints it red instead of the accent colour.
/// </summary>
public static class NavBadge
{
    public static readonly DependencyProperty CountProperty =
        DependencyProperty.RegisterAttached("Count", typeof(int), typeof(NavBadge), new PropertyMetadata(0));

    public static readonly DependencyProperty IsAlertProperty =
        DependencyProperty.RegisterAttached("IsAlert", typeof(bool), typeof(NavBadge), new PropertyMetadata(false));

    public static int GetCount(DependencyObject element) => (int)element.GetValue(CountProperty);

    public static void SetCount(DependencyObject element, int value) => element.SetValue(CountProperty, value);

    public static bool GetIsAlert(DependencyObject element) => (bool)element.GetValue(IsAlertProperty);

    public static void SetIsAlert(DependencyObject element, bool value) => element.SetValue(IsAlertProperty, value);
}
