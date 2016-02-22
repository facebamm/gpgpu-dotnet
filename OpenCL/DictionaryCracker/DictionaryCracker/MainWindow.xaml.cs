using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DictionaryCracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<IMD5Cracker> _crackers;
        private List<string> _passwords;

        public MainWindow()
        {
            InitializeComponent();

            log.Text += "Loading dictionary... ";

            Task.Run(() =>
            {
                _crackers = new List<IMD5Cracker>();
                _crackers.Add(new CpuMD5Cracker());
                _crackers.Add(new OpenCLMD5Cracker());

                var passwords = File.ReadAllLines("passwords.txt");
                _passwords = passwords.Where(p => p.Length < 16).ToList();

                _passwords.AddRange(passwords.Select(p => p.ToUpper()));

            }).ContinueWith(t =>
            {
                log.Text += _passwords.Count.ToString("N0") + " passwords loaded." + Environment.NewLine + Environment.NewLine;
                button.IsEnabled = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            IProgress<string> progress = new Progress<string>(p =>
            {
                log.Text += p + Environment.NewLine;
                log.ScrollToEnd();
            });

            Task.Run(() =>
            {
                Stopwatch sw = new Stopwatch();
                var hash = "81596a5a9c2174b0a286cbf3ccf3528b";

                foreach (var cracker in _crackers)
                {
                    sw.Restart();
                    progress.Report("Cracker: " + cracker.Name + " warming up...");
                    progress.Report("Device: " + cracker.Device);

                    cracker.Warmup();

                    progress.Report("Warmup finished in " + sw.Elapsed.ToString());
                    progress.Report("Cracking hash: " + hash);
                    sw.Restart();

                    var match = cracker.Crack(_passwords, hash);

                    var elapsed = sw.ElapsedMilliseconds;
                    var mHashPerSec = (double)_passwords.Count / (1000000.0 * elapsed / 1000.0);

                    progress.Report("Finished in " + sw.Elapsed.ToString());
                    progress.Report("Mhash/sec : " + mHashPerSec.ToString("F2"));
                    if (string.IsNullOrEmpty(match))
                        progress.Report("No match found.");
                    else
                        progress.Report("Password match found: " + match);

                    progress.Report("");
                }

                sw.Stop();
            });
        }
    }
}
