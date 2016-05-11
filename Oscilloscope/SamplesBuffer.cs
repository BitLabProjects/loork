using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui
{
  unsafe class SamplesBuffer
  {
    public readonly int[] Buffer;
    public readonly int StartIdx;
    public readonly int Length;
    public SamplesBuffer(int BufferLength, int PreambleLength)
    {
      Buffer = new int[BufferLength + PreambleLength];
      StartIdx = PreambleLength;
      Length = BufferLength;
    }
  }
}
