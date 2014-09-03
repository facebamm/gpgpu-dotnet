using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitmapTest
{
    public class CPUImageProcessor : IImageProcessor
    {
        public enum Channel
        {
            R,
            G,
            B,
            RGB
        }
        
        public Channel SelectedChannel { get; set; }
        public bool ConvertToGrayscale { get; set; }
        public bool CopyOriginal { get; set; }

        private int _edgeColor = unchecked((int)0xff101010);
        private static readonly double INTENSITY_RED = 0.3;
        private static readonly double INTENSITY_GREEN = 0.59;
        private static readonly double INTENSITY_BLUE = 0.11;


        public void Process(int[] inputArray, int inputImgWidth, int inputImgHeight, double threshold, out int[] outputArray)
        {
            int[,] matrix = SplitPixels(inputArray, inputImgWidth, inputImgHeight);
            Sobel(matrix, inputArray, inputImgWidth, inputImgHeight, threshold);

            outputArray = inputArray;
        }

        private int[,] SplitPixels(int[] pixelData, int w, int h)
        {
            var matrix = new int[h, w];

            for (int i = 0; i < h; i++)
                for (int j = 0; j < w; j++)
                    matrix[i, j] = pixelData[i * w + j];

            return matrix;
        }

        private void Negative(int[] pixelData)
        {
            for (int i = 0; i < pixelData.Length; ++i)
            {
                pixelData[i] ^= 0x00ffffff;
            }
        }

        private void Sobel(int[,] intputData, int[] outputData, int width, int height, double threshold)
        {
            for (int i = 1; i < height - 2; i++)
            {
                for (int j = 1; j < width - 2; j++)
                {
                    int cr = intputData[i + 1, j];
                    int cl = intputData[i - 1, j];
                    int cu = intputData[i, j - 1];
                    int cd = intputData[i, j + 1];
                    int cld = intputData[i - 1, j + 1];
                    int clu = intputData[i - 1, j - 1];
                    int crd = intputData[i + 1, j + 1];
                    int cru = intputData[i + 1, j - 1];
                    int dx = 0, dy = 0;
                    switch (SelectedChannel)
                    {
                        case Channel.R:
                            dx = Red(cld) + 2 * Red(cd) + Red(crd) - (Red(clu) + 2 * Red(cu) + Red(cru));
                            dy = Red(crd) + 2 * Red(cr) + Red(cru) - (Red(cld) + 2 * Red(cl) + Red(clu));
                            break;
                        case Channel.G:
                            dx = Green(cld) + 2 * Green(cd) + Green(crd) - (Green(clu) + 2 * Green(cu) + Green(cru));
                            dy = Green(crd) + 2 * Green(cr) + Green(cru) - (Green(cld) + 2 * Green(cl) + Green(clu));
                            break;
                        case Channel.B:
                            dx = Blue(cld) + 2 * Blue(cd) + Blue(crd) - (Blue(clu) + 2 * Blue(cu) + Blue(cru));
                            dy = Blue(crd) + 2 * Blue(cr) + Blue(cru) - (Blue(cld) + 2 * Blue(cl) + Blue(clu));
                            break;
                        case Channel.RGB:
                            dx = Blue(Grayscale(cld)) + 2 * Blue(Grayscale(cd)) + Blue(Grayscale(crd)) - (Blue(Grayscale(clu)) + 2 * Blue(Grayscale(cu)) + Blue(Grayscale(cru)));
                            dy = Blue(Grayscale(crd)) + 2 * Blue(Grayscale(cr)) + Blue(Grayscale(cru)) - (Blue(Grayscale(cld)) + 2 * Blue(Grayscale(cl)) + Blue(Grayscale(clu)));
                            break;
                        //case Channel.RGB:
                        //    dx = grayscale(cld).B + 2 * grayscale(cd).B + grayscale(crd).B - (grayscale(clu).B + 2 * grayscale(cu).B + grayscale(cru).B);
                        //    dy = grayscale(crd).B + 2 * grayscale(cr).B + grayscale(cru).B - (grayscale(cld).B + 2 * grayscale(cl).B + grayscale(clu).B);
                        //    break;
                    }
                    double power = Math.Abs(dx) + Math.Abs(dy);
                    if (power > threshold)
                        outputData[i * width + j] = _edgeColor;
                    else
                    {
                        if (CopyOriginal)
                        {
                            int c = outputData[i * width + j];
                            if (ConvertToGrayscale)
                                outputData[i * width + j] = Grayscale(c);
                            else
                                outputData[i * width + j] = c;
                        }
                        else
                            outputData[i * width + j] = unchecked((int)0xffffffff);
                    }
                }
            }
        }

        public int Grayscale(int pixel)
        {
            int red, green, blue, grey;

            red = (pixel & 0x00ff0000) >> 16;
            green = (pixel & 0x0000ff00) >> 8;
            blue = (pixel & 0x000000ff);

            grey = (int)(red * INTENSITY_RED) + (int)(green * INTENSITY_GREEN) + (int)(blue * INTENSITY_BLUE);

            pixel = (pixel & unchecked((int)0xff000000)) | (grey << 16) | (grey << 8) | grey;

            return pixel;
        }

        private int Red(int pixel)
        {
            return (pixel & 0x00ff0000) >> 16;
        }

        private int Green(int pixel)
        {
            return (pixel & 0x0000ff00) >> 8;
        }

        private int Blue(int pixel)
        {
            return (pixel & 0x000000ff);
        }
    }
}
