using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui.Screen
{
  internal class RenderUtils
  {
    //Assume:
    //1) x1 < x2
    //2) Only one of x1 and x2 can be outside
    public static bool LineClipX(ref int x1, ref int y1,
                                 ref int x2, ref int y2,
                                 int mScreenWidth, int mScreenHeight)
    {
      double x1d = x1;
      double x2d = x2;
      double y1d = y1;
      double y2d = y2;
      var result = CohenSutherlandLineClip(ref x1d, ref y1d, ref x2d, ref y2d, 0, mScreenWidth - 1, 0, mScreenHeight - 1);
      x1 = (int)x1d;
      x2 = (int)x2d;
      y1 = (int)y1d;
      y2 = (int)y2d;
      return result;
    }

    enum OutCode
    {
      Inside = 0, // 0000
      Left = 1,   // 0001
      Right = 2,  // 0010
      Bottom = 4, // 0100
      Top = 8     // 1000
    }

    private static OutCode ComputeOutCode(double x, double y, int xmin, int xmax, int ymin, int ymax)
    {
      OutCode code = OutCode.Inside;  // initialised as being inside of clip window

      if (x < xmin)             // to the left of clip window
        code |= OutCode.Left;
      else if (x > xmax)        // to the right of clip window
        code |= OutCode.Right;
      if (y < ymin)             // below the clip window
        code |= OutCode.Bottom;
      else if (y > ymax)        // above the clip window
        code |= OutCode.Top;

      return code;
    }

    // Cohen–Sutherland clipping algorithm clips a line from
    // P0 = (x0, y0) to P1 = (x1, y1) against a rectangle with 
    // diagonal from (xmin, ymin) to (xmax, ymax).
    private static bool CohenSutherlandLineClip(ref double x0, ref double y0, ref double x1, ref double y1, int xmin, int xmax, int ymin, int ymax)
    {
      // compute outcodes for P0, P1, and whatever point lies outside the clip rectangle
      OutCode outcode0 = ComputeOutCode(x0, y0, xmin, xmax, ymin, ymax);
      OutCode outcode1 = ComputeOutCode(x1, y1, xmin, xmax, ymin, ymax);
      bool accept = false;

      while (true)
      {
        if ((outcode0 | outcode1) == OutCode.Inside)
        {
          // bitwise OR is 0: both points inside window; trivially accept and exit loop
          accept = true;
          break;
        }
        else if ((outcode0 & outcode1) != OutCode.Inside)
        {
          // bitwise AND is not 0: both points share an outside zone (LEFT, RIGHT, TOP,
          // or BOTTOM), so both must be outside window; exit loop (accept is false)
          break;
        }
        else
        {
          // failed both tests, so calculate the line segment to clip
          // from an outside point to an intersection with clip edge
          double x, y;

          // At least one endpoint is outside the clip rectangle; pick it.
          OutCode outcodeOut = outcode1 > outcode0 ? outcode1 : outcode0;

          // Now find the intersection point;
          // use formulas:
          //   slope = (y1 - y0) / (x1 - x0)
          //   x = x0 + (1 / slope) * (ym - y0), where ym is ymin or ymax
          //   y = y0 + slope * (xm - x0), where xm is xmin or xmax
          // No need to worry about divide-by-zero because, in each case, the
          // outcode bit being tested guarantees the denominator is non-zero
          if ((outcodeOut & OutCode.Top) != OutCode.Inside)
          {           // point is above the clip window
            x = x0 + (x1 - x0) * (ymax - y0) / (y1 - y0);
            y = ymax;
          }
          else if ((outcodeOut & OutCode.Bottom) != OutCode.Inside)
          { // point is below the clip window
            x = x0 + (x1 - x0) * (ymin - y0) / (y1 - y0);
            y = ymin;
          }
          else if ((outcodeOut & OutCode.Right) != OutCode.Inside)
          {  // point is to the right of clip window
            y = y0 + (y1 - y0) * (xmax - x0) / (x1 - x0);
            x = xmax;
          }
          else if ((outcodeOut & OutCode.Left) != OutCode.Inside)
          {   // point is to the left of clip window
            y = y0 + (y1 - y0) * (xmin - x0) / (x1 - x0);
            x = xmin;
          } else
          {
            return false;
          }

          // Now we move outside point to intersection point to clip
          // and get ready for next pass.
          if (outcodeOut == outcode0)
          {
            x0 = x;
            y0 = y;
            outcode0 = ComputeOutCode(x0, y0, xmin, xmax, ymin, ymax);
          }
          else
          {
            x1 = x;
            y1 = y;
            outcode1 = ComputeOutCode(x1, y1, xmin, xmax, ymin, ymax);
          }
        }
      }
      return accept;
    }

    public unsafe static void LinePlot(int x1, int y1,
                                       int x2, int y2,
                                       int mScreenHeight,
                                       byte* mScreenPtrStart,
                                       byte* mScreenPtrEnd,
                                       byte drawOpacity)
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

      for (var k = 0; k < -b - 1; k += 1)
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

    public static unsafe void LineRGB(int x1, int y1,
                                      int x2, int y2,
                                      byte red, byte green, byte blue,
                                      int mScreenHeight, byte* mScreenPtrStart, byte* mScreenPtrEnd)
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

      for (var k = 0; k < -b - 1; k += 1)
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
        Debug.Assert(pixelPtr >= mScreenPtrStart && pixelPtr < mScreenPtrEnd);
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
