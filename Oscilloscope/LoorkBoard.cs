using bitLab.Log;
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
    private const int screenWidth = 400;
    private const int screenHeight = 240;

    private const float RefreshIntervalInSec = 1.0f / 30.0f;
    private const int SamplesPerSecond = 44100;// (int)(10 * OneMillion);
    private const int MarginTopBottom = 10;
    private const int MaxSignalValue = 4096;

    private byte[] mScreenBuffer;
    private SurfaceVM mSurfaceVM;
    private UserInterfaceVM mUserInterfaceVM;
    private Dispatcher mDispatcher;
    private System.Threading.Timer mTimer;
    private int samplesInScreenWidth;
    private int trigger;
    private float signalScaleWidth;
    private float signalScaleHeight;
    private byte intensity = 0;
    private bool isCounterStarted;
    private Channel mChannel;
    private QueryPerfCounter counter;
    private bool isWorking = false;
    private SignalAnalyzer mSignalAnalyzer;

    public LoorkBoard(Dispatcher dispatcher)
    {
      mDispatcher = dispatcher;
      mScreenBuffer = new byte[screenWidth * screenHeight];
      mSurfaceVM = new SurfaceVM(screenWidth, screenHeight, (x, y) => (byte)(mScreenBuffer[x * screenHeight + y]));
      mUserInterfaceVM = new UserInterfaceVM();

      TriggerPercent = 50;
      MicrosecondsPerDivision = 100;


      mChannel = new Channel(SamplesPerSecond);
      isCounterStarted = false;
      counter = new QueryPerfCounter();

      var samplesPerMicrosecond = (SamplesPerSecond / 1000000.0f);
      samplesInScreenWidth = (int)Math.Ceiling(samplesPerMicrosecond * MicrosecondsPerDivision * 10);
      if (samplesInScreenWidth % 2 > 0)
        samplesInScreenWidth++;

      mSignalAnalyzer = new SignalAnalyzer(samplesInScreenWidth / 2, samplesInScreenWidth / 2);

      mTimer = new System.Threading.Timer(mTimer_Tick, null, (int)(RefreshIntervalInSec * 1000), (int)(RefreshIntervalInSec * 1000));
    }

    public double TriggerPercent { get; set; }
    public int MicrosecondsPerDivision { get; set; }
    public SurfaceVM SurfaceVM { get { return mSurfaceVM; } }
    public UserInterfaceVM UserInterfaceVM { get { return mUserInterfaceVM; } }

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
      signalScaleWidth = screenWidth / (float)(samplesInScreenWidth-1);
      signalScaleHeight = (screenHeight - 2 * MarginTopBottom) / (float)MaxSignalValue;
      trigger = (int)(TriggerPercent / 100 * MaxSignalValue);
      mSignalAnalyzer.TriggerThreshold = trigger;

      unsafe
      {
        int channelSamplesCount;
        mChannel.Capture(elapsedSeconds, mSignalAnalyzer.GetBufferToFill(), out channelSamplesCount);

        fixed (byte* screenPtrStart = mScreenBuffer)
        {
          var renderer = new Renderer(screenPtrStart, screenWidth, screenHeight);
          renderer.Clear();
          mSignalAnalyzer.InputSamples(channelSamplesCount, (int* samplesPtr) =>
          {
            renderer.Plot(samplesPtr - samplesInScreenWidth / 2, 
                          samplesPtr + samplesInScreenWidth / 2, 
                          signalScaleWidth, 
                          signalScaleHeight, 
                          MarginTopBottom);
          });

          var conditionedTrigger = (int)(trigger * signalScaleHeight + MarginTopBottom);
          renderer.Line(0, conditionedTrigger, screenWidth - 1, conditionedTrigger);

          //Grid
          var divisionSize = (int)(screenWidth / 10);
          for (int x = 1; x < 10; x++)
          {
            var repetitions = x == 5 ? 3 : 1;
            for(var rep = 0; rep < repetitions; rep++)
              renderer.Line(x * divisionSize, 0, x*divisionSize, screenHeight);
          }

          var divisionsFittingHeightCount = screenHeight / divisionSize;
          for (int y = 1; y < divisionsFittingHeightCount; y++)
          {
            var repetitions = y == divisionsFittingHeightCount/2 ? 3 : 1;
            for (var rep = 0; rep < repetitions; rep++)
              renderer.Line(0, y * divisionSize, screenWidth,  y * divisionSize);
          }
        }
      }

      try
      {
        mDispatcher.Invoke(() => mSurfaceVM.RefreshAll());
      }
      catch (TaskCanceledException)
      {
        //Well... uh
      }
      isWorking = false;
    }
  }
}
