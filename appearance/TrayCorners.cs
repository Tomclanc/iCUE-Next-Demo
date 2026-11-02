using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Applies a real native window region only to explicitly marked iCUE tray popups.
// Does not show, hide, activate, or change the main iCUE window.
static class TrayCorners {
 delegate void WinEventProc(IntPtr hook,uint e,IntPtr h,int obj,int child,uint thread,uint time);
 [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc callback,uint pid,uint tid,uint flags);
 [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
 [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr h,IntPtr region);
 static WinEventProc onWindowEvent=delegate(IntPtr hook,uint e,IntPtr h,int obj,int child,uint thread,uint time) {
  if(obj!=0||child!=0||h==IntPtr.Zero)return;
  var cls=new StringBuilder(128);GetClassName(h,cls,128);
  var title=new StringBuilder(128);GetWindowText(h,title,128);
  if(cls.ToString()=="CUETrayMenu"||title.ToString().StartsWith("iCUE Tray Theme"))Tick();
 };
 delegate bool EnumProc(IntPtr h,IntPtr p);
 [StructLayout(LayoutKind.Sequential)] struct Rect { public int L,T,R,B; }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f,IntPtr p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h,IntPtr r,bool redraw);
 [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l,int t,int r,int b,int w,int h);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
 [DllImport("gdi32.dll")] static extern bool PtInRegion(IntPtr r,int x,int y);
 [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 static Dictionary<IntPtr,string> applied=new Dictionary<IntPtr,string>();
 static IntPtr Region(int w,int h,int dpi) { int diameter=(int)Math.Round(24.0*dpi/96); return CreateRoundRectRgn(0,0,w,h,diameter,diameter); }
 static void Tick() {
  var current=new HashSet<IntPtr>();
  EnumWindows(delegate(IntPtr h,IntPtr unused) {
   if(!IsWindowVisible(h))return true;
   var title=new StringBuilder(128);GetWindowText(h,title,title.Capacity);
   var windowClass=new StringBuilder(128);GetClassName(h,windowClass,128);
   if(windowClass.ToString()!="CUETrayMenu" && !(windowClass.ToString().StartsWith("Qt") && title.ToString().StartsWith("iCUE Tray Theme")))return true;
   uint pid;GetWindowThreadProcessId(h,out pid);
   try { using(var p=Process.GetProcessById((int)pid)) {
    if(!String.Equals(p.MainModule.FileName,@"C:\Program Files\Corsair\Corsair iCUE5 Software\iCUE.exe",StringComparison.OrdinalIgnoreCase))return true;
   }}catch{return true;}
   Rect r;if(!GetWindowRect(h,out r))return true;
   int w=r.R-r.L,ht=r.B-r.T,dpi=(int)GetDpiForWindow(h);if(w<=0||ht<=0)return true;
   string key=pid+":"+w+":"+ht+":"+dpi;current.Add(h);
   string old;if(applied.TryGetValue(h,out old)&&old==key)return true;
   var region=Region(w,ht,dpi);
   if(SetWindowRgn(h,region,true)!=0)applied[h]=key;else DeleteObject(region);
   return true;
  },IntPtr.Zero);
  foreach(var h in new List<IntPtr>(applied.Keys))if(!current.Contains(h))applied.Remove(h);
 }
 [STAThread] static int Main(string[] args) {
  SetProcessDpiAwarenessContext(new IntPtr(-4));
  if(args.Length>0&&args[0]=="--self-test") {
   foreach(int dpi in new[]{96,144,192}) {var r=Region(320,266,dpi);bool ok=!PtInRegion(r,0,0)&&PtInRegion(r,160,133);DeleteObject(r);if(!ok)return 1;}return 0;
  }
  bool created;using(var mutex=new Mutex(true,"Local\\iCUETrayCorners",out created)) {
   if(!created)return 0;
   SetProcessDpiAwarenessContext(new IntPtr(-4));
   var hook=SetWinEventHook(0x8002,0x800B,IntPtr.Zero,onWindowEvent,0,0,2);
   try {using(var timer=new System.Windows.Forms.Timer()) {timer.Interval=100;timer.Tick+=delegate{Tick();};timer.Start();Application.Run();}}finally{if(hook!=IntPtr.Zero)UnhookWinEvent(hook);}
  }return 0;
 }
}
