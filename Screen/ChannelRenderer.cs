using loork_gui.Screen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui
{
  unsafe class ChannelRenderer
  {
    const byte drawOpacity = 1; 

    private readonly byte* mScreenPtrStart;
    private readonly byte* mScreenPtrEnd;
    private readonly int mScreenWidth;
    private readonly int mScreenHeight;
    private readonly Oscilloscope.SignalYScaling mSignalScalingToViewport;

    public ChannelRenderer(byte* screenPtrStart, int screenWidth, int screenHeight, Oscilloscope.SignalYScaling SignalScalingToViewport)
    {
      mScreenPtrStart = screenPtrStart;
      mScreenPtrEnd = screenPtrStart + 1 * screenWidth * screenHeight;
      mScreenWidth = screenWidth;
      mScreenHeight = screenHeight;
      mSignalScalingToViewport = SignalScalingToViewport;
    }

    public void Clear()
    {
      var size = mScreenWidth * mScreenHeight;
      var screenPtrEnd = mScreenPtrStart + size;

      //Blank screen
      var screenPtr = mScreenPtrStart;
      while (screenPtr++ < screenPtrEnd)
        *screenPtr = 0;
    }

    public void Plot(float* samplesPtrStart, float* samplesPtrEnd, 
                     float signalScaleWidth, float offsetX)
    {
      var signalOffsetY = mSignalScalingToViewport.SignalOffsetY;
      var scaleY = mSignalScalingToViewport.SignalScaleY * mScreenHeight;
      var samplesPtr = samplesPtrStart;
      var prevConditionedSample = ((*samplesPtr++) - signalOffsetY) * scaleY;
      var x = 0;
      while (samplesPtr < samplesPtrEnd)
      {
        var currConditionedSample = ((*samplesPtr++) - signalOffsetY) * scaleY;
        x++;
        PlotLine((int)((x - 1) * signalScaleWidth + offsetX), 
                 (int)prevConditionedSample, 
                 (int)(x * signalScaleWidth + offsetX), 
                 (int)currConditionedSample);
        prevConditionedSample = currConditionedSample;
      }
    }

    private void PlotLine(int x1, int y1, int x2, int y2)
    {

      if (RenderUtils.LineClipX(ref x1, ref y1, ref x2, ref y2, mScreenWidth, mScreenHeight))
      {
        RenderUtils.LinePlot(x1, y1, x2, y2, mScreenHeight, mScreenPtrStart, mScreenPtrEnd, drawOpacity);
      }
    }
  }
}
