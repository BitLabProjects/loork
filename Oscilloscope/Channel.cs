using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace loork_gui.Oscilloscope
{
  public class Channel
  {
    public Channel()
    {
    }

    public delegate SamplesBuffer BufferFilledHandler(Channel sender, SamplesBuffer e);
    public event BufferFilledHandler BufferFilledEvent;
    protected SamplesBuffer RaiseBufferFilledEvent(SamplesBuffer samplesBuffer)
    {
      if (BufferFilledEvent != null)
      {
        return BufferFilledEvent(this, samplesBuffer);
      } else
      {
        return null;
      }
    }

    public virtual int SamplesPerSecond { get; }
    public virtual int NominalSamplesPerBufferFill { get; }
  }

  public class AudioChannel : Channel
  {
    private byte[] mReaderBuffer;
    private Mp3FileReader mAudioReader;
    private Timer mTimer;
    private bool mTimerIsWorking;
    //private int[] mWave;
    private SamplesBuffer mSamplesBuffer;

    const float RefreshIntervalInSec = 1.0f / 30.0f;
    const int Period = (int)(RefreshIntervalInSec * 1000);

    public AudioChannel(string fullFileName) : base()
    {
      //var signalFrequency = 1000.0f;
      //var signalsSamplesPerPeriod = (int)(SamplesPerSecond / signalFrequency);
      //mWave = new int[signalsSamplesPerPeriod];
      //for (int i = 0; i < mWave.Length; i++)
      //{
      //  float t = (float)(i) / mWave.Length;
      //  mWave[i] = (int)(2048 + 1800 * Math.Sin(2 * Math.PI * t));
      //}

      mAudioReader = new Mp3FileReader(fullFileName);
      //mAudioReader = new Mp3FileReader("..\\..\\chirp.mp3");
      mReaderBuffer = new byte[mAudioReader.Mp3WaveFormat.SampleRate * 4];
      mTimer = new Timer(mTimer_Tick, null, Period, Period);
    }

    public override int SamplesPerSecond => mAudioReader.Mp3WaveFormat.SampleRate;
    public override int NominalSamplesPerBufferFill => (int)(SamplesPerSecond * RefreshIntervalInSec);

    private void mTimer_Tick(object state)
    {
      if (mTimerIsWorking)
      {
        Console.WriteLine("Reentrancy avoided");
        return;
      }
      mTimerIsWorking = true;

      if (mSamplesBuffer != null)
      {
        AudioCapture(mSamplesBuffer);
      }

      mSamplesBuffer = RaiseBufferFilledEvent(mSamplesBuffer);

      mTimerIsWorking = false;
    }

    public void AudioCapture(SamplesBuffer bufferToFill)
    {
      var samplesCaptured = Math.Min(NominalSamplesPerBufferFill, bufferToFill.AvailableLength);

      if (samplesCaptured == 0)
      {
        //throw new ArgumentException("Too much time passed, not enough buffer");
        Console.WriteLine("Overflow");
        return;
      }

      if (mAudioReader.Position > mAudioReader.Length - samplesCaptured * 4)
        mAudioReader.Position = 0;
      //*4 because the audio is 2 channel 16 bit
      var readCount = mAudioReader.Read(mReaderBuffer, 0, samplesCaptured * 4);
      unsafe
      {
        var startIdx = bufferToFill.AllocateFillRegionReturnStartIdx(samplesCaptured);
        fixed (float* bufferStart = &bufferToFill.Buffer[startIdx])
        {
          var buffer = bufferStart;
          var bufferEnd = bufferStart + samplesCaptured;

          fixed (byte* waveStart = mReaderBuffer)
          {
            Int16* waveEnd = (Int16*)(waveStart + mReaderBuffer.Length);
            Int16* wave = (Int16*)(waveStart);

            while (buffer < bufferEnd)
            {
              var sample = *wave++;
              sample = (Int16)(sample / 64);
              wave += 1;
              *buffer++ = 2048 + (sample);
            }
          }
        }
      }
    }

    /*
    private long mWaveInitialOffset;
    public void Capture_SineWave(SamplesBuffer bufferToFill, int samplesCaptured)
    {
      unsafe
      {
        fixed (float* bufferStart = &bufferToFill.Buffer[bufferToFill.StartIdx])
        {
          var buffer = bufferStart;
          var bufferEnd = bufferStart + samplesCaptured;

          fixed (int* waveStart = mWave)
          {
            int* waveEnd = waveStart + mWave.Length;

            var isFirst = true;
            while (buffer < bufferEnd)
            {
              int* wave = waveStart;
              if (isFirst)
              {
                wave += mWaveInitialOffset;
                isFirst = false;
              }

              while ((wave < waveEnd) && (buffer < bufferEnd))
              {
                *buffer++ = (int)(*wave++ + (-50 + r.Next() % 100));
              }
              mWaveInitialOffset = wave - waveStart;
            }
          }
        }
      }
    }
    */
  }
}
