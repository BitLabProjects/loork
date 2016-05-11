using loork_gui.Oscilloscope;
using loork_gui.View;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace loork_gui
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    private void Application_Startup(object sender, StartupEventArgs e)
    {
      var board = new LoorkBoard(Dispatcher);
      var userInterfaceVM = new UserInterfaceVM(board);

      var window = new MainWindow();
      window.DataContext = userInterfaceVM;
      window.Show();
    }
  }
}
