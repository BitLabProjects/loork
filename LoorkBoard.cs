﻿using System;
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
    private int samplesInScreenWidth;
    private int trigger;
    private float signalScaleWidth;
    private float signalScaleHeight;

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
      MicrosecondsPerDivision = 100;


      mChannel = new Channel(SamplesPerSecond);
      isCounterStarted = false;
      counter = new QueryPerfCounter();

      //Use a safe minimum buffer of double the screen width
      //TODO Calculate proper size based on current visualized Sec/Division
      var samplesPerMicrosecond = (SamplesPerSecond / 1000000.0f);
      samplesInScreenWidth = (int)Math.Ceiling(samplesPerMicrosecond * MicrosecondsPerDivision * 10);
      if (samplesInScreenWidth % 2 > 0)
        samplesInScreenWidth++;

      mSignalAnalyzer = new SignalAnalyzer(samplesInScreenWidth / 2, samplesInScreenWidth / 2);

      mTimer = new System.Threading.Timer(mTimer_Tick, null, (int)(refreshIntervalInSec * 1000), (int)(refreshIntervalInSec * 1000));
    }

    private byte intensity = 0;
    private bool isCounterStarted;
    private Channel mChannel;
    private QueryPerfCounter counter;
    private bool isWorking = false;
    private SignalAnalyzer mSignalAnalyzer;

    public double TriggerPercent { get; set; }
    public int MicrosecondsPerDivision { get; set; }

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
      signalScaleHeight = (screenHeight - 2 * marginTopBottom) / (float)maxSignalValue;
      trigger = (int)(TriggerPercent / 100 * maxSignalValue);
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
                          marginTopBottom);
          });

          var conditionedTrigger = (int)(trigger * signalScaleHeight + marginTopBottom);
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
