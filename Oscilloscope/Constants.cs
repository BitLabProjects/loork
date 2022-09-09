using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui.Oscilloscope
{
  class Constants
  {
    public const int ScreenWidth = 400;
    public const int ScreenHeight = 240;

    public const float RefreshIntervalInSec = 1.0f / 30.0f;
    public const int SamplesPerSecond = 44100;
    public const int MarginTopBottom = 10;
    public const int MaxSignalValue = 4096;
    public static readonly int[] TimebaseSteps = new int[] {
      1, 2, 5,
      10, 20, 50,
      100, 200, 500,
      1 *1000, 2*1000, 5*1000,
      //The following values require a different algorithm for the rendering, as it can't display a whole wave in the same capture 
      //10*1000, 20*1000, 50*1000, 
      //100*1000, 200*1000, 500*1000};
    };
  }
}
