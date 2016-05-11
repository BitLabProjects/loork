using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui.Oscilloscope
{
  class Channel
  {
    private int[] mWave;

    private int mSamplesPerSecond;
    private int mSampleCount;
    private Random r = new Random();

    private byte[] mReaderBuffer;
    private Mp3FileReader mAudioReader;

    public Channel(int samplesPerSecond)
    {
      mSamplesPerSecond = samplesPerSecond;

      mReaderBuffer = new byte[samplesPerSecond*4];

      var signalFrequency = 1000.0f;
      var signalsSamplesPerPeriod = (int)(SamplesPerSecond / signalFrequency);
      mWave = new int[signalsSamplesPerPeriod];
      for (int i = 0; i < mWave.Length; i++)
      {
        float t = (float)(i) / mWave.Length;
        mWave[i] = (int)(2048 + 1800 * Math.Sin(2 * Math.PI * t));
      }

      mAudioReader = new Mp3FileReader("..\\..\\in_the_raw.mp3");
      //mAudioReader = new Mp3FileReader("..\\..\\chirp.mp3");
    }

    public int SamplesPerSecond { get { return mSamplesPerSecond; } }

    public void Capture(float secondsPassed, SamplesBuffer bufferToFill, out int samplesCaptured)
    {
      samplesCaptured = (int)(secondsPassed * mSamplesPerSecond);
      if (samplesCaptured > bufferToFill.Length)
      {
        //throw new ArgumentException("Too much time passed, not enough buffer");
        Console.WriteLine("Overflow");
        samplesCaptured = bufferToFill.Length;
      }

      Capture_AudioFile(bufferToFill, samplesCaptured);
      //Capture_SineWave(bufferToFill, samplesCaptured);
    }

    public void Capture_AudioFile(SamplesBuffer bufferToFill, int samplesCaptured)
    {
      if (mAudioReader.Position > mAudioReader.Length - samplesCaptured * 4)
        mAudioReader.Position = 0;
      //*4 because the audio is 2 channel 16 bit
      mAudioReader.Read(mReaderBuffer, 0, samplesCaptured*4);
      unsafe
      {
        fixed (int* bufferStart = &bufferToFill.Buffer[bufferToFill.StartIdx])
        {
          int* buffer = bufferStart;
          int* bufferEnd = bufferStart + samplesCaptured;

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

    private long mWaveInitialOffset;
    public void Capture_SineWave(SamplesBuffer bufferToFill, int samplesCaptured)
    {
      unsafe
      {
        fixed (int* bufferStart = &bufferToFill.Buffer[bufferToFill.StartIdx])
        {
          int* buffer = bufferStart;
          int* bufferEnd = bufferStart + samplesCaptured;

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
  }
}
