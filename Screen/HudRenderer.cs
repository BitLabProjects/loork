using loork_gui.Screen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui
{
  unsafe class HudRenderer
  {
    private byte* mScreenPtrStart;
    private readonly byte* mScreenPtrEnd;
    private int mScreenWidth;
    private int mScreenHeight;

    public HudRenderer(byte* screenPtrStart, int screenWidth, int screenHeight)
    {
      mScreenPtrStart = screenPtrStart;
      mScreenPtrEnd = screenPtrStart + 3 * screenWidth * screenHeight;
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

    public void LineRGB(int x1, int y1, int x2, int y2, byte red, byte green, byte blue)
    {

      if (RenderUtils.LineClipX(ref x1, ref y1, ref x2, ref y2, mScreenWidth, mScreenHeight))
      {
        RenderUtils.LineRGB(x1, y1, x2, y2, red, green, blue, mScreenHeight, mScreenPtrStart, mScreenPtrEnd);
      }
    }
  }
}
