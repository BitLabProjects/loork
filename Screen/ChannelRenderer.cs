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
    const byte drawOpacity = 1; //(byte)(0.05 * 255); // 30%;

    private readonly byte* mScreenPtrStart;
    private readonly byte* mScreenPtrEnd;
    private readonly int mScreenWidth;
    private readonly int mScreenHeight;
    private readonly Oscilloscope.SignalYScaling mSignalScalingToViewport;

    public ChannelRenderer(byte* screenPtrStart,int screenWidth, int screenHeight, Oscilloscope.SignalYScaling SignalScalingToViewport)
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
                     float signalScaleWidth, float offsetX, 
                     int marginTopBottom)
    {
      var samplesPtr = samplesPtrStart;
      var scaleFactor = 1.0f / mSignalScalingToViewport.maxAverageDifference;
      var prevConditionedSample = ((*samplesPtr++) - mSignalScalingToViewport.averageValue) * scaleFactor * (mScreenHeight / 2 - marginTopBottom * 2) + mScreenHeight / 2;
      var x = 0;
      while (samplesPtr < samplesPtrEnd)
      {
        var currConditionedSample = ((*samplesPtr++) - mSignalScalingToViewport.averageValue) * scaleFactor * (mScreenHeight / 2 - marginTopBottom * 2) + mScreenHeight / 2;
        x++;
        LineClipX((int)((x - 1) * signalScaleWidth + offsetX), 
                  (int)prevConditionedSample, 
                  (int)(x * signalScaleWidth + offsetX), 
                  (int)currConditionedSample);
        prevConditionedSample = currConditionedSample;
      }
    }

    //Assume:
    //1) x1 < x2
    //2) Only one of x1 and x2 can be outside
    private void LineClipX(int x1, int y1,
                           int x2, int y2)
    {
      if (x1 < 0)
      {
        y1 = y1 + (y2 - y1) * (0 - x1) / (x2 - x1);
        x1 = 0;
      }
      else if (x2 >= mScreenWidth)
      {
        y2 = y1 + (y2 - y1) * (mScreenWidth-1 - x1) / (x2 - x1);
        x2 = mScreenWidth-1;
      }

      if (y1 < 0 || y1 >= mScreenHeight || y2 < 0 || y2 >= mScreenHeight)
        return;

      Line(x1, y1, x2, y2);
    }

    private void Line(int x1, int y1, 
                      int x2, int y2)
    {
      var swap = false;
      var DX = x2 - x1;
      var DY = y2 - y1;

      //siccome scambio DY e DX ho sempre DX>=DY allora per sapere quale coordinata occorre cambiare uso una variabile
      if (Math.Abs(DX) < Math.Abs(DY))
      {
        //swap(DX, DY);
        var tmp = DX;
        DX = DY;
        DY = tmp;
        swap = true;
      }

      //per non scrivere sempre i valori assoluti cambio DY e DX con altre variabili
      var a = 2 * Math.Abs(DY);
      var b = -Math.Abs(DX);

      //il nostro valore d0
      var d = a + b;
      var dOverflowIncrement = a + 2 * b;

      //s e q sono gli incrementi/decrementi di x e y
      var xIncrement = mScreenHeight;
      var yIncrement = 1;
      if (x1 > x2) xIncrement = -xIncrement;
      if (y1 > y2) yIncrement = -yIncrement;

      var pixelPtrIncrement = swap ? yIncrement : xIncrement;

      //assegna le coordinate iniziali
      var pixelPtr = mScreenPtrStart + x1 * mScreenHeight + y1;
      //draw pixel
      var value = *pixelPtr;
      *pixelPtr = value < 255 ? (byte)(value + drawOpacity) : (byte)255;

      for (var k = 0; k < -b-1; k += 1)
      {
        if (d > 0)
        {
          pixelPtr += xIncrement + yIncrement;
          d += dOverflowIncrement;
        }
        else
        {
          pixelPtr += pixelPtrIncrement;
          d += a;
        }

#if DEBUG
        if (k == -b - 1)
          Debug.Assert(pixelPtr == mScreenPtrStart + x2 * mScreenHeight + y2);
        Debug.Assert(pixelPtr >= mScreenPtrStart && pixelPtr < mScreenPtrEnd);
#endif

        //draw pixel
        value = *pixelPtr;
        //*pixelPtr = value > drawOpacity ? (byte)(value - drawOpacity) : (byte)0;
        *pixelPtr = value < 255 ? (byte)(value + drawOpacity) : (byte)255;
      }
    }
  }
}
