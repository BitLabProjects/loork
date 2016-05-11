using bitLab.ViewModel;
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
      SetTriggerLevelFunctionality = new CDelegateCommand(mSetTriggerLevelFunctionality);
      SetMicrosecondsPerDivisionFunctionality = new CDelegateCommand(mSetMicrosecondsPerDivisionFunctionality);
      UpdateValueFromKnob = new CDelegateCommand(mUpdateValueFromKnob);
    }

    #region Properties
    private Functionality mCurrentFunctionality;
    public Functionality CurrentFunctionality
    {
      get { return mCurrentFunctionality; }
      set { SetAndNotify(ref mCurrentFunctionality, value); }
    }
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
    private void mUpdateValueFromKnob(object arg)
    {
      var delta = (double)arg;
      switch (CurrentFunctionality)
      {
        case Functionality.TriggerLevel:
          var newTriggerPercent = mBoard.TriggerPercent + delta / 4;
          newTriggerPercent = newTriggerPercent > 100 ? 100 : newTriggerPercent;
          newTriggerPercent = newTriggerPercent < 0 ? 0 : newTriggerPercent;
          mBoard.TriggerPercent = newTriggerPercent;
          break;
      }
    }
    #endregion
  }
}
