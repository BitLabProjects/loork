using bitLab.Log;
using loork_gui.Screen;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace loork_gui.Oscilloscope
{
  unsafe class LoorkBoard
  {
    private byte[] mScreenBufferHud;
    private byte[] mScreenBufferCh1;
    private ScreenSurface mScreenSurface;
    private Dispatcher mDispatcher;
    private Timer mTimer;
    private bool isCounterStarted;
    private Channel mChannel;
    private QueryPerfCounter counter;
    private bool isWorking = false;
    private SignalAnalyzer mSignalAnalyzer;

    //User settings
    private double mTriggerPercent;
    private int mMicrosecondsPerDivision;
    private bool mSettingsChanged;
    //Calculated values from user settings
    private int mSamplesInScreenWidth;
    private float mSignalScaleWidth;
    private float mSignalScaleHeight;

    public LoorkBoard(Dispatcher dispatcher)
    {
      mDispatcher = dispatcher;
      mScreenBufferCh1 = new byte[Constants.ScreenWidth * Constants.ScreenHeight];
      mScreenBufferHud = new byte[Constants.ScreenWidth * Constants.ScreenHeight * 3];
      mScreenSurface = new ScreenSurface(Constants.ScreenWidth, Constants.ScreenHeight, mScreenBufferHud, mScreenBufferCh1);

      mTriggerPercent = 50;
      mMicrosecondsPerDivision = 100;
      mSettingsChanged = true;

      //mChannel = new Channel(Constants.SamplesPerSecond);
      var spc= new SerialPortChannel(100, "COM4");
      spc.TryOpen();
      mChannel = spc;

      isCounterStarted = false;
      counter = new QueryPerfCounter();

      mTimer = new System.Threading.Timer(mTimer_Tick, null, (int)(Constants.RefreshIntervalInSec * 1000), (int)(Constants.RefreshIntervalInSec * 1000));
    }

    #region Properties
    public double TriggerPercent
    {
      get
      {
        return mTriggerPercent;
      }
      set
      {
        mTriggerPercent = value;
        mSettingsChanged = true;
      }
    }
    public int MicrosecondsPerDivision
    {
      get
      {
        return mMicrosecondsPerDivision;
      }
      set
      {
        mMicrosecondsPerDivision = value;
        mSettingsChanged = true;
      }
    }
    #endregion

    public ScreenSurface ScreenSurface { get { return mScreenSurface; } }

    public void NextMicrosecondsPerDivision()
    {
      for (var idxStep = 0; idxStep < Constants.TimebaseSteps.Length; idxStep++)
        if (MicrosecondsPerDivision < Constants.TimebaseSteps[idxStep])
        {
          MicrosecondsPerDivision = Constants.TimebaseSteps[idxStep];
          return;
        }
    }
    public void PrevMicrosecondsPerDivision()
    {
      for (var idxStep = Constants.TimebaseSteps.Length - 1; idxStep > 0; idxStep--)
        if (MicrosecondsPerDivision > Constants.TimebaseSteps[idxStep])
        {
          MicrosecondsPerDivision = Constants.TimebaseSteps[idxStep];
          return;
        }
    }
    private void Create()
    {
      var samplesPerMicrosecond = (Constants.SamplesPerSecond / 1000000.0f);
      mSamplesInScreenWidth = (int)Math.Ceiling(samplesPerMicrosecond * MicrosecondsPerDivision * 10);
      if (mSamplesInScreenWidth % 2 > 0)
        mSamplesInScreenWidth++;

      mSignalScaleWidth = Constants.ScreenWidth / (float)(mSamplesInScreenWidth - 1);
      mSignalScaleHeight = (Constants.ScreenHeight - 2 * Constants.MarginTopBottom) / (float)Constants.MaxSignalValue;

      mSignalAnalyzer = new SignalAnalyzer(mSamplesInScreenWidth / 2, mSamplesInScreenWidth / 2, TriggerPercent);
    }

    private void mRenderHud()
    {
      fixed (byte* screenPtrStartHud = mScreenBufferHud)
      {
        var renderer = new HudRenderer(screenPtrStartHud, Constants.ScreenWidth, Constants.ScreenHeight);
        renderer.Clear();

        var conditionedTrigger = (int)(mSignalAnalyzer.TriggerSample * mSignalScaleHeight + Constants.MarginTopBottom);
        renderer.Line(0, conditionedTrigger, Constants.ScreenWidth - 1, conditionedTrigger,
                      100, 100, 100);

        //Grid
        var divisionSize = (int)(Constants.ScreenWidth / 10);
        for (int x = 1; x < 10; x++)
        {
          var repetitions = x == 5 ? 3 : 1;
          for (var rep = 0; rep < repetitions; rep++)
            renderer.Line(x * divisionSize, 0, x * divisionSize, Constants.ScreenHeight,
                          150, 150, 150);
        }

        var divisionsFittingHeightCount = Constants.ScreenHeight / divisionSize;
        for (int y = 1; y < divisionsFittingHeightCount; y++)
        {
          var repetitions = y == divisionsFittingHeightCount / 2 ? 3 : 1;
          for (var rep = 0; rep < repetitions; rep++)
            renderer.Line(0, y * divisionSize, Constants.ScreenWidth, y * divisionSize,
                          150, 150, 150);
        }
      }
    }

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

      if (mSettingsChanged)
      {
        Create();
        mRenderHud();
        mSettingsChanged = false;
      }

      int channelSamplesCount;
      var samplesBuffer = mSignalAnalyzer.GetBufferToFill();
      mChannel.Capture(elapsedSeconds, samplesBuffer, out channelSamplesCount);

      // TODO Move away from here
      Debug.Assert(samplesBuffer.Length >= mSignalAnalyzer.MinSampleCount, "The buffer must be larger than the minimum samples length");

      if (samplesBuffer.FilledLength >= mSignalAnalyzer.MinSampleCount)
      {
        fixed (byte* screenPtrStartCh1 = mScreenBufferCh1)
        {
          var renderer = new ChannelRenderer(screenPtrStartCh1, Constants.ScreenWidth, Constants.ScreenHeight);
          renderer.Clear();
          mSignalAnalyzer.InputSamples(samplesBuffer.FilledLength, (float* samplesPtr, float offsetPercent) =>
          {
            renderer.Plot(samplesPtr - mSamplesInScreenWidth / 2,
                          samplesPtr + mSamplesInScreenWidth / 2,
                          mSignalScaleWidth,
                          mSignalScaleHeight,
                          -offsetPercent * mSignalScaleWidth,
                          Constants.MarginTopBottom);
          });
        }
      }

      try
      {
        mDispatcher.Invoke(() => mScreenSurface.RefreshAll());
      }
      catch (TaskCanceledException)
      {
        //Well... uh
      }
      isWorking = false;
    }
  }
}
