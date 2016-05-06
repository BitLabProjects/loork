using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui
{
  unsafe class SignalAnalyzer
  {
    private int[] mSamplesBuffer1;
    private int[] mSamplesBuffer2;
    private int[] mLastSamplesBuffer;
    private int mLastSamplesBufferCount;
    private int mLastSamplesBufferRemainingCount;
    private int mTriggerSamplesBeforeCount;
    private int mTriggerSamplesAfterCount;

    public SignalAnalyzer(int triggerSamplesBeforeCount, int triggerSamplesAfterCount)
    {
      const int BufferSize = 4000;
      mSamplesBuffer1 = new int[BufferSize];
      mSamplesBuffer2 = new int[BufferSize];
      mLastSamplesBuffer = mSamplesBuffer2;//Start with buffer 2 so the buffer 1 is the first one filled
      mLastSamplesBufferCount = 0;
      mLastSamplesBufferRemainingCount = 0;
      mTriggerSamplesBeforeCount = triggerSamplesBeforeCount;
      mTriggerSamplesAfterCount = triggerSamplesAfterCount;
      TriggerThreshold = 2048;
    }

    public int TriggerThreshold;

    public int[] GetBufferToFill()
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
      fixed (int* samplesPtrStart = filledBuffer)
      {
        var lastPtrToSearchTrigger = samplesPtrStart + (samplesCount - mTriggerSamplesAfterCount - 1);

        //TODO Recover unused samples of previous scan using mLastSamplesBufferRemainingCount
        var samplesPtr = samplesPtrStart + mTriggerSamplesBeforeCount;
        //Avoid triggering if already above
        while (samplesPtr != lastPtrToSearchTrigger)
        {
          if (*samplesPtr < TriggerThreshold)
          {
            break;
          }
          samplesPtr++;
        }

        while (samplesPtr != lastPtrToSearchTrigger)
        {
          if (*samplesPtr > TriggerThreshold)
          {
            onTriggerCallback(samplesPtr);
            //Discard samples still above the trigger, do avoid triggering by mistake.
            //TODO mLastTriggerSampleIdx = (mLastTriggerSampleIdx + mTriggerSamplesAfterCount) % mSamples.Length;
            while (samplesPtr != lastPtrToSearchTrigger)
            {
              if (*samplesPtr < TriggerThreshold)
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
      mLastSamplesBufferRemainingCount = 0;
    }
  }
}
