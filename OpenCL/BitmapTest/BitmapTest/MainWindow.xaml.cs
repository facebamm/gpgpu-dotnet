using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Diagnostics;
using System.Net.Http;
using System.Net;

namespace BitmapTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string SelectedProcessor { get; set; }
        public List<string> Processors { get; set; }
        public CPUImageProcessor.Channel SelectedChannel { get; set; }
        public double Threshold { get; set; }
        public bool ConvertToGrayscale { get; set; }
        public bool CopyOriginal { get; set; }

        private CPUImageProcessor _cpuImageProcessor;
        private OpenCLImageProcessor _openCLImageProcessor;
        private AmpImageProcessor _ampImageProcessor;


        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            ConvertToGrayscale = false;
            CopyOriginal = false;
            Threshold = 50;
            SelectedChannel = CPUImageProcessor.Channel.RGB;
            DataContext = this;
            _cpuImageProcessor = new CPUImageProcessor { SelectedChannel = this.SelectedChannel };
            _ampImageProcessor = new AmpImageProcessor();
            _openCLImageProcessor = new OpenCLImageProcessor("sobel.cl");

            Processors = new List<string>(new string[] {"CPU", "C++ AMP", "OpenCL"});
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            LoadImage();
        }

        private void LoadImage()
        {
            Uri u = new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image.png"), UriKind.Absolute);
            BitmapImage originalBmp = new BitmapImage(u);
            originalBmp.CacheOption = BitmapCacheOption.OnLoad;
            BitmapSource prgbaSource = new FormatConvertedBitmap(originalBmp, PixelFormats.Pbgra32, null, 0);
            var bmp = new WriteableBitmap(prgbaSource);
            originalImage.Source = bmp;

            var source = bmp;
            bmp = new WriteableBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, source.Format, source.Palette);
            convolutedImage.Source = bmp;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Convolute(originalImage.Source as WriteableBitmap, convolutedImage.Source as WriteableBitmap);
        }

        private void Convolute(WriteableBitmap source, WriteableBitmap target)
        {
            if (source == null || target == null)
                return;

            var sw = new Stopwatch();
            sw.Start();

            int w = target.PixelWidth;
            int h = target.PixelHeight;
            int[] pixelData = new int[w * h];
            int widthInBytes = 4 * w;
            source.CopyPixels(pixelData, widthInBytes, 0);
            int[] result = null;

            IImageProcessor processor = null;
            switch (SelectedProcessor)
            {
                case "CPU":
                    processor = _cpuImageProcessor;
                    break;
                case "C++ AMP":
                    processor = _ampImageProcessor;
                    break;
                case "OpenCL":
                    processor = _openCLImageProcessor;
                    break;
                default:
                    return;
            }

            processor.Process(pixelData, w, h, Threshold, out result);
            target.WritePixels(new Int32Rect(0, 0, w, h), result, widthInBytes, 0);

            sw.Stop();
            time.Text = sw.ElapsedMilliseconds.ToString();
        }


        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Reload();
        }

        private void Reload()
        {
            Convolute(originalImage.Source as WriteableBitmap, convolutedImage.Source as WriteableBitmap);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        private void Button_Get_Click(object sender, RoutedEventArgs e)
        {
            var client = new WebClient();
            client.DownloadFileCompleted += client_DownloadFileCompleted;
            client.DownloadFileAsync(new Uri(url.Text), System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image.png"));
        }

        private void client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
                MessageBox.Show(e.Error.ToString());
            else
                LoadImage();
        }
    }
}