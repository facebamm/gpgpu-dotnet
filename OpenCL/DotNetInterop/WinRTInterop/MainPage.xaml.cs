using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using WindowsRuntimeAmpComponent;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace WinRTInterop
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Run(object sender, RoutedEventArgs e)
        {
            button.IsEnabled = false;
            var arr = new[] { 1.0f, 2.0f, 3.0f, 4.0f };
            List<float> inputs = new List<float>(arr);

            var amp = new AmpRuntimeComponent();
            IReadOnlyList<float> outputs = await amp.square_array_async(inputs);

            log.Text += string.Join(", ", outputs);
            button.IsEnabled = true;
        }
    }
}
