using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Converters;

/// <summary>
/// Maps a mail-folder name to a stable, distinct badge brush (an empty name yields a transparent
/// brush). The stable name→index mapping lives in <see cref="FolderColorPicker"/> (framework-free
/// and unit-tested); this converter owns the Avalonia colours so <c>EmailRow</c> stays UI-agnostic.
/// </summary>
public sealed class FolderNameToBrushConverter : IValueConverter
{
    // One brush per FolderColorPicker.PaletteSize slot; saturated colours that read against white text.
    private static readonly IBrush[] s_brushes =
    [
        new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), // blue
        new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)), // emerald
        new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)), // amber
        new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)), // violet
        new SolidColorBrush(Color.FromRgb(0xE1, 0x1D, 0x48)), // rose
        new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xB2)), // cyan
        new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)), // orange
        new SolidColorBrush(Color.FromRgb(0xC0, 0x26, 0xD3)), // fuchsia
    ];

    public object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name || name.Length == 0)
        {
            return Brushes.Transparent;
        }

        return s_brushes[FolderColorPicker.IndexFor(name)];
    }

    public object? ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
