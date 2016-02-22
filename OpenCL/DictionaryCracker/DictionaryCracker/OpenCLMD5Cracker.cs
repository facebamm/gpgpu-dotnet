using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Cl = OpenCL.Net;

namespace DictionaryCracker
{
    public class OpenCLMD5Cracker : IDisposable, IMD5Cracker
    {
        private Cl.Context _context;
        private Cl.Device _device;
        private Cl.Program program;
        private string programPath = "C:\\Work\\GPGPU\\SDP\\OpenCL\\DictionaryCracker\\DictionaryCracker\\md5match.cl";
        private const int KEY_LENGTH = 16;

        public OpenCLMD5Cracker()
        {
            programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\md5match.cl");
            if(!File.Exists(programPath))
                programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"md5match.cl");
            if (!File.Exists(programPath))
                throw new FileNotFoundException(programPath);
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

                //We will be looking only for GPU devices in Release mode
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

        public void Warmup()
        {
            string[] strings = { "1234", "12345", "123456", "1234567" };
            var match = Crack(strings.ToList(), "827ccb0eea8a706c4c34a16891f84e7b");
        }

        public string Crack(List<string> plaintext, string target)
        {
            string match = "";

            byte[] plaintext_bytes = new byte[plaintext.Count * KEY_LENGTH];
            byte[] plaintext_lengths = new byte[plaintext.Count];
            byte[] target_bytes = new byte[KEY_LENGTH];
            byte[] match_bytes = null;

            int encoded;

            for (int i = 0; i < plaintext.Count; i++)
            {
                encoded = Encoding.Default.GetBytes(plaintext[i], 0, plaintext[i].Length, plaintext_bytes, KEY_LENGTH * i);
                plaintext_lengths[i] = (byte)encoded;
            }

            var hexBytes = HexHelper.StringToByteArray(target);
            for (int i = 0; i < hexBytes.Length; i++)
                target_bytes[i] = hexBytes[i];

            CrackImpl(plaintext_bytes, plaintext_lengths, target_bytes, out match_bytes);
            int end;
            for (end = 0; end < match_bytes.Length && match_bytes[end] != 0; end++) ;

            match = Encoding.Default.GetString(match_bytes, 0, end);

            return match;
        }

        private void CrackImpl(byte[] plaintext_bytes, byte[] plaintext_lengths, byte[] target, out byte[] match)
        {
            Cl.ErrorCode error;
            match = null;

            //Create the required kernel (entry function)
            Cl.Kernel kernel = Cl.Cl.CreateKernel(program, "md5", out error);
            CheckErr(error, "Cl.CreateKernel");

            int intPtrSize = 0;
            intPtrSize = Marshal.SizeOf(typeof(IntPtr));

            var plaintextBytesBuffer = Cl.Cl.CreateBuffer(_context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly, (IntPtr)plaintext_bytes.Length, plaintext_bytes, out error);
            CheckErr(error, "Cl.CreateBuffer plaintext_bytes");

            var plaintextLengthsBuffer = Cl.Cl.CreateBuffer(_context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly, (IntPtr)plaintext_lengths.Length, plaintext_lengths, out error);
            CheckErr(error, "Cl.CreateBuffer plaintext_lengths");

            var targetBuffer = Cl.Cl.CreateBuffer(_context, Cl.MemFlags.CopyHostPtr | Cl.MemFlags.ReadOnly, (IntPtr)KEY_LENGTH, target, out error);
            CheckErr(error, "Cl.CreateBuffer target");

            match = new byte[KEY_LENGTH];
            var matchBuffer = Cl.Cl.CreateBuffer<byte>(_context, Cl.MemFlags.WriteOnly | Cl.MemFlags.CopyHostPtr, match, out error);
            CheckErr(error, "Cl.CreateBuffer match");

            //Pass the memory buffers to our kernel function
            error = Cl.Cl.SetKernelArg(kernel, 0, (IntPtr)intPtrSize, plaintextBytesBuffer);
            error |= Cl.Cl.SetKernelArg(kernel, 1, (IntPtr)intPtrSize, plaintextLengthsBuffer);
            error |= Cl.Cl.SetKernelArg(kernel, 2, (IntPtr)intPtrSize, targetBuffer);
            error |= Cl.Cl.SetKernelArg(kernel, 3, (IntPtr)intPtrSize, matchBuffer);
            CheckErr(error, "Cl.SetKernelArg");

            //Create a command queue, where all of the commands for execution will be added
            Cl.CommandQueue cmdQueue = Cl.Cl.CreateCommandQueue(_context, _device, (Cl.CommandQueueProperties)0, out error);
            CheckErr(error, "Cl.CreateCommandQueue");
            Cl.Event clevent;

            IntPtr[] workGroupSizePtr = new IntPtr[] { (IntPtr)(plaintext_bytes.Length / KEY_LENGTH) };
            //Execute our kernel (OpenCL code)
            error = Cl.Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, workGroupSizePtr, null, 0, null, out clevent);
            CheckErr(error, "Cl.EnqueueNDRangeKernel");
            //Wait for completion of all calculations on the GPU.
            error = Cl.Cl.Finish(cmdQueue);
            CheckErr(error, "Cl.Finish");

            error = Cl.Cl.EnqueueReadBuffer(cmdQueue, matchBuffer, Cl.Bool.True, match, 0, null, out clevent);
            CheckErr(error, "Cl.EnqueueReadBuffer");
            //Clean up memory
            Cl.Cl.ReleaseKernel(kernel);
            Cl.Cl.ReleaseCommandQueue(cmdQueue);

            Cl.Cl.ReleaseMemObject(plaintextBytesBuffer);
            Cl.Cl.ReleaseMemObject(plaintextLengthsBuffer);
            Cl.Cl.ReleaseMemObject(targetBuffer);
            Cl.Cl.ReleaseMemObject(matchBuffer);
        }

        public void Dispose()
        {
            program.Dispose();
        }


        public string Name
        {
            get { return "OpenCL MD5 Cracker"; }
        }

        public string Device
        {
            get
            {
                Cl.ErrorCode error;
                var name = Cl.Cl.GetDeviceInfo(_device, Cl.DeviceInfo.Name, out error);
                var type = Cl.Cl.GetDeviceInfo(_device, Cl.DeviceInfo.Type, out error).CastTo<Cl.DeviceType>();

                return string.Format("{0}: {1}", type, name);
            }
        }
    }
}
