using LibUsbDotNet;
using LibUsbDotNet.Main;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DbiBackendGui
{
    /// <summary>
    /// Rewrite dbibackend.py to C#, using LibUsbDotnet
    /// </summary>
    public class Dbi
    {
        private static readonly int CMD_ID_EXIT = 0;
        private static readonly int CMD_ID_LIST_OLD = 1;
        private static readonly int CMD_ID_FILE_RANGE = 2;
        private static readonly int CMD_ID_LIST = 3;

        private static readonly int CMD_TYPE_REQUEST = 0;
        private static readonly int CMD_TYPE_RESPONSE = 1;
        private static readonly int CMD_TYPE_ACK = 2;

        private static readonly int BUFFER_SEGMENT_DATA_SIZE = 0x100000;

        private static readonly byte[] MAGIC_HEAD_BYTES = Encoding.ASCII.GetBytes("DBI0");

        private static UsbDeviceFinder switchFinder = new(0x057E, 0x3000);
        private UsbDevice? _switchDevice;

        private UsbEndpointReader? _readEp;
        private UsbEndpointWriter? _writeEp;

        private static byte[] memBuffer = new byte[4096];

        private List<string> logs = new List<string>();

        public Dbi()
        {
        }

        private void Log(string message)
        {
            Debug.WriteLine(message);
            logs.Add(message);
        }

        private void WriteMessageHeader(int cmdType, int cmdId, int data)
        {
            var buf = new byte[16];
            Buffer.BlockCopy(MAGIC_HEAD_BYTES, 0, buf, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(cmdType), 0, buf, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(cmdId), 0, buf, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(data), 0, buf, 12, 4);
            _writeEp!.Write(buf, 5000, out _);
        }

        public void StartServer()
        {
            ConnectToSwitch();
            PollCommands();
        }

        public void ProcessExitCommand()
        {
            Log("Exit.");
            WriteMessageHeader(CMD_TYPE_RESPONSE, CMD_ID_EXIT, 0);
        }

        public void ProcessFileRangeCommand(int fileSize)
        {
            Log("File range");
            WriteMessageHeader(CMD_TYPE_ACK, CMD_ID_FILE_RANGE, fileSize);

            _readEp!.Read(memBuffer, 5000, out int len);
            var rangeSize = BitConverter.ToInt32(memBuffer, 0);
            var rangeOffset = BitConverter.ToInt64(memBuffer, 4);
            var nspNameLen = BitConverter.ToInt32(memBuffer, 12);
            var nspName = Encoding.UTF8.GetString(memBuffer, 16, len - 16);

            WriteMessageHeader(CMD_TYPE_RESPONSE, CMD_ID_FILE_RANGE, rangeSize);

            var buf = new byte[16];
            _readEp!.Read(buf, 5000, out _);

            // 开始写入文件
        }

        public void ProcessListCommand()
        {
            var files = new List<string>() { "c:/aaa.nsp", "c:/bbb.nsp" };

            var str = "";
            foreach (var fileName in files)
            {
                str += fileName + '\n';
            }

            WriteMessageHeader(CMD_TYPE_RESPONSE, CMD_ID_LIST, str.Length);

            if (str.Length > 0)
            {
                var buf = new byte[16];
                _readEp!.Read(buf, 5000, out _);
                _writeEp!.Write(Encoding.UTF8.GetBytes(str), 5000, out _);
            }
        }

        public void PollCommands()
        {
            Log("Entering command loop.");
            ErrorCode ec = ErrorCode.None;
            byte[] buf = new byte[1024];
            while (true)
            {
                try
                {
                    ec = _readEp.Read(buf, 5000, out int len);
                    if (Encoding.ASCII.GetString(buf, 0, 4) != "DBI0") continue;
                    var cmdType = BitConverter.ToInt32(buf, 4);
                    var cmdId = BitConverter.ToInt32(buf, 8);
                    var dataSize = BitConverter.ToInt32(buf, 12);

                    if (cmdId == CMD_ID_EXIT)
                    {
                        ProcessExitCommand();
                        break;
                    }
                    if (cmdId == CMD_ID_FILE_RANGE) ProcessFileRangeCommand(dataSize);
                    if (cmdId == CMD_ID_LIST) ProcessListCommand();
                }
                catch (Exception)
                {
                    Log("Connection lost.");
                    ConnectToSwitch();
                }
            }
            _switchDevice.Close();
        }

        public void ConnectToSwitch()
        {
            try
            {
                var allDevices = LegacyUsbRegistry.DeviceList;
                foreach (LegacyUsbRegistry registry in allDevices)
                {
                    if (registry.Vid == 0x057E && registry.Pid == 0x3000)
                    {
                        registry.Open(out _switchDevice);
                        break;
                    }
                }

                if (_switchDevice == null) throw new Exception("Device Not Found.");
                IUsbDevice? wholeUsbDevice = _switchDevice as IUsbDevice;
                if (wholeUsbDevice is not null)
                {
                    // This is a "whole" USB device. Before it can be used, 
                    // the desired configuration and interface must be selected.

                    // Select config #1
                    wholeUsbDevice.SetConfiguration(1);

                    // Claim interface #0.
                    wholeUsbDevice.ClaimInterface(0);
                }

                _readEp = _switchDevice.OpenEndpointReader(ReadEndpointID.Ep01);
                _writeEp = _switchDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
