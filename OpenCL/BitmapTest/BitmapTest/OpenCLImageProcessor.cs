using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Cl = OpenCL.Net;

namespace BitmapTest
{
    class OpenCLImageProcessor : IDisposable, IImageProcessor
    {
        private Cl.Context _context;
        private Cl.Device _device;
        private Cl.Program program;
        private string programPath;

        public OpenCLImageProcessor(string programPath)
        {
            programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\sobel.cl");
            if (!File.Exists(programPath))
                programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"sobel.cl");
            if (!File.Exists(programPath))
                throw new FileNotFoundException(programPath);

            this.programPath = programPath;
            Setup();
            LoadProgram();
        }

        private void CheckErr(Cl.ErrorCode err, string name)
        {
            if (err != Cl.ErrorCode.Success)
            {
                Console.WriteLine("ERROR: " + name + " (" + err.ToString() + ")");
                MessageBox.Show("ERROR: " + name + " (" + err.ToString() + ")");
            }
        }
        private void ContextNotify(string errInfo, byte[] data, IntPtr cb, IntPtr userData)
        {
            Console.WriteLine("OpenCL Notification: " + errInfo);
            MessageBox.Show("OpenCL Notification: " + errInfo);
        }

        private void Setup()
        {
            Cl.ErrorCode error;
            Cl.Platform[] platforms = Cl.Cl.GetPlatformIDs(out error);
            List<Cl.Device> devicesList = new List<Cl.Device>();

            CheckErr(error, "Cl.GetPlatformIDs");

            foreach (Cl.Platform platform in platforms)
            {
                string platformName = Cl.Cl.GetPlatformInfo(platform, Cl.PlatformInfo.Name, out error).ToString();
                Console.WriteLine("Platform: " + platformName);
                CheckErr(error, "Cl.GetPlatformInfo");
                //We will be looking only for GPU devices
#if DEBUG
                foreach (Cl.Device device in Cl.Cl.GetDeviceIDs(platform, Cl.DeviceType.Cpu, out error))
#else
                foreach (Cl.Device device in Cl.Cl.GetDeviceIDs(platform, Cl.DeviceType.Gpu, out error))
#endif
                {
                    CheckErr(error, "Cl.GetDeviceIDs");
                    Console.WriteLine("Device: " + device.ToString());
                    devicesList.Add(device);
                }
            }

            if (devicesList.Count <= 0)
            {
                Console.WriteLine("No devices found.");
                return;
            }

            _device = devicesList[0];

            if (Cl.Cl.GetDeviceInfo(_device, Cl.DeviceInfo.ImageSupport,
                      out error).CastTo<Cl.Bool>() == Cl.Bool.False)
            {
                Console.WriteLine("No image support.");
                return;
            }
            _context = Cl.Cl.CreateContext(null, 1, new[] { _device }, ContextNotify, IntPtr.Zero, out error);    //Second parameter is amount of devices
            CheckErr(error, "Cl.CreateContext");
        }

        private void LoadProgram()
        {
            Cl.ErrorCode error;
            if (!System.IO.File.Exists(programPath))
            {
                Console.WriteLine("Program doesn't exist at path " + programPath);
                return;
            }

            string programSource = System.IO.File.ReadAllText(programPath);

            program = Cl.Cl.CreateProgramWithSource(_context, 1, new[] { programSource }, null, out error);
            CheckErr(error, "Cl.CreateProgramWithSource");

            //Compile kernel source
#if DEBUG
            var args = string.Format("-g -s \"{0}\"", programPath);
            error = Cl.Cl.BuildProgram(program, 0, null, args, null, IntPtr.Zero);
#else
            error = Cl.Cl.BuildProgram(program, 1, new[] { _device }, string.Empty, null, IntPtr.Zero);
#endif
            CheckErr(error, "Cl.BuildProgram");
            //Check for any compilation errors
            if (Cl.Cl.GetProgramBuildInfo(program, _device, Cl.ProgramBuildInfo.Status, out error).CastTo<Cl.BuildStatus>()
                != Cl.BuildStatus.Success)
            {
                CheckErr(error, "Cl.GetProgramBuildInfo");
                Console.WriteLine("Cl.GetProgramBuildInfo != Success");
                Console.WriteLine(Cl.Cl.GetProgramBuildInfo(program, _device, Cl.ProgramBuildInfo.Log, out error));

                return;
            }
        }

