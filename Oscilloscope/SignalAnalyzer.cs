using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui.Oscilloscope
{
  unsafe class SignalAnalyzer
  {
    private SamplesBuffer mSamplesBuffer1;
    private SamplesBuffer mSamplesBuffer2;
    private SamplesBuffer mLastSamplesBuffer;
    private int mLastSamplesBufferCount;
    private int mTriggerSamplesBeforeCount;
    private int mTriggerSamplesAfterCount;
    private int mTriggerSample;

    public SignalAnalyzer(int triggerSamplesBeforeCount,
                          int triggerSamplesAfterCount,
                          double triggerPercent)
    {
      const int BufferSize = 4000;
      mSamplesBuffer1 = new SamplesBuffer(BufferSize, triggerSamplesBeforeCount);
      mSamplesBuffer2 = new SamplesBuffer(BufferSize, triggerSamplesBeforeCount);
      mLastSamplesBuffer = mSamplesBuffer2;//Start with buffer 2 so the buffer 1 is the first one filled
      mLastSamplesBufferCount = 0;
      mTriggerSamplesBeforeCount = triggerSamplesBeforeCount;
      mTriggerSamplesAfterCount = triggerSamplesAfterCount;
      mTriggerSample = (int)(triggerPercent / 100 * Constants.MaxSignalValue);
    }

    public SamplesBuffer GetBufferToFill()
    {
      if (mLastSamplesBuffer == mSamplesBuffer1)
        return mSamplesBuffer2;
      else
        return mSamplesBuffer1;
    }

    //Called when a trigger sample has been found
    //The trigger search algorithm calls the callback only when the TriggerSamplesBeforeCount and TriggerSamplesAfterCount are satisfied
    //The callback code can go forward and backward safely on the pointer by the requested samples count
    public delegate void OnTriggerCallbackDelegate(int* samplePtr);

    public void InputSamples(int samplesCount, OnTriggerCallbackDelegate onTriggerCallback)
    {
      var filledBuffer = GetBufferToFill();
      //Start searching for next trigger
      fixed (int* samplesPtrStart = &filledBuffer.Buffer[filledBuffer.StartIdx])
      {
        var lastPtrToSearchTrigger = samplesPtrStart + (samplesCount - mTriggerSamplesAfterCount - 1);

        var samplesPtr = samplesPtrStart;// + mTriggerSamplesBeforeCount;
        //Avoid triggering if already above
        while (samplesPtr != lastPtrToSearchTrigger)
        {
          if (*samplesPtr < mTriggerSample)
          {
            break;
          }
          samplesPtr++;
        }

        while (samplesPtr != lastPtrToSearchTrigger)
        {
          if (*samplesPtr > mTriggerSample)
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
                fixed (int* lastSampledStartPtr = &mLastSamplesBuffer.Buffer[mLastSamplesBuffer.StartIdx + mLastSamplesBufferCount - missingSamples])
                {
                  var lastSampledPtr = lastSampledStartPtr;
                  var preamblePtr = samplesPtrStart - missingSamples;
                  while(preamblePtr < samplesPtrStart)
                  {
                    *preamblePtr++ = *lastSampledPtr++;
                  }
                }
              }
            }

            if (!skipTrigger)
              onTriggerCallback(samplesPtr);

            //Discard samples still above the trigger, do avoid triggering by mistake.
            //TODO mLastTriggerSampleIdx = (mLastTriggerSampleIdx + mTriggerSamplesAfterCount) % mSamples.Length;
            while (samplesPtr != lastPtrToSearchTrigger)
            {
              if (*samplesPtr < mTriggerSample)
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
      mLastSamplesBuffer = filledBuffer;
      mLastSamplesBufferCount = samplesCount;
    }
  }
}
