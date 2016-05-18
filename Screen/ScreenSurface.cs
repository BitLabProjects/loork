using loork_gui.Oscilloscope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace loork_gui.Screen
{
  class ScreenSurface
  {
    private WriteableBitmap mImage;
    private int mWidth, mHeight;
    private byte[] mScreenBufferHud;
    private byte[] mScreenBufferChannel1;

    public ScreenSurface(int width, int height, byte[] screenBufferHud, byte[] screenBufferChannel1)
    {
      mWidth = width;
      mHeight = height;
      mScreenBufferHud = screenBufferHud;
      mScreenBufferChannel1 = screenBufferChannel1;

      InitializeBitmap();
    }

    public BitmapSource Image
    {
      get
      {
        return mImage;
      }
    }

    public void RefreshAll()
    {
      RefreshRectangle(0, 0, mWidth, mHeight);
    }

    private void RefreshRectangle(int rX, int rY, int rWidth, int rHeight)
    {
      mImage.Lock();

      unsafe
      {
        var bytesPerPixel = (mImage.Format.BitsPerPixel / 8);
        var ptr = (byte*)mImage.BackBuffer;
        ptr += rY * mImage.BackBufferStride + rX * bytesPerPixel;

        for (int y = rY; y < rY + rHeight; y++)
        {
          for (int x = rX; x < rX + rWidth; x++)
          {
            var offset = x * mHeight + y;
            byte valHudR = mScreenBufferHud[offset * 3 + 0];
            byte valHudG = mScreenBufferHud[offset * 3 + 1];
            byte valHudB = mScreenBufferHud[offset * 3 + 2];
            int valCh1Int = mScreenBufferChannel1[offset];
            if (valCh1Int != 0)
            {
              var valCh2Int = valCh1Int * 5 + 120;
              if (valCh2Int > 255)
              {
                var valCh3Int = valCh1Int * 5;
                var valCh1 = (byte)(valCh3Int > 100 ? 100 : valCh3Int);
                valHudR = 255;
                valHudG = 255;
                valHudB = valCh1;
              } else
              {
                var valCh1 = (byte)(valCh2Int > 255 ? 255 : valCh2Int);
                valHudR = valCh1;
                valHudG = valCh1;
                valHudB = 0;
              }
            }
            *ptr++ = valHudR;
            *ptr++ = valHudG;
            *ptr++ = valHudB;
          }
          ptr += mImage.BackBufferStride - rWidth * bytesPerPixel;
        }
      }
      mImage.AddDirtyRect(new System.Windows.Int32Rect(rX, rY, rWidth, rHeight));
      mImage.Unlock();
    }

    private void InitializeBitmap()
    {
      var bmp = new WriteableBitmap(mWidth, mHeight, 96, 96, System.Windows.Media.PixelFormats.Rgb24, null);
      bmp.Lock();

      unsafe
      {
        var ptr = (byte*)bmp.BackBuffer;
        var ptrEnd = ptr + mHeight * mWidth * 3;
        while (ptr < ptrEnd)
          *ptr++ = 0;
      }
      bmp.AddDirtyRect(new System.Windows.Int32Rect(0, 0, mWidth, mHeight));
      bmp.Unlock();

      mImage = bmp;
    }
  }
}
