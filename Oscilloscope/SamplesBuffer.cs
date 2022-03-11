using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui.Oscilloscope
{
  unsafe class SamplesBuffer
  {
    public readonly float[] Buffer;
    public readonly int StartIdx;
    public readonly int Length;
    public int FilledLength;
    public SamplesBuffer(int BufferLength, int PreambleLength)
    {
      Buffer = new float[BufferLength + PreambleLength];
      StartIdx = PreambleLength;
      Length = BufferLength;
      FilledLength = 0;
    }

    public int AvailableLength
    {
      get
      {
        return Length - FilledLength;
      }
    }

    public int AllocateFillRegionReturnStartIdx(int desiredLength)
    {
      if (desiredLength > AvailableLength)
      {
        throw new ArgumentException("AvailableLength must be checked by the caller");
      }

      var result = StartIdx + FilledLength;
      FilledLength += desiredLength;
      return result;
    }
  }
}
