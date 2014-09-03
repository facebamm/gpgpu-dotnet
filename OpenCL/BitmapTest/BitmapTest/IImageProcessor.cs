using System;
namespace BitmapTest
{
    public interface IImageProcessor
    {
        void Process(int[] inputArray, int inputImgWidth, int inputImgHeight, double threshold, out int[] outputArray);
    }
}
