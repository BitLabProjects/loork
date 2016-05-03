﻿using System;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace loork_gui
{
  class SurfaceVM: INotifyPropertyChanged
  {
    private WriteableBitmap mImage;
    private int mWidth, mHeight;
    private Func<int, int, byte> mCalcPixel;

    public SurfaceVM(int width, int height, Func<int, int, byte> CalcPixel)
    {
      mWidth = width;
      mHeight = height;
      mCalcPixel = CalcPixel;

      InitializeBitmap();
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
            byte val = mCalcPixel(x, y);

            *ptr = val;
            ptr++;
            *ptr = val;
            ptr++;
            *ptr = val;
            ptr++;
          }
          ptr += mImage.BackBufferStride - rWidth * bytesPerPixel;
        }
      }
      mImage.AddDirtyRect(new System.Windows.Int32Rect(rX, rY, rWidth, rHeight));
      mImage.Unlock();

      Notify("FractalImage");
    }

    public BitmapSource Image
    {
      get
      {
        return mImage;
      }
    }

    private void InitializeBitmap()
    {
      var bmp = new WriteableBitmap(mWidth, mHeight, 96, 96, System.Windows.Media.PixelFormats.Rgb24, null);
      bmp.Lock();

      unsafe
      {
        var ptr = (byte*)bmp.BackBuffer;

        for (int y = 0; y < mHeight; y++)
        {
          for (int x = 0; x < mWidth; x++)
          {
            *ptr = 0;
            ptr++;
            *ptr = 0;
            ptr++;
            *ptr = 0;
            ptr++;
          }
        }
      }
      bmp.AddDirtyRect(new System.Windows.Int32Rect(0, 0, mWidth, mHeight));
      bmp.Unlock();

      mImage = bmp;
      Notify("FractalImage");
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void Notify(string propName)
    {
      if (PropertyChanged != null)
      {
        PropertyChanged(this, new PropertyChangedEventArgs(propName));
      }
    }
  }
}
