using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BitmapTest
{
    public class AmpImageProcessor : IImageProcessor
    {
        [DllImport("SobelAmp", CallingConvention = CallingConvention.StdCall)]
        extern unsafe static void filter_image(int* imageData, int* outputData, int width, int height, double threshold);

        public unsafe void Process(int[] inputArray, int inputImgWidth, int inputImgHeight, double threshold, out int[] outputArray)
        {
            outputArray = new int[inputArray.Length];

            fixed (int* inPtr = &inputArray[0])
            {
                fixed (int* outPtr = &outputArray[0])
                {
                    filter_image(inPtr, outPtr, inputImgWidth, inputImgHeight, threshold);
                }
            }
        }
    }
}
