using System;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace loork_gui
{
  class SurfaceVM: INotifyPropertyChanged
  {
    private WriteableBitmap mFractalImage;
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
      mFractalImage.Lock();

      unsafe
      {
        var bytesPerPixel = (mFractalImage.Format.BitsPerPixel / 8);
        var ptr = (byte*)mFractalImage.BackBuffer;
        ptr += rY * mFractalImage.BackBufferStride + rX * bytesPerPixel;

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
          ptr += mFractalImage.BackBufferStride - rWidth * bytesPerPixel;
        }
      }
      mFractalImage.AddDirtyRect(new System.Windows.Int32Rect(rX, rY, rWidth, rHeight));
      mFractalImage.Unlock();

      Notify("FractalImage");
    }

    public BitmapSource FractalImage
    {
      get
      {
        return mFractalImage;
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

      mFractalImage = bmp;
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
