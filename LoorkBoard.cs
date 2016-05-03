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
    const byte drawOpacity = (byte)(0.15 * 255); // 30%;
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
                var prevConditionedSample = (*samplesPtr++) * signalScale;
                var x = 0;
                while (samplesPtr < samplesPtrEnd - 1)
                {
                  var currConditionedSample = (*samplesPtr++) * signalScale;
                  x++;
                  mLine(screenPtrStart, x - 1, (int)prevConditionedSample, x, (int)currConditionedSample);
                  prevConditionedSample = currConditionedSample;
                }
                /*
                screenPtr = screenPtrStart + marginTopBottom;
                var prevConditionedSample = (*samplesPtr++) * signalScale;
                while (screenPtr < screenPtrEnd - screenHeight)
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

                  screenPtr += screenHeight;
                  prevConditionedSample = currConditionedSample;
                }
                */

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

          mDrawTriggerLine(screenPtrStart, screenPtrEnd);
        }
      }

      mDispatcher.Invoke(() => mSurfaceVM.RefreshAll());
      isWorking = false;
    }

    private unsafe void mLine(byte* screenPtrStart, int x1, int y1, int x2, int y2)
    {
      var swap = 0;
      var DX = x2 - x1;
      var DY = y2 - y1;

      //siccome scambio DY e DX ho sempre DX>=DY allora per sapere quale coordinata occorre cambiare uso una variabile
      if (Math.Abs(DX) < Math.Abs(DY))
      {
        //swap(DX, DY);
        var tmp = DX;
        DX = DY;
        DY = tmp;
        swap = 1;
      }

      //per non scrivere sempre i valori assoluti cambio DY e DX con altre variabili
      var a = Math.Abs(DY);
      var b = -Math.Abs(DX);

      //il nostro valore d0
      var d = 2 * a + b;

      //s e q sono gli incrementi/decrementi di x e y
      var q = 1;
      var s = 1;
      if (x1 > x2) q = -1;
      if (y1 > y2) s = -1;
      //disegna_punto(x, y);
      *(screenPtrStart + x1 * screenHeight + y1) = 0;
      //disegna_punto(x2, y2);
      *(screenPtrStart + x2 * screenHeight + y2) = 0;
      
      //assegna le coordinate iniziali
      var x = x1;
      var y = y1;

      for (var k = 0; k < -b; k += 1)
      {
        if (d > 0)
        {
          x = x + q;
          y = y + s;
          d = d + 2 * (a + b);
        }
        else
        {
          x = x + q;
          if (swap == 1)
          {
            y = y + s;
            x = x - q;
          }
          d = d + 2 * a;
        }
        //disegna_punto(x, y);
        *(screenPtrStart + x * screenHeight + y) = 0;
      }
    }

    private unsafe void mDrawTriggerLine(byte* screenPtrStart, byte* screenPtrEnd)
    {
      var screenPtr = screenPtrStart + marginTopBottom + (int)(trigger * signalScale);
      while (screenPtr < screenPtrEnd)
      {
        *screenPtr = 0;
        screenPtr += screenHeight;
      }
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
