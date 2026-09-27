using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Kantela.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Kantela.Controls;

// Shows a site's favicon, or a globe when there is none or it cannot be decoded.
public sealed partial class SiteIconView : UserControl
{
    // Typed as object because the value is a plain .NET record, not a Windows Runtime type.
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(SiteIconView), new PropertyMetadata(null, (d, _) => ((SiteIconView)d).UpdateImage()));

    private const int DecodePixels = 40;

    private int _version;

    public SiteIconView()
    {
        InitializeComponent();
    }

    public FaviconImage? Icon
    {
        get => (FaviconImage?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private async void UpdateImage()
    {
        int version = ++_version;
        ShowPlaceholder();
        if (Icon is not FaviconImage icon)
        {
            return;
        }

        try
        {
            using InMemoryRandomAccessStream stream = new();
            await stream.WriteAsync(icon.Data.AsBuffer());
            stream.Seek(0);

            ImageSource source;
            if (icon.IsSvg)
            {
                SvgImageSource svg = new() { RasterizePixelWidth = DecodePixels, RasterizePixelHeight = DecodePixels };
                if (await svg.SetSourceAsync(stream) != SvgImageSourceLoadStatus.Success)
                {
                    return;
                }

                source = svg;
            }
            else
            {
                BitmapImage bitmap = new() { DecodePixelWidth = DecodePixels, DecodePixelType = DecodePixelType.Logical };
                await bitmap.SetSourceAsync(stream);
                source = bitmap;
            }

            if (version == _version)
            {
                IconImage.Source = source;
                IconImage.Visibility = Visibility.Visible;
                PlaceholderIcon.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception)
        {
            // Undecodable data keeps the placeholder.
        }
    }

    private void ShowPlaceholder()
    {
        IconImage.Source = null;
        IconImage.Visibility = Visibility.Collapsed;
        PlaceholderIcon.Visibility = Visibility.Visible;
    }
}
