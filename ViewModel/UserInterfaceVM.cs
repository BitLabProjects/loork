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
    public UserInterfaceVM()
    {
      SetTriggerLevelFunctionality = new CDelegateCommand(mSetTriggerLevelFunctionality);
      SetMicrosecondsPerDivisionFunctionality = new CDelegateCommand(mSetMicrosecondsPerDivisionFunctionality);
    }

    private Functionality mCurrentFunctionality;
    public Functionality CurrentFunctionality
    {
      get { return mCurrentFunctionality; }
      set { SetAndNotify(ref mCurrentFunctionality, value); }
    }

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
  }
}
