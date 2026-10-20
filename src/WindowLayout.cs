using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace CueStudio {public partial class MainWindow {
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetSystemMetrics(int index);

 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct MonitorData {public int Size;public NativeRect Monitor,Work;public uint Flags;}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct MinMaxData {public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorData data);
 void InitializeWorkArea(){var source=System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);if(source!=null)source.AddHook(WorkAreaHook);}
 IntPtr WorkAreaHook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){
  if(msg==0x0024){var monitor=new MonitorData();monitor.Size=System.Runtime.InteropServices.Marshal.SizeOf(typeof(MonitorData));if(GetMonitorInfo(MonitorFromWindow(hwnd,2),ref monitor)){
   var data=(MinMaxData)System.Runtime.InteropServices.Marshal.PtrToStructure(lp,typeof(MinMaxData));
   data.MaxPosition.X=monitor.Work.Left-monitor.Monitor.Left;data.MaxPosition.Y=monitor.Work.Top-monitor.Monitor.Top;
   data.MaxSize.X=monitor.Work.Right-monitor.Work.Left;data.MaxSize.Y=monitor.Work.Bottom-monitor.Work.Top;
   System.Runtime.InteropServices.Marshal.StructureToPtr(data,lp,false);handled=true;
  }}return IntPtr.Zero;
 }
 Canvas captionCanvas;Button maximizeButton;
 Dictionary<FrameworkElement,Point> baseLayout=new Dictionary<FrameworkElement,Point>();
 void CaptionButton(Canvas c,string kind,double x,Action action){var b=Click(c,"",x,0,24,40,action);b.ToolTip=kind=="min"?"最小化":kind=="close"?"关闭":"最大化";b.Content=CaptionGlyph(kind);if(kind=="max")maximizeButton=b;}
 FrameworkElement CaptionGlyph(string kind){string path=kind=="min"?"M1,6 L13,6":kind=="close"?"M2,2 L12,12 M12,2 L2,12":WindowState==WindowState.Maximized?"M4,1 L12,1 12,9 M2,4 L9,4 9,12 2,12 Z":"M2,2 L12,2 12,12 2,12 Z";return new System.Windows.Shapes.Path{Data=Geometry.Parse(path),Stroke=B(muted),StrokeThickness=kind=="close"?1.5:1,Width=14,Height=14,Stretch=Stretch.None,SnapsToDevicePixels=true};}
 void CaptureLayout(Canvas title){foreach(UIElement child in title.Children)System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(child,true);captionCanvas=title;baseLayout.Clear();foreach(FrameworkElement e in surface.Children)baseLayout[e]=new Point(Canvas.GetLeft(e),Canvas.GetTop(e));foreach(FrameworkElement e in title.Children)baseLayout[e]=new Point(Canvas.GetLeft(e),Canvas.GetTop(e));}
 void ResizePane(FrameworkElement element,double width,double height){element.Width=width;element.Height=height;var border=element as Border;if(border!=null){var child=border.Child as FrameworkElement;if(child!=null){child.Width=width;child.Height=height;if(border.CornerRadius.TopLeft>0)child.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,width-2),Math.Max(0,height-2)),border.CornerRadius.TopLeft-1,border.CornerRadius.TopLeft-1);}}}
 void LayoutSurface(){if(surface==null||building||captionCanvas==null)return;shell.Margin=new Thickness(0);double w=ActualWidth>0?ActualWidth:1390,h=ActualHeight>0?ActualHeight:840,dx=w-1390,dy=h-840;surface.Width=w;surface.Height=h;overlay.Width=w;overlay.Height=h;
  foreach(var pair in baseLayout){var e=pair.Key;var pt=pair.Value;if(Double.IsNaN(pt.X)||Double.IsNaN(pt.Y))continue;double x=pt.X,y=pt.Y;if(e.Parent==captionCanvas){if(x>=1125)x+=dx;}else if(e.Parent==surface){if(pt.X==0&&pt.Y==0)ResizePane(e,w,40);if(pt.X==0&&pt.Y==40)ResizePane(e,80,Math.Max(0,h-40));if(page=="主页"&&pt.X>=100&&pt.X<=410&&pt.Y>=431)y+=Math.Max(0,dy/2);if(page=="控制面板"&&e is ScrollViewer){e.Width=Math.Max(0,w-110);e.Height=Math.Max(0,h-50);}if(page=="设备"){if(pt.Y==40&&pt.X==96)ResizePane(e,Math.Max(0,w-96),72);else if(pt.Y>=512)y+=dy;else if(pt.X>=352&&pt.Y>=128){x+=dx/2;y+=dy/2;}}if(page=="Murals"&&pt.Y==40){if(pt.X==80)ResizePane(e,360,Math.Max(0,h-40));if(pt.X==440)ResizePane(e,Math.Max(0,w-440),Math.Max(0,h-40));}}Canvas.SetLeft(e,x);Canvas.SetTop(e,y);}
  if(maximizeButton!=null){maximizeButton.Content=CaptionGlyph("max");maximizeButton.ToolTip=WindowState==WindowState.Maximized?"还原":"最大化";}
  foreach(FrameworkElement e in overlay.Children){var b=e as Border;if(b==null)continue;if(b.Child==null){Canvas.SetTop(e,40);if(drawer){Canvas.SetLeft(e,320);e.Width=Math.Max(0,w-320);}else{Canvas.SetLeft(e,0);e.Width=w;}e.Height=Math.Max(0,h-40);}else if(drawer)ResizePane(e,320,Math.Max(0,h-40));else{Canvas.SetLeft(e,Math.Max(0,(w-e.Width)/2));Canvas.SetTop(e,Math.Max(40,(h-e.Height)/2));}}
 }
}}

