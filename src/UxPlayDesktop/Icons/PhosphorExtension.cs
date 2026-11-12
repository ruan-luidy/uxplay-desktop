using System;
using System.Collections.Concurrent;
using System.Windows.Markup;
using System.Windows.Media;
using MahApps.Metro.IconPacks;

namespace UxPlayDesktop.Icons;

/// <summary>
/// A Phosphor icon as a frozen <see cref="Geometry"/>, for use straight from XAML:
/// <c>&lt;Path Data="{icons:Phosphor GearSixBold}" /&gt;</c> or
/// <c>&lt;Button hc:IconElement.Geometry="{icons:Phosphor XBold}" /&gt;</c>.
/// Each icon is parsed once and cached.
/// </summary>
[MarkupExtensionReturnType(typeof(Geometry))]
public sealed class PhosphorExtension : MarkupExtension
{
    private static readonly ConcurrentDictionary<PackIconPhosphorIconsKind, Geometry> Cache = new();

    public PhosphorExtension()
    {
    }

    public PhosphorExtension(PackIconPhosphorIconsKind kind)
    {
        Kind = kind;
    }

    [ConstructorArgument("kind")]
    public PackIconPhosphorIconsKind Kind { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Get(Kind);

    public static Geometry Get(PackIconPhosphorIconsKind kind) => Cache.GetOrAdd(kind, k =>
    {
        var data = new PackIconPhosphorIcons { Kind = k }.Data;
        if (string.IsNullOrEmpty(data))
        {
            return Geometry.Empty;
        }

        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    });
}
