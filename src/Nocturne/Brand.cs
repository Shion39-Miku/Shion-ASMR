using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Nocturne;

public static class Brand
{
    public const string Name = "月咲 Shion";
    const string IconUri = "pack://application:,,,/Nocturne;component/Assets/Brand/shion.png";

    public static ImageSource Artwork { get; } = LoadArtwork();

    static ImageSource LoadArtwork()
    {
        var image = new BitmapImage(new Uri(IconUri));
        image.Freeze();
        return image;
    }
}
