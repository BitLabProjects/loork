using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace loork_gui
{
  class LoorkBoard
  {
    private SerialPort mPort;
    private const int screenWidth = 800;
    private const int screenHeight = 600;
    private bool mSyncReceived;
    private byte[] mScreenBuffer;
    private int mScreenBufferIdx;
    private SurfaceVM mSurfaceVM;
    private Dispatcher mDispatcher;
    private System.Threading.Timer mTimer;

    const float refreshIntervalInSec = 1.0f / 30.0f;
    const int OneMillion = 1000000;
    const int SamplesPerSecond = 44100;// (int)(10 * OneMillion);
    const int trigger = 2048;

    public LoorkBoard(Dispatcher dispatcher)
    {
      mDispatcher = dispatcher;
      mScreenBuffer = new byte[screenWidth * screenHeight];
      mScreenBufferIdx = 0;
      //mSurfaceVM = new SurfaceVM(screenWidth, screenHeight, (x, y) => (byte)(mScreenBuffer[x * screenHeight + y] == 0 ? 255 : 0));
      mSurfaceVM = new SurfaceVM(screenWidth, screenHeight, (x, y) => (byte)(mScreenBuffer[x * screenHeight + y]));
      mSyncReceived = false;

      //mPort = new SerialPort("COM3", 115200, Parity.None, 8, StopBits.One);
      //mPort.DataReceived += mPort_DataReceived;
      //mPort.Open();


      mChannel = new Channel(SamplesPerSecond);
      isCounterStarted = false;
      counter = new QueryPerfCounter();

      mTimer = new System.Threading.Timer(mTimer_Tick, null, (int)(refreshIntervalInSec * 1000), (int)(refreshIntervalInSec * 1000));
    }

    private byte intensity = 0;
    private bool isCounterStarted;
    private Channel mChannel;
    private QueryPerfCounter counter;
    private bool isWorking = false;

    private void mTimer_Tick(object state)
    {
      if (!isCounterStarted)
      {
        counter.Start();
        isCounterStarted = true;
        return;
      }

      if (isWorking)
      {
        Console.WriteLine("Reentrancy avoided");
        return;
      }
      isWorking = true;

      counter.Stop();
      var elapsedSeconds = (float)(counter.Duration(1) / 1000000000.0f);
      counter.Start();

      //Console.WriteLine("{0:0.0}", elapsedSeconds * 1000);

      intensity = (byte)((intensity + 1) % 255);
      const byte drawOpacity = (byte)(0.15 * 255); // 30%;

      unsafe
      {
        fixed (byte* screenPtrStart = mScreenBuffer)
        {
          var size = screenWidth * screenHeight;
          var screenPtrEnd = screenPtrStart + size;

          //Blank screen
          var screenPtr = screenPtrStart;
          while (screenPtr++ < screenPtrEnd)
            *screenPtr = 255;

          int channelSamplesCount;
          var channelSamples = mChannel.Capture(elapsedSeconds, out channelSamplesCount);

          fixed (int* samplesStart = channelSamples)
          {
            var samplesEnd = samplesStart + screenWidth / 2 + (channelSamplesCount - screenWidth / 2);
            var samplesPtr = samplesStart + screenWidth / 2;

            //Wait for un-trigger
            if (*samplesPtr >= trigger)
            {
              while (samplesPtr < samplesEnd)
              {
                if (*samplesPtr++ < trigger)
                  break;
              }
            }

            var prevSample = *samplesPtr;
            while (samplesPtr < samplesEnd)
            {
              var sample = *samplesPtr++;
              if (sample >= trigger && Math.Abs(sample - prevSample - 100) < 5)
              {
                samplesPtr -= screenWidth / 2;
                screenPtr = screenPtrStart + 50;
                var signalScale = (screenHeight - 100) / 4096f;
                var prevConditionedSample = (*samplesPtr++) * signalScale;
                while (screenPtr < screenPtrEnd)
                {
                  var currConditionedSample = (*samplesPtr++) * signalScale;
                  var delta = currConditionedSample - prevConditionedSample;
                  var currPtr = screenPtr + (int)prevConditionedSample;
                  var halfWayPtr = currPtr + (int)(delta / 2);
                  var ptrDelta = delta > 0 ? 1 : -1;
                  while (currPtr != halfWayPtr)
                  {
                    var currValue = *currPtr;
                    *currPtr = currValue > drawOpacity ? (byte)(currValue - drawOpacity) : (byte)0;
                    currPtr += ptrDelta;
                  }
                  currPtr += screenHeight;
                  var endPtr = screenPtr + screenHeight + (int)currConditionedSample;
                  while (currPtr != endPtr)
                  {
                    var currValue = *currPtr;
                    *currPtr = currValue > drawOpacity ? (byte)(currValue - drawOpacity) : (byte)0;
                    currPtr += ptrDelta;
                  }
                  //Last point (or only point if prevCondSample==currCondSample
                  var value = *currPtr;
                  *currPtr = value > drawOpacity ? (byte)(value - drawOpacity) : (byte)0;
                  //*(screenPtr + (int)currConditionedSample) = 0;

                  screenPtr += screenHeight;
                  prevConditionedSample = currConditionedSample;
                }

                while (samplesPtr < samplesEnd)
                {
                  if (*samplesPtr++ < trigger)
                    break;
                }
                sample = *(samplesPtr - 1);
              }
              prevSample = sample;
            }
          }
        }
      }

      mDispatcher.Invoke(() => mSurfaceVM.RefreshAll());
      isWorking = false;
    }

    public SurfaceVM SurfaceVM { get { return mSurfaceVM; } }

    private void mPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
      var bytesToRead = Math.Min(mPort.BytesToRead, mScreenBuffer.Length - mScreenBufferIdx);
      mScreenBufferIdx += mPort.Read(mScreenBuffer, mScreenBufferIdx, bytesToRead);

      if (!mSyncReceived)
      {
        for (int i = 0; i < mScreenBufferIdx; i++)
          if (mScreenBuffer[i] == 85)
          {
            var j = 0;
            i++;
            while (i < mScreenBufferIdx)
              mScreenBuffer[j++] = mScreenBuffer[i++];

            mScreenBufferIdx = j;

            mSyncReceived = true;
            break;
          }
        if (!mSyncReceived)
        {
          mScreenBufferIdx = 0;
        }
      }

      if (mScreenBufferIdx == mScreenBuffer.Length)
      {
        mDispatcher.Invoke(() => mSurfaceVM.RefreshAll());
        mScreenBufferIdx = 0;
        mSyncReceived = false;
      }
    }
  }
}
