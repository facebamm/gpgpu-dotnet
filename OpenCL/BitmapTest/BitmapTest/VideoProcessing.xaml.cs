using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BitmapTest
{
    /// <summary>
    /// Interaction logic for VideoProcessing.xaml
    /// </summary>
    public partial class VideoProcessing : Window
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr handle);

        public VideoProcessing()
        {
            InitializeComponent();

            videoElement.EnableSampleGrabbing = true;
            videoElement.NewVideoSample += videoElement_NewVideoSample;
        }

        void videoElement_NewVideoSample(object sender, WPFMediaKit.DirectShow.MediaPlayers.VideoSampleArgs e)
        {
            var a = e.VideoFrame;
            var conv = new ImageConverter();
            byte[] image = (byte[])conv.ConvertTo(e.VideoFrame, typeof(byte[]));

            var bs = LoadBitmap(e.VideoFrame);

            int w = e.VideoFrame.Width;
            int h = e.VideoFrame.Height;
            int[] pixelData = new int[w * h];
            int widthInBytes = 4 * w;
            bs.CopyPixels(pixelData, widthInBytes, 0);

        }

        public static BitmapSource LoadBitmap(Bitmap source)
        {

            var ip = source.GetHbitmap();

            var bs = Imaging.CreateBitmapSourceFromHBitmap(ip, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            DeleteObject(ip);

            return bs;

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(videoElement.NaturalVideoWidth, videoElement.NaturalVideoHeight, 96, 96, PixelFormats.Default);
            bmp.Render(videoElement);

            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            int[] pixelData = new int[w * h];
            int widthInBytes = 4 * w;
            bmp.CopyPixels(pixelData, widthInBytes, 0);

        }
    }
}
