using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui
{
  unsafe class HudRenderer
  {
    private byte* mScreenPtrStart;
    private int mScreenWidth;
    private int mScreenHeight;

    public HudRenderer(byte* screenPtrStart, int screenWidth, int screenHeight)
    {
      mScreenPtrStart = screenPtrStart;
      mScreenWidth = screenWidth;
      mScreenHeight = screenHeight;
    }

    public void Clear()
    {
      var size = mScreenWidth * mScreenHeight * 3;
      var screenPtrEnd = mScreenPtrStart + size;

      //Blank screen
      var screenPtr = mScreenPtrStart;
      while (screenPtr++ < screenPtrEnd)
        *screenPtr = 0;
    }

    public void Line(int x1, int y1, 
                     int x2, int y2,
                     byte red, byte green, byte blue)
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
      var xIncrement = mScreenHeight * 3;
      var yIncrement = 3;
      if (x1 > x2) xIncrement = -xIncrement;
      if (y1 > y2) yIncrement = -yIncrement;

      var pixelPtrIncrement = swap ? yIncrement : xIncrement;

      //assegna le coordinate iniziali
      var pixelPtr = mScreenPtrStart + (x1 * mScreenHeight + y1) * 3;
      //draw pixel
      var tmpPtr = pixelPtr;
      *tmpPtr++ = red;
      *tmpPtr++ = green;
      *tmpPtr++ = blue;

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
          System.Diagnostics.Debug.Assert(pixelPtr == mScreenPtrStart + (x2 * mScreenHeight + y2) * 3);
#endif

        //draw pixel
        tmpPtr = pixelPtr;
        *tmpPtr++ = red;
        *tmpPtr++ = green;
        *tmpPtr++ = blue;
      }
    }
  }
}
