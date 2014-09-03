using OpenCLManagedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DotNetInterop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("NativeAmpLibrary", CallingConvention = CallingConvention.StdCall)]
        extern unsafe static void square_array(float* array, int length);

        public MainWindow()
        {
            InitializeComponent();
        }

        // Method marked unsafe to enable using the fixed command to pin managed objects in memory.
        private unsafe void RunAmp(object sender, RoutedEventArgs e)
        {
            // Allocate an array
            float[] arr = new[] { 1.0f, 2.0f, 3.0f, 4.0f };

            // Square the array elements using C++ AMP
            fixed (float* arrPt = &arr[0])
            {
                square_array(arrPt, arr.Length);
            }

            // Print output
            log.Text += string.Join(", ", arr) + Environment.NewLine;
        }

        private void RunOpenCL(object sender, RoutedEventArgs e)
        {
            var openCL = new OpenCLWrapper();

            // Allocate an array
            float[] arr = new[] { 1.0f, 2.0f, 3.0f, 4.0f };

            // Square the array element using OpenCL
            openCL.SquareArray(arr);

            // Print output
            log.Text += string.Join(", ", arr) + Environment.NewLine;
        }

        private void DeviceInfo(object sender, RoutedEventArgs e)
        {
            var openCL = new OpenCLWrapper();

            Log(string.Format("OpenCL Device Name: {0}", openCL.DeviceName));
            Log(string.Format("OpenCL Device Type: {0}", openCL.DeviceType));
            Log(string.Format("OpenCL Version: {0}", openCL.OpenCLVersion));
        }

        private void Log(string text)
        {
            log.Text += text + Environment.NewLine;
        }
    }
}
