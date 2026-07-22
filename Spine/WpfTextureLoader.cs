using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Loads atlas page PNGs into BGRA pixel buffers for the software renderer.
/// </summary>
public sealed class WpfTextureLoader : TextureLoader
{
    public void Load(AtlasPage page, string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Atlas page texture not found.", path);
        }

        var uri = new Uri(path, UriKind.Absolute);
        var decoder = BitmapDecoder.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        page.width = width;
        page.height = height;
        page.rendererObject = new AtlasTexture(width, height, pixels);
    }

    public void Unload(object texture)
    {
        // AtlasTexture is GC-managed.
    }
}

public sealed class AtlasTexture
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public AtlasTexture(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }
}
