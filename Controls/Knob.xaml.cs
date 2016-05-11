using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace loork_gui.Controls
{
  /// <summary>
  /// Interaction logic for Knob.xaml
  /// </summary>
  public partial class Knob : UserControl
  {
    public delegate void KnobRotatedEventHandler(object sender, KnobRotatedEventArgs e);
    public event KnobRotatedEventHandler KnobRotated;

    private double mAngle;
    private bool mCaptured;
    private double mMousePreviousAngle;

    public Knob()
    {
      InitializeComponent();
      mAngle = Math.PI / 2;
    }

    private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
    {
      var grid = sender as Grid;
      mCaptured = grid.CaptureMouse();
      mMousePreviousAngle = mCalcAngleFromMouse(e);
      e.Handled = true;
    }
    private void Grid_MouseMove(object sender, MouseEventArgs e)
    {
      if (mCaptured)
      {
        var mouseCurrentAngle = mCalcAngleFromMouse(e);
        //Calculate delta and take it to [-pi,+pi] range
        var angleDelta = mouseCurrentAngle - mMousePreviousAngle;
        if (angleDelta > Math.PI) angleDelta -= 2 * Math.PI;
        if (angleDelta < -Math.PI) angleDelta += 2 * Math.PI;
        mAngle += angleDelta;
        //Normalize angle between [0, 2*pi]
        if (mAngle > 2 * Math.PI) mAngle -= 2 * Math.PI; 
        if (mAngle < 0)           mAngle += 2 * Math.PI;

        
        UpdateIndicatorPosition();

        var deltaPercent = angleDelta / Math.PI * 100;
        if (KnobRotatedCommand != null)
          KnobRotatedCommand.Execute(deltaPercent);

        mMousePreviousAngle = mouseCurrentAngle;

        e.Handled = true;
      }
    }
    private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
    {
      if (mCaptured)
      {
        var grid = sender as Grid;
        grid.ReleaseMouseCapture();
        mCaptured = false;
        e.Handled = true;
      }
    }

    private double mCalcAngleFromMouse(MouseEventArgs e)
    {
      var mousePos = e.GetPosition(this);
      var mousePosFromCenter = mousePos - new Point(ActualWidth / 2, ActualHeight / 2);
      return Math.Atan2(-mousePosFromCenter.Y, mousePosFromCenter.X);
    }

    private void UpdateIndicatorPosition()
    {
      var radius = ActualWidth / 2 - (indicatorEllipse.ActualWidth * 1.4 / 2);
      var translateTransform = (TranslateTransform)(indicatorEllipse.RenderTransform);
      translateTransform.X = +Math.Cos(mAngle) * radius;
      translateTransform.Y = -Math.Sin(mAngle) * radius;
    }

    private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
      UpdateIndicatorPosition();
    }

    #region Dependency Properties
    public ICommand KnobRotatedCommand
    {
      get
      {
        return (ICommand)GetValue(KnobRotatedCommandProperty);
      }
      set
      {
        SetValue(KnobRotatedCommandProperty, value);
      }
    }

    public static DependencyProperty KnobRotatedCommandProperty { get; } =
      DependencyProperty.Register("KnobRotatedCommand",
                                  typeof(ICommand), typeof(Knob),
                                  new PropertyMetadata(null));
    #endregion
  }

  public class KnobRotatedEventArgs: EventArgs
  {
    public readonly double DeltaPercent;
    public KnobRotatedEventArgs(double deltaPercent)
    {
      this.DeltaPercent = deltaPercent;
    }
  }
}
