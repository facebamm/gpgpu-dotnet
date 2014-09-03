using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Cl = OpenCL.Net;

namespace OpenCLManagedLibrary
{
    public class OpenCLWrapper : IDisposable
    {
        private Cl.Context _context;
        private Cl.Device _device;
        private Cl.Program _program;
        private Cl.ErrorCode _error;
        private string programPath = @"C:\Work\GPGPU\SDP\OpenCL\DotNetInterop\OpenCLManagedLibrary\square_array.cl";

        public string DeviceName
        {
            get
            {
                var name = Cl.Cl.GetDeviceInfo(_device, Cl.DeviceInfo.Name, out _error);
                CheckErr(_error, "Cl.GetDeviceInfo");

                return name.ToString();
            }
        }
        public string DeviceType
        {
            get
            {
                var type = Cl.Cl.GetDeviceInfo(_device, Cl.DeviceInfo.Type, out _error).CastTo<Cl.DeviceType>();
                CheckErr(_error, "Cl.GetDeviceInfo");

                return type.ToString();
            }
        }
        public string OpenCLVersion
        {
            get
            {
                var version = Cl.Cl.GetDeviceInfo(_device, Cl.DeviceInfo.Version, out _error);
                CheckErr(_error, "Cl.GetDeviceInfo");

                return version.ToString();
            }
        }

        public OpenCLWrapper()
        {
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
            Cl.Platform[] platforms = Cl.Cl.GetPlatformIDs(out _error);
            List<Cl.Device> devicesList = new List<Cl.Device>();

            CheckErr(_error, "Cl.GetPlatformIDs");

            foreach (Cl.Platform platform in platforms)
            {
                string platformName = Cl.Cl.GetPlatformInfo(platform, Cl.PlatformInfo.Name, out _error).ToString();
                Console.WriteLine("Platform: " + platformName);
                CheckErr(_error, "Cl.GetPlatformInfo");

                foreach (Cl.Device device in Cl.Cl.GetDeviceIDs(platform, Cl.DeviceType.All, out _error))
                {
                    CheckErr(_error, "Cl.GetDeviceIDs");
                    Console.WriteLine("Device: " + device.ToString());
                    devicesList.Add(device);
                }
            }

            if (devicesList.Count <= 0)
            {
                Console.WriteLine("No devices found.");
                return;
            }

            foreach (var device in devicesList)
            {
                var type = Cl.Cl.GetDeviceInfo(device, Cl.DeviceInfo.Type, out _error).CastTo<Cl.DeviceType>();
                CheckErr(_error, "Cl.GetDeviceIDs");

                //We will be looking only for GPU devices in Release mode
#if DEBUG
                if (type == Cl.DeviceType.Cpu)
#else
                if (type == Cl.DeviceType.Gpu)
#endif
                {
                    _device = device;
                    break;
                }
            }

            _context = Cl.Cl.CreateContext(null, 1, new[] { _device }, ContextNotify, IntPtr.Zero, out _error);    //Second parameter is amount of devices
            CheckErr(_error, "Cl.CreateContext");
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

            _program = Cl.Cl.CreateProgramWithSource(_context, 1, new[] { programSource }, null, out error);
            CheckErr(error, "Cl.CreateProgramWithSource");

            //Compile kernel source
#if DEBUG
            error = Cl.Cl.BuildProgram(_program, 0, null, string.Format("-g -s \"{0}\"", programPath), null, IntPtr.Zero);
#else
            error = Cl.Cl.BuildProgram(_program, 1, new[] { _device }, string.Empty, null, IntPtr.Zero);
#endif
            CheckErr(error, "Cl.BuildProgram");

            //Check for any compilation errors
            if (Cl.Cl.GetProgramBuildInfo(_program, _device, Cl.ProgramBuildInfo.Status, out error).CastTo<Cl.BuildStatus>()
                != Cl.BuildStatus.Success)
            {
                CheckErr(error, "Cl.GetProgramBuildInfo");
                Console.WriteLine("Cl.GetProgramBuildInfo != Success");
                Console.WriteLine(Cl.Cl.GetProgramBuildInfo(_program, _device, Cl.ProgramBuildInfo.Log, out error));

                return;
            }
        }

        public void SquareArray(float[] array)
        {
            Cl.ErrorCode error;

            //Create the required kernel (entry function)
            Cl.Kernel kernel = Cl.Cl.CreateKernel(_program, "square_array", out error);
            CheckErr(error, "Cl.CreateKernel");

            int intPtrSize = 0;
            intPtrSize = Marshal.SizeOf(typeof(IntPtr));

            var arrayBuffer = Cl.Cl.CreateBuffer<float>(_context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadWrite, array, out error);
            CheckErr(error, "Cl.CreateBuffer plaintext_bytes");

            //Pass the memory buffers to our kernel function
            error = Cl.Cl.SetKernelArg(kernel, 0, (IntPtr)intPtrSize, arrayBuffer);
            CheckErr(error, "Cl.SetKernelArg");

            //Create a command queue, where all of the commands for execution will be added
            Cl.CommandQueue cmdQueue = Cl.Cl.CreateCommandQueue(_context, _device, (Cl.CommandQueueProperties)0, out error);
            CheckErr(error, "Cl.CreateCommandQueue");
            Cl.Event clevent;

            IntPtr[] workGroupSizePtr = new IntPtr[] { (IntPtr)array.Length };
            //Execute our kernel (OpenCL code)
            error = Cl.Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, workGroupSizePtr, null, 0, null, out clevent);
            CheckErr(error, "Cl.EnqueueNDRangeKernel");

            //Wait for completion of all calculations on the GPU.
            error = Cl.Cl.Finish(cmdQueue);
            CheckErr(error, "Cl.Finish");

            // Read the buffer from memory
            error = Cl.Cl.EnqueueReadBuffer(cmdQueue, arrayBuffer, Cl.Bool.True, array, 0, null, out clevent);
            CheckErr(error, "Cl.EnqueueReadBuffer");

            //Clean up memory
            Cl.Cl.ReleaseKernel(kernel);
            Cl.Cl.ReleaseCommandQueue(cmdQueue);
            Cl.Cl.ReleaseMemObject(arrayBuffer);
        }

        public void Dispose()
        {
            _program.Dispose();
        }
    }
}
