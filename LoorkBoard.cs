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
    private const int screenWidth = 400;
    private const int screenHeight = 240;
    private bool mSyncReceived;
    private byte[] mScreenBuffer;
    private int mScreenBufferIdx;
    private SurfaceVM mSurfaceVM;
    private Dispatcher mDispatcher;
    private System.Threading.Timer mTimer;

    const float refreshIntervalInSec = 1.0f / 30.0f;
    const int OneMillion = 1000000;
    const int SamplesPerSecond = 44100;// (int)(10 * OneMillion);
    const int marginTopBottom = 10;
    const int maxSignalValue = 4096;
    private int trigger;
    private float signalScale;

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
      TriggerPercent = 50;


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

    public double TriggerPercent { get; set; }

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
      signalScale = (screenHeight - 2 * marginTopBottom) / (float)maxSignalValue;
      trigger = (int)(TriggerPercent / 100 * maxSignalValue);

      unsafe
      {
        fixed (byte* screenPtrStart = mScreenBuffer)
        {
          var renderer = new Renderer(screenPtrStart, screenHeight);
          var size = screenWidth * screenHeight;
          var screenPtrEnd = screenPtrStart + size;

          //Blank screen
          var screenPtr = screenPtrStart;
          while (screenPtr++ < screenPtrEnd)
            *screenPtr = 230;

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
              if (sample >= trigger)// && Math.Abs(sample - prevSample - 100) < 5)
              {
                samplesPtr -= screenWidth / 2;
                var samplesPtrEnd = samplesPtr + screenWidth;
                var prevConditionedSample = (*samplesPtr++) * signalScale + marginTopBottom;
                var x = 0;
                while (samplesPtr < samplesPtrEnd - 1)
                {
                  var currConditionedSample = (*samplesPtr++) * signalScale + marginTopBottom;
                  x++;
                  renderer.Line(x - 1, (int)prevConditionedSample, x, (int)currConditionedSample);
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

          //mDrawTriggerLine(screenPtrStart, screenPtrEnd);
          var conditionedTrigger = (int)(trigger * signalScale + marginTopBottom);
          renderer.Line(0, conditionedTrigger, screenWidth - 1, conditionedTrigger);
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
