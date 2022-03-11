using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using bitLab.Log;

namespace loork_gui.Oscilloscope
{
  internal class SerialPortChannel: Channel
  {
    private readonly string mPortName;
    private readonly Int32 mBaudRate;
    private readonly Int32 mDataBits;
    private readonly StopBits mStopBits;
    private readonly Parity mParity;
    private SerialPort mPort;

    public SerialPortChannel(int samplesPerSecond, string portName): base(samplesPerSecond)
    {
      mPortName = portName;
      mBaudRate = 115200;
      mDataBits = 8;
      mStopBits = StopBits.One;
      mParity = Parity.None;
    }

    public bool TryOpen()
    {
      try
      {
        mPort = new SerialPort(mPortName, mBaudRate, mParity, mDataBits, mStopBits);
        mPort.NewLine = "\n";
        mPort.DataReceived += port_DataReceived;
        mPort.Open();
        return true;
      }
      catch (Exception ex)
      {
        Logger.LogError($"Could not open serial port {mPortName}: " + ex.Message);
        return false;
      }
    }

    private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
      //dataReadCallback(port.ReadExisting());
      
    }

    public override void Capture(float secondsPassed, SamplesBuffer bufferToFill, out int samplesCaptured)
    {
      var bytesToRead = mPort.BytesToRead;
      if (bytesToRead % 2 == 1)
      {
        bytesToRead -= 1;
      }
      if (bytesToRead < 2)
      {
        samplesCaptured = 0;
        return;
      }

      var inputBuffer = new byte[4000 * 2]; // Buffer for 4k samples
      bytesToRead = Math.Min(bytesToRead, inputBuffer.Length);

      var readBytes = mPort.Read(inputBuffer, 0, bytesToRead);
      if (readBytes != bytesToRead)
      {
        throw new InvalidOperationException("Less bytes recevived than expected");
      }

      samplesCaptured = bytesToRead / 2;
      //if (bufferToFill.StartIdx + samplesCaptured > bufferToFill.Buffer.Length)
      if (samplesCaptured > bufferToFill.AvailableLength)
      {
        //throw new ArgumentException("Too much time passed, not enough buffer");
        Console.WriteLine("Overflow");
        samplesCaptured = bufferToFill.AvailableLength;
      }

      unsafe
      {
        var startIdx = bufferToFill.AllocateFillRegionReturnStartIdx(samplesCaptured);
        fixed (float* bufferStart = &bufferToFill.Buffer[startIdx])
        {
          var buffer = bufferStart;
          fixed (byte* inputStart = &inputBuffer[0])
          {
            var input = (short*)inputStart;
            var inputEnd = input + samplesCaptured;
            while (input < inputEnd)
            {
              *buffer = (*input) >> 3;
              buffer++;
              input++;
            }
          }
        }
      }
    }
  }
}
