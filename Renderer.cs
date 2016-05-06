using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui
{
  unsafe class Renderer
  {
    const byte drawOpacity = (byte)(0.15 * 255); // 30%;

    private byte* mScreenPtrStart;
    private int mScreenWidth;
    private int mScreenHeight;

    public Renderer(byte* screenPtrStart, int screenWidth, int screenHeight)
    {
      mScreenPtrStart = screenPtrStart;
      mScreenWidth = screenWidth;
      mScreenHeight = screenHeight;
    }

    public void Clear()
    {
      var size = mScreenWidth * mScreenHeight;
      var screenPtrEnd = mScreenPtrStart + size;

      //Blank screen
      var screenPtr = mScreenPtrStart;
      while (screenPtr++ < screenPtrEnd)
        *screenPtr = 230;
    }

    public void Plot(int* samplesPtrStart, int* samplesPtrEnd, float signalScale, int marginTopBottom)
    {
      var samplesPtr = samplesPtrStart;
      var prevConditionedSample = (*samplesPtr++) * signalScale + marginTopBottom;
      var x = 0;
      while (samplesPtr < samplesPtrEnd - 1)
      {
        var currConditionedSample = (*samplesPtr++) * signalScale + marginTopBottom;
        x++;
        Line(x - 1, (int)prevConditionedSample, x, (int)currConditionedSample);
        prevConditionedSample = currConditionedSample;
      }
    }

    public void Line(int x1, int y1, int x2, int y2)
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
      *pixelPtr = value > drawOpacity ? (byte)(value - drawOpacity) : (byte)0;

      for (var k = 0; k < -b; k += 1)
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
          System.Diagnostics.Debug.Assert(pixelPtr == mScreenPtrStart + x2 * mScreenHeight + y2);
#endif

        //draw pixel
        value = *pixelPtr;
        *pixelPtr = value > drawOpacity ? (byte)(value - drawOpacity) : (byte)0;
      }
    }
  }
}
