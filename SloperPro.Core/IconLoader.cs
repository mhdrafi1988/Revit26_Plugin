using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

public static class IconLoader
{
    public static BitmapSource LoadPng(string resourcePath)
    {
        var asm = Assembly.GetExecutingAssembly();
        using Stream stream = asm.GetManifestResourceStream(resourcePath);
        return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
    }
}
