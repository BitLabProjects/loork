using bitLab.ViewModel;
using loork_gui.Oscilloscope;
using loork_gui.Screen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loork_gui
{
  public enum Functionality
  {
    TriggerLevel,
    VoltsPerDivision,
    MicrosecondsPerDivision,
  }

  class UserInterfaceVM: CBaseVM
  {
    private LoorkBoard mBoard;

    public UserInterfaceVM(LoorkBoard board)
    {
      mBoard = board;
      mBoard.OnSignalAnalysis += () => Notify(nameof(IsAnalyzingSignal));
      SetTriggerLevelFunctionality = new CDelegateCommand(mSetTriggerLevelFunctionality);
      SetMicrosecondsPerDivisionFunctionality = new CDelegateCommand(mSetMicrosecondsPerDivisionFunctionality);
      UpdateValueFromKnob = new CDelegateCommand(mUpdateValueFromKnob);
      mPrevUpdateValueFromKnobDate = DateTime.Now;
    }

    #region Properties
    private Functionality mCurrentFunctionality;
    public Functionality CurrentFunctionality
    {
      get { return mCurrentFunctionality; }
      set { SetAndNotify(ref mCurrentFunctionality, value); }
    }
    public ScreenSurface ScreenSurface { get { return mBoard.ScreenSurface; } }
    public bool IsAnalyzingSignal => mBoard.IsAnalyzingSignal;
    #endregion

    #region Commands
    public CDelegateCommand SetTriggerLevelFunctionality { get; private set; }
    private void mSetTriggerLevelFunctionality(object arg)
    {
      CurrentFunctionality = Functionality.TriggerLevel;
    }

    public CDelegateCommand SetMicrosecondsPerDivisionFunctionality { get; private set; }
    private void mSetMicrosecondsPerDivisionFunctionality(object arg)
    {
      CurrentFunctionality = Functionality.MicrosecondsPerDivision;
    }

    public CDelegateCommand UpdateValueFromKnob { get; private set; }

    private DateTime mPrevUpdateValueFromKnobDate;
    private double mMicrosecondsPerDivisionAccumulator;
    private void mUpdateValueFromKnob(object arg)
    {
      //Reset accumulator if enough time has passed since last delta, to guess the begin of a new interaction
      var now = DateTime.Now;
      if (now.Subtract(mPrevUpdateValueFromKnobDate).TotalMilliseconds > 1000)
        mMicrosecondsPerDivisionAccumulator = 0;
      mPrevUpdateValueFromKnobDate = now;

      var delta = (float)(double)arg;
      switch (CurrentFunctionality)
      {
        case Functionality.TriggerLevel:
          mBoard.TriggerPercent = mClamp(0, mBoard.TriggerPercent + delta / 4, 100);
          break;
        case Functionality.MicrosecondsPerDivision:
          mMicrosecondsPerDivisionAccumulator += delta;
          //mBoard.MicrosecondsPerDivision = (int)mClamp(10, mBoard.MicrosecondsPerDivision + delta, 500*1000);
          if (Math.Abs(mMicrosecondsPerDivisionAccumulator) > 30)
          {
            if (mMicrosecondsPerDivisionAccumulator > 0)
              mBoard.NextMicrosecondsPerDivision();
            else
              mBoard.PrevMicrosecondsPerDivision();
            mMicrosecondsPerDivisionAccumulator = 0;
          }
          break;
      }
    }

    private float mClamp(float min, float value, float max)
    {
      return value < min ? min : (value > max ? max : value);
    }
    #endregion
  }
}
