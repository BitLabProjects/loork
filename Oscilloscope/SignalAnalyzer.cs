using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui.Oscilloscope
{
  class SignalYScaling
  {
    public float minY;
    public float maxY;

    public void Set(float minY, float maxY)
    {
      this.minY = minY;
      this.maxY = maxY;
    }

    public float SignalOffsetY => minY;
    public float SignalScaleY => 1.0f / (maxY - minY);
  }

  enum SignalAnalysisResult
  {
    NoAnalysisNeeded = 0,
    AnalysisInProgress = 1,
    AnalysisCompleted = 2,
  }

  unsafe class SignalAnalyzer
  {
    private SamplesBuffer mSamplesBuffer1;
    private SamplesBuffer mSamplesBuffer2;
    private SamplesBuffer mLastSamplesBuffer;
    private int mSamplesPerWindow;
    private int mConditioningSamplesCount;
    private float mConditioningMinValue;
    private float mConditioningMaxValue;
    private SignalYScaling mSignalScalingToViewport;

    public SignalAnalyzer(int samplesPerWindow,
                          int samplesPerSecond,
                          int bufferSize)
    {
      mSamplesBuffer1 = new SamplesBuffer(bufferSize, 0);
      mSamplesBuffer2 = new SamplesBuffer(bufferSize, 0);
      mLastSamplesBuffer = mSamplesBuffer2;//Start with buffer 2 so the buffer 1 is the first one filled
      mSamplesPerWindow = samplesPerWindow;
      TriggerSample = 0;
      mConditioningSamplesCount = samplesPerSecond;
      mConditioningMinValue = float.MaxValue;
      mConditioningMaxValue = float.MinValue;
      mSignalScalingToViewport = new SignalYScaling();
      mSignalScalingToViewport.Set(-1, +1);
    }

    public float TriggerSample;

    public SignalYScaling SignalScalingToViewport => mSignalScalingToViewport;
    public bool IsAnalyzing => mConditioningSamplesCount > 0;

    public int SamplesPerWindow { get { return mSamplesPerWindow; } }

    public SamplesBuffer GetBufferToFill()
    {
      if (mLastSamplesBuffer == mSamplesBuffer1)
        return mSamplesBuffer2;
      else
        return mSamplesBuffer1;
    }

    public void SwitchBuffers()
    {
      mLastSamplesBuffer = GetBufferToFill();
      GetBufferToFill().FilledLength = 0;
    }

    public SignalAnalysisResult AnalyzeSamples(int samplesCount)
    {
      // If we're not done with auto conditioning calc, do it
      if (mConditioningSamplesCount > 0)
      {
        var filledBuffer = GetBufferToFill();
        //Start searching for next trigger
        fixed (float* samplesPtrStart = &filledBuffer.Buffer[filledBuffer.StartIdx])
        {
          var lastSamplesPtrForConditioning = samplesPtrStart + Math.Min(samplesCount, mConditioningSamplesCount);
          var samplesPtrCond = samplesPtrStart;
          while (samplesPtrCond != lastSamplesPtrForConditioning)
          {
            var sample = *samplesPtrCond;
            mConditioningMinValue = Math.Min(sample, mConditioningMinValue);
            mConditioningMaxValue = Math.Max(sample, mConditioningMaxValue);

            samplesPtrCond++;
            mConditioningSamplesCount--;
            if (mConditioningSamplesCount == 0)
            {
              // TODO Choose a predefined scale based on min-max value
              mSignalScalingToViewport.Set(mConditioningMinValue, mConditioningMaxValue);
              TriggerSample = (mConditioningMaxValue + mConditioningMinValue) * 0.5f;
              return SignalAnalysisResult.AnalysisCompleted;
            }
          }
        }
        return SignalAnalysisResult.AnalysisInProgress;
      }

      return SignalAnalysisResult.NoAnalysisNeeded;
    }

    //Called when a trigger sample has been found
    //The trigger search algorithm calls the callback only when the TriggerSamplesBeforeCount and TriggerSamplesAfterCount are satisfied
    //The callback code can go forward and backward safely on the pointer by the requested samples count
    public delegate void OnTriggerCallbackDelegate(float* samplePtr, float offsetPercent);

    public void InputSamples(int samplesCount, OnTriggerCallbackDelegate onTriggerCallback)
    {
      // TODO Support horizontal trigger
      var triggerSamplesBeforeCount = mSamplesPerWindow / 2;
      var triggerSamplesAfterCount = triggerSamplesBeforeCount;

      var filledBuffer = GetBufferToFill();
      //Start searching for next trigger
      fixed (float* samplesPtrStart = &filledBuffer.Buffer[filledBuffer.StartIdx])
      {
        // Stop before the end of available samples, because we need enough samples to draw on screen
        var lastPtrToSearchTrigger = samplesPtrStart + samplesCount - triggerSamplesAfterCount - 1;

        var samplesPtr = samplesPtrStart + triggerSamplesBeforeCount;
        //Search the first sample below the trigger point
        while (samplesPtr != lastPtrToSearchTrigger)
        {
          if (*samplesPtr < TriggerSample)
          {
            break;
          }
          samplesPtr++;
        }

        while (samplesPtr != lastPtrToSearchTrigger)
        {
          if (*samplesPtr > TriggerSample)
          {
            //Calulate offset between previous sample and current sample of the exact trigger location, in percent of sample duration
            var sample = *samplesPtr;
            var prevSample = *(samplesPtr - 1);
            //-0.5f found by empirical programming
            var offsetPercent = ((float)TriggerSample - prevSample) / (sample - prevSample) - 0.5f;

            onTriggerCallback(samplesPtr, offsetPercent);

            //Discard samples still above the trigger, to avoid triggering by mistake.
            while (samplesPtr != lastPtrToSearchTrigger)
            {
              if (*samplesPtr < TriggerSample)
              {
                break;
              }
              samplesPtr++;
            }
          }
          else
          {
            samplesPtr++;
          }
        }
      }
    }
  }
}
