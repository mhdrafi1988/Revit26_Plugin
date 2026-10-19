using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
            
namespace Revit26_Plugin.Resources.Icons
{
    internal static class ImageUtils
    {
        public static BitmapImage Load(string resourcePath)
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            using Stream stream = asm.GetManifestResourceStream(resourcePath)
                ?? throw new FileNotFoundException("Icon not found: " + resourcePath);

            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze(); // 🔥 important for Revit stability

            return image;
        }
    }
}
