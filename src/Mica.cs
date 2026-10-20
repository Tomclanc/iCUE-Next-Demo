using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace CueStudio {
 public partial class MainWindow {
  [StructLayout(LayoutKind.Sequential)] struct GlassMargins {public int Left,Right,Top,Bottom;}
  [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref GlassMargins margins);
  bool micaActive;
  System.Windows.Controls.Button ProfileTool(System.Windows.Controls.Canvas canvas,string kind,double x,double y,Action action,string hint){
   var button=Click(canvas,"",x,y,kind=="back"||kind=="forward"?32:24,32,action,kind=="back"||kind=="forward"?selectedBg:null);button.ToolTip=hint;
   var icon=new System.Windows.Shapes.Path{Stroke=B(muted),StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Stretch=Stretch.Uniform,Width=kind=="back"||kind=="forward"?6:16,Height=kind=="back"||kind=="forward"?10:16};
   string path=kind=="back"?"M9,3 L4,8 9,13":kind=="forward"?"M4,3 L9,8 4,13":kind=="folder"?"M2,5 L7,5 9,7 20,7 20,19 2,19 Z M11,10 L11,16 M8,13 L14,13":"M3,7 L16,7 M12,3 L16,7 12,11 M19,16 L6,16 M10,12 L6,16 10,20";
   icon.Data=Geometry.Parse(path);button.Content=kind=="forward"?(FrameworkElement)icon:OriginalGlyph(kind,muted,16);return button;
  }
  void ApplyMica(){try{var hwnd=new WindowInteropHelper(this).Handle;if(hwnd==IntPtr.Zero)return;int corners=2;DwmSetWindowAttribute(hwnd,33,ref corners,4);int backdrop=4;micaActive=DwmSetWindowAttribute(hwnd,38,ref backdrop,4)==0;if(micaActive){var source=HwndSource.FromHwnd(hwnd);if(source!=null)source.CompositionTarget.BackgroundColor=Colors.Transparent;var margins=new GlassMargins{Left=-1,Right=-1,Top=-1,Bottom=-1};DwmExtendFrameIntoClientArea(hwnd,ref margins);Background=Brushes.Transparent;if(shell!=null)shell.Background=Brushes.Transparent;if(surface!=null)surface.Background=new SolidColorBrush(dark?Color.FromArgb(48,24,24,28):Color.FromArgb(100,248,249,252));}}catch{micaActive=false;}}
 }
}
