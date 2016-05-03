using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui
{
  class Channel
  {
    private int[] mBuffer;
    private int[] mWave;

    private int mSamplesPerSecond;
    private int mSampleCount;
    private Random r = new Random();

    private byte[] mReaderBuffer;
    private Mp3FileReader mAudioReader;

    public Channel(int samplesPerSecond)
    {
      mSamplesPerSecond = samplesPerSecond;

      //Allocate a buffer of one second
      mBuffer = new int[samplesPerSecond];
      mReaderBuffer = new byte[samplesPerSecond*4];

      var signalFrequency = 1000.0f;
      var signalsSamplesPerPeriod = (int)(SamplesPerSecond / signalFrequency);
      mWave = new int[signalsSamplesPerPeriod];
      for (int i = 0; i < mWave.Length; i++)
      {
        float t = (float)(i) / mWave.Length;
        mWave[i] = (int)(2048 + 1800 * Math.Sin(2 * Math.PI * t));
      }

      //mAudioReader = new Mp3FileReader("..\\..\\in_the_raw.mp3");
      mAudioReader = new Mp3FileReader("..\\..\\chirp.mp3");
    }

    public int SamplesPerSecond { get { return mSamplesPerSecond; } }

    public int[] Capture(float secondsPassed, out int samplesCaptured)
    {
      samplesCaptured = (int)(secondsPassed * mSamplesPerSecond);
      if (samplesCaptured > mBuffer.Length)
      {
        //throw new ArgumentException("Too much time passed, not enough buffer");
        Console.WriteLine("Overflow");
        samplesCaptured = mBuffer.Length;
      }

      //Capture_AudioFile(samplesCaptured);
      Capture_SineWave(samplesCaptured);

      return mBuffer;
    }

    public void Capture_AudioFile(int samplesCaptured)
    {
      if (mAudioReader.Position > mAudioReader.Length - samplesCaptured * 4)
        mAudioReader.Position = 0;
      //*4 because the audio is 2 channel 16 bit
      mAudioReader.Read(mReaderBuffer, 0, samplesCaptured*4);
      unsafe
      {
        fixed (int* bufferStart = mBuffer)
        {
          int* buffer = bufferStart;
          int* bufferEnd = bufferStart + samplesCaptured;

          fixed (byte* waveStart = mReaderBuffer)
          {
            Int16* waveEnd = (Int16*)(waveStart + mReaderBuffer.Length);
            Int16* wave = (Int16*)(waveStart);

            while (buffer < bufferEnd)
            {
              var sample = *wave++;// (*wave++) | (*wave++) << 8;
              sample = (Int16)(sample / 64);
              //System.IO.File.AppendAllText("C:\\temp\\out.txt", wave + ",");
              wave += 1;
              *buffer++ = 2048 + (sample);
              //*buffer++ = 2048 + (sample);
              //*buffer++ = 2048 + (sample);
            }
          }
        }
      }
    }

    public void Capture_SineWave(int samplesCaptured)
    {
      unsafe
      {
        fixed (int* bufferStart = mBuffer)
        {
          int* buffer = bufferStart;
          int* bufferEnd = bufferStart + samplesCaptured;

          fixed (int* waveStart = mWave)
          {
            int* waveEnd = waveStart + mWave.Length;

            while (buffer < bufferEnd)
            {
              int* wave = waveStart;
              while ((wave < waveEnd) && (buffer < bufferEnd))
              {
                *buffer++ = (int)(*wave++ + (-50 + r.Next() % 100));
              }
            }
          }
        }
      }
    }
  }
}
