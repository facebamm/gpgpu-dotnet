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
using WPFMediaKit.DirectShow.Controls;
using System.Threading.Tasks;
using System.ComponentModel;

namespace BitmapTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class VideoWindow : Window, INotifyPropertyChanged
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
        private int _width = 480;
        private int _height = 360;

        public int FPS
        {
            get { return _fps; }
            set
            {
                if (value != _fps)
                {
                    _fps = value;
                    if(PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("FPS"));
                }
            }
        }
        private int _fps;
        

        public VideoWindow()
        {
            InitializeComponent();
            DataContext = this;

            videoElement.VideoCaptureDevice = MultimediaUtil.VideoInputDevices.FirstOrDefault();

            ConvertToGrayscale = false;
            CopyOriginal = false;
            Threshold = 50;
            SelectedChannel = CPUImageProcessor.Channel.RGB;
            DataContext = this;
            _openCLImageProcessor = new OpenCLImageProcessor("sobel.cl");
            _cpuImageProcessor = new CPUImageProcessor { SelectedChannel = this.SelectedChannel };
            _ampImageProcessor = new AmpImageProcessor();

            Processors = new List<string>(new string[] { "CPU", "C++ AMP", "OpenCL" });

            Task.Run(() =>
            {
                int[] pixelData = new int[_width * _height];
                var sw = new Stopwatch();
                sw.Start();

                while (true)
                {
                    sw.Restart();
                    Dispatcher.Invoke(() => GetVideoFrame(ref pixelData));

                    var result = ProcessVideoFrame(pixelData);
                    Dispatcher.Invoke(() => SetProcessedVideoFrame(result));

                    if(sw.ElapsedMilliseconds != 0)
                        FPS = (int)(1000.0 / sw.ElapsedMilliseconds);
                }
            });
        }

        private void SetProcessedVideoFrame(int[] result)
        {
            WriteableBitmap source = null;

            if (convolutedImage.Source == null)
            {
                source = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Pbgra32, null);
                convolutedImage.Source = source;
            }

            source = convolutedImage.Source as WriteableBitmap;

            source.WritePixels(new Int32Rect(0, 0, _width, _height), result, _width * 4, 0);
        }

        private void GetVideoFrame(ref int[] data)
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(videoElement.DesiredPixelWidth, videoElement.DesiredPixelHeight, 96, 96, PixelFormats.Default);
            bmp.Render(videoElement);
            bmp.CopyPixels(data, 4 * _width, 0);
        }

        private void Convolute_Click(object sender, RoutedEventArgs e)
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(_width, _height, 96, 96, PixelFormats.Default);
            bmp.Render(videoElement);

            if (convolutedImage.Source == null)
            {
                var source = new WriteableBitmap(bmp.PixelWidth, bmp.PixelHeight, bmp.DpiX, bmp.DpiY, bmp.Format, bmp.Palette);
                convolutedImage.Source = source;
            }

            Convolute(bmp, convolutedImage.Source as WriteableBitmap);
        }

        private void Convolute(BitmapSource source, WriteableBitmap target)
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

        private int[] ProcessVideoFrame(int[] data)
        {
            if (data == null)
                return null;

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
                    return null;
            }

            processor.Process(data, _width, _height, Threshold, out result);

            return result;
        }


        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Reload();
        }

        private void Reload()
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(videoElement.DesiredPixelWidth, videoElement.DesiredPixelHeight, 96, 96, PixelFormats.Default);
            bmp.Render(videoElement);
            Convolute(bmp, convolutedImage.Source as WriteableBitmap);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}