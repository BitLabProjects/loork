using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui.Oscilloscope
{
  struct SignalYScaling
  {
    public float averageValue;
    public float maxAverageDifference;
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
    private int mLastSamplesBufferCount;
    private int mTriggerSamplesBeforeCount;
    private int mTriggerSamplesAfterCount;
    private int mConditioningSamplesCount;
    private float mConditioningMinValue;
    private float mConditioningMaxValue;
    private SignalYScaling mSignalScalingToViewport;

    public SignalAnalyzer(int triggerSamplesBeforeCount,
                          int triggerSamplesAfterCount,
                          int samplesPerSecond,
                          int bufferSize)
    {
      mSamplesBuffer1 = new SamplesBuffer(bufferSize, 0);
      mSamplesBuffer2 = new SamplesBuffer(bufferSize, 0);
      mLastSamplesBuffer = mSamplesBuffer2;//Start with buffer 2 so the buffer 1 is the first one filled
      mLastSamplesBufferCount = 0;
      mTriggerSamplesBeforeCount = triggerSamplesBeforeCount;
      mTriggerSamplesAfterCount = triggerSamplesAfterCount;
      TriggerSample = 0;
      mConditioningSamplesCount = samplesPerSecond;
      mConditioningMinValue = float.MaxValue;
      mConditioningMaxValue = float.MinValue;
      mSignalScalingToViewport.averageValue = 0;
      mSignalScalingToViewport.maxAverageDifference = 4096;
    }

    public float TriggerSample;

    public SignalYScaling SignalScalingToViewport => mSignalScalingToViewport;
    public bool IsAnalyzing => mConditioningSamplesCount > 0;

    public int MinSampleCount { get { return mTriggerSamplesBeforeCount + mTriggerSamplesAfterCount; } }

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
              // Actually calculate the offset-scale to bring the signal to -1, +1 interval
              mSignalScalingToViewport.averageValue = (mConditioningMaxValue + mConditioningMinValue) * 0.5f;
              mSignalScalingToViewport.maxAverageDifference = (mConditioningMaxValue - mConditioningMinValue) * 0.5f;
              TriggerSample = mSignalScalingToViewport.averageValue;
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
      var filledBuffer = GetBufferToFill();
      //Start searching for next trigger
      fixed (float* samplesPtrStart = &filledBuffer.Buffer[filledBuffer.StartIdx])
      {
        var lastPtrToSearchTrigger = samplesPtrStart + (samplesCount - mTriggerSamplesAfterCount - 1);

        var samplesPtr = samplesPtrStart;// + mTriggerSamplesBeforeCount;
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
            var skipTrigger = false;
            //You always have mTriggerSamplesAfterCount leftover samples from the previous run (Except for the first one)
            //If a trigger is found before mTriggerSamplesAfterCount samples in this run, copy preamble samples before 
            //the trigger for the display to draw
            var missingSamples = mTriggerSamplesBeforeCount - (samplesPtr - samplesPtrStart);
            if (missingSamples > 0)
            {
              //Skip trigger if no samples to integrate
              if (mLastSamplesBufferCount < missingSamples)
              {
                skipTrigger = true;
              }
              else
              {
                fixed (float* lastSampledStartPtr = &mLastSamplesBuffer.Buffer[mLastSamplesBuffer.StartIdx + mLastSamplesBufferCount - missingSamples])
                {
                  var lastSampledPtr = lastSampledStartPtr;
                  var preamblePtr = samplesPtrStart - missingSamples;
                  while (preamblePtr < samplesPtrStart)
                  {
                    *preamblePtr++ = *lastSampledPtr++;
                  }
                }
              }
            }

            if (!skipTrigger)
            {
              //Calulate offset between previous sample and current sample of the exact trigger location, in percent of sample duration
              var sample = *samplesPtr;
              var prevSample = *(samplesPtr - 1);
              //-0.5f found by empirical programming
              var offsetPercent = ((float)TriggerSample - prevSample) / (sample - prevSample) - 0.5f;

              onTriggerCallback(samplesPtr, offsetPercent);
            }

            //Discard samples still above the trigger, do avoid triggering by mistake.
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
      mLastSamplesBufferCount = 0;
    }
  }
}