        public void Process(int[] inputArray, int inputImgWidth, int inputImgHeight, double threshold, out int[] outputArray)
        {
            Cl.ErrorCode error;
            outputArray = null;

            //Create the required kernel (entry function)
            Cl.Kernel kernel = Cl.Cl.CreateKernel(program, "sobelEdgeDetect", out error);
            CheckErr(error, "Cl.CreateKernel");

            int intPtrSize = IntPtr.Size;

            //OpenCL memory buffer that will keep our image's byte[] data.
            Cl.IMem inputImage2DBuffer;
            Cl.ImageFormat clImageFormat = new Cl.ImageFormat(Cl.ChannelOrder.RGBA, Cl.ChannelType.Unsigned_Int8);

            //Copy the raw bitmap data to an unmanaged byte[] array
            //inputByteArray = new byte[inputImgBytesSize];
            //Marshal.Copy(bitmapData.Scan0, inputByteArray, 0, inputImgBytesSize);
            //Allocate OpenCL image memory buffer
            inputImage2DBuffer = Cl.Cl.CreateImage2D(_context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly, clImageFormat,
                                                (IntPtr)inputImgWidth, (IntPtr)inputImgHeight,
                                                (IntPtr)0, inputArray, out error);
            CheckErr(error, "Cl.CreateImage2D input");

            //Unmanaged output image's raw RGBA byte[] array
            outputArray = new int[inputArray.Length];
            //Allocate OpenCL image memory buffer
            Cl.IMem outputImage2DBuffer = Cl.Cl.CreateImage2D(_context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.WriteOnly, clImageFormat, (IntPtr)inputImgWidth,
                (IntPtr)inputImgHeight, (IntPtr)0, outputArray, out error);
            CheckErr(error, "Cl.CreateImage2D output");
            //Pass the memory buffers to our kernel function
            error = Cl.Cl.SetKernelArg(kernel, 0, (IntPtr)intPtrSize, inputImage2DBuffer);
            error |= Cl.Cl.SetKernelArg(kernel, 1, (IntPtr)intPtrSize, outputImage2DBuffer);
            error |= Cl.Cl.SetKernelArg(kernel, 2, (float)threshold / 250.0f);
            CheckErr(error, "Cl.SetKernelArg");

            //Create a command queue, where all of the commands for execution will be added
            Cl.CommandQueue cmdQueue = Cl.Cl.CreateCommandQueue(_context, _device, (Cl.CommandQueueProperties)0, out error);
            CheckErr(error, "Cl.CreateCommandQueue");
            Cl.Event clevent;

            //Copy input image from the host to the GPU.
            IntPtr[] originPtr = new IntPtr[] { (IntPtr)0, (IntPtr)0, (IntPtr)0 };    //x, y, z
            IntPtr[] regionPtr = new IntPtr[] { (IntPtr)inputImgWidth, (IntPtr)inputImgHeight, (IntPtr)1 };    //x, y, z
            IntPtr[] workGroupSizePtr = new IntPtr[] { (IntPtr)inputImgWidth, (IntPtr)inputImgHeight, (IntPtr)1 };
            error = Cl.Cl.EnqueueWriteImage(cmdQueue, inputImage2DBuffer, Cl.Bool.True,
               originPtr, regionPtr, (IntPtr)0, (IntPtr)0, inputArray, 0, null, out clevent);
            CheckErr(error, "Cl.EnqueueWriteImage");
            //Execute our kernel (OpenCL code)
            error = Cl.Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, workGroupSizePtr, null, 0, null, out clevent);
            CheckErr(error, "Cl.EnqueueNDRangeKernel");
            //Wait for completion of all calculations on the GPU.
            error = Cl.Cl.Finish(cmdQueue);
            CheckErr(error, "Cl.Finish");
            //Read the processed image from GPU to raw RGBA data byte[] array
            error = Cl.Cl.EnqueueReadImage(cmdQueue, outputImage2DBuffer, Cl.Bool.True, originPtr, regionPtr,
                                        (IntPtr)0, (IntPtr)0, outputArray, 0, null, out clevent);
            CheckErr(error, "Cl.clEnqueueReadImage");
            //Clean up memory
            Cl.Cl.ReleaseKernel(kernel);
            Cl.Cl.ReleaseCommandQueue(cmdQueue);

            Cl.Cl.ReleaseMemObject(inputImage2DBuffer);
            Cl.Cl.ReleaseMemObject(outputImage2DBuffer);
        }

        public void Dispose()
        {
            program.Dispose();
        }
    }
}
