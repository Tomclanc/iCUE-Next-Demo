using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CueStudio {
 public class Profile {
  public string Name = "默认配置文件";
  public DeviceState Cooler = new DeviceState();
  public DeviceState Memory = new DeviceState();
  public string Mural = "彩虹";
  public string Color = "#B0E65A";
  public double Brightness = 80;
  public double Speed = 45;
  public string Cooling = "均衡";
  public string Screen = "CPU 温度";
  public string Light = "彩虹波浪";
  public double Fan = 65;
  public string Zones = "全部灯珠";
 }
 public class Preferences {
  public int Version = 1;
  public List<LocalSetting> LocalSettings = new List<LocalSetting>();
  public string Theme = "深色";
  public int Selected = 0;
  public List<Profile> Profiles = new List<Profile>();
  public List<string> Sensors = new List<string>();
 }
 public partial class MainWindow : Window {
  [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  void CaptionTheme() { try { var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;int value=dark?1:0;if(hwnd!=IntPtr.Zero)DwmSetWindowAttribute(hwnd,20,ref value,4); } catch {} ApplyMica(); }
  public Preferences Pref;
    public static string Store = SharedSettings();
  static string SharedSettings(){string dir=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"CueReplicaPreview",System.Security.Principal.WindowsIdentity.GetCurrent().User.Value);Directory.CreateDirectory(dir);string dest=System.IO.Path.Combine(dir,"settings.xml"),old=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CueReplicaPreview","settings.xml");if(!File.Exists(dest)&&File.Exists(old))File.Copy(old,dest);return dest;}
  string page = "主页", device = "H100i ELITE LCD", tab = "灯光效果";
  bool dark, paused, building;
  Grid shell, content;
  TextBlock status;
  DispatcherTimer timer;
  int tick;
  List<Action> live = new List<Action>();
  string bg, panel, card, fg, muted, line;
  const string Accent = "#D4EA63";
  static readonly string[] Murals = { "CORSAIR 黄", "CORSAIR ONE 蓝", "彩虹", "星际", "抽象", "流体" };
  static readonly string[] SensorNames = { "CPU 温度", "CPU 负载", "GPU 温度", "GPU 风扇", "冷却液温度", "水泵转速" };
  Profile P { get { return Pref.Profiles[Pref.Selected]; } }
  public MainWindow() {
   Title = "iCUE Next"; UseLayoutRounding=true; SnapsToDevicePixels=true; RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality); Width = 1390; Height = 840; MinWidth = 1000; MinHeight = 640; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.CanResize;
   System.Windows.Shell.WindowChrome.SetWindowChrome(this,new System.Windows.Shell.WindowChrome{CaptionHeight=40,ResizeBorderThickness=new Thickness(5),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(12),UseAeroCaptionButtons=false});
   PreviewKeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape){if(cardDragCancel!=null){cardDragCancel();e.Handled=true;return;}if(drawer){drawer=false;Build();}else CloseModal();e.Handled=true;}};
   WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13;
   Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("Styles.xaml"));
   using(var iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("icue-app.ico")){base.Icon=BitmapFrame.Create(iconStream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);}
   Pref = Read(Store); if(!File.Exists(Store)){Pref.Profiles[0].Cooler.Layers.Add(new LightLayer());Pref.Profiles[0].Memory.Layers.Add(new LightLayer());} Build();SourceInitialized+=delegate{InitializeWorkArea();CaptionTheme();ApplyMica();};
   timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
   timer.Tick += delegate { if (!paused) { tick++; foreach (var update in live.ToArray()) update(); } }; timer.Start();
   SizeChanged+=delegate{LayoutSurface();};StateChanged+=delegate{LayoutSurface();};SystemEvents.UserPreferenceChanged += SystemThemeChanged; Loaded+=delegate{InitializeDesktopFeatures();if(!Store.EndsWith("test-settings.xml"))Telemetry.Start();};
   Closed += delegate { timer.Stop(); SystemEvents.UserPreferenceChanged -= SystemThemeChanged; Save(); };
  }
  void SystemThemeChanged(object s, UserPreferenceChangedEventArgs e) { if (Pref.Theme == "跟随系统") Dispatcher.BeginInvoke(new Action(Build)); }
  public static Preferences Read(string path) {
   try { using (var f = File.OpenRead(path)) { var p = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(f); Validate(p); return p; } }
   catch { return new Preferences { Profiles=new List<Profile>{new Profile()}, Sensors=new List<string>(SensorNames) }; }
  }
  public static void Validate(Preferences p) {
   if(p == null || p.Version != 1 || p.Profiles == null || p.Profiles.Count == 0 || p.Profiles.Count > 50) throw new InvalidDataException("配置文件无效或版本不支持。");
   if(!new[]{"深色","亮色","跟随系统"}.Contains(p.Theme)) p.Theme="跟随系统";
   p.Selected = Math.Max(0,Math.Min(p.Selected,p.Profiles.Count-1));
   foreach(var v in p.Profiles) {
    if(v==null || String.IsNullOrWhiteSpace(v.Name) || v.Name.Length>40) throw new InvalidDataException("配置名称无效。");
    v.Brightness=Clamp(v.Brightness); v.Speed=Clamp(v.Speed); v.Fan=Clamp(v.Fan);
    try { ColorConverter.ConvertFromString(v.Color); } catch { v.Color="#B0E65A"; }
    if(String.IsNullOrWhiteSpace(v.Mural)) v.Mural="彩虹";
    if(!new[]{"静音","均衡","极限","自定义"}.Contains(v.Cooling)) v.Cooling="均衡";
    if(!new[]{"CPU 温度","冷却液温度","水泵转速","时钟"}.Contains(v.Screen)) v.Screen="CPU 温度";
    if(!new[]{"彩虹波浪","静态颜色","呼吸","色彩循环"}.Contains(v.Light)) v.Light="彩虹波浪";
   }
   if(p.Sensors==null) p.Sensors=new List<string>(); p.Sensors=p.Sensors.Where(x=>SensorNames.Contains(x)).Distinct().ToList();
  }
  static double Clamp(double n) { return Double.IsNaN(n)||Double.IsInfinity(n)?50:Math.Max(0,Math.Min(100,n)); }
  public void Save() {
   try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Store)); var tmp=Store+".tmp";
    using(var f=File.Create(tmp)) new XmlSerializer(typeof(Preferences)).Serialize(f,Pref);
    if(File.Exists(Store)) File.Replace(tmp,Store,null); else File.Move(tmp,Store);
   } catch(Exception e) { if(status!=null) status.Text="保存失败："+e.Message; }
  }
  static SolidColorBrush B(string c) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)); }
  TextBlock Text(string s, double size=13, string color=null, bool bold=false) { return new TextBlock { Text=s, FontSize=size, Foreground=B(color??fg), FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal, TextWrapping=TextWrapping.Wrap, VerticalAlignment=VerticalAlignment.Center }; }
  StackPanel Stack(bool horizontal=false) { return new StackPanel { Orientation=horizontal?Orientation.Horizontal:Orientation.Vertical }; }
  Border Box(UIElement child, string color=null, double padding=18, double radius=8) { return new Border { Background=B(color??card), BorderBrush=B(line), BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(radius), Padding=new Thickness(padding), Child=child }; }
  Button Btn(string text, Action action, bool primary=false) {
   var b=new Button { Content=text, Foreground=B(primary?"#202615":fg), Background=B(primary?Accent:card), BorderBrush=B(line), BorderThickness=new Thickness(1), Padding=new Thickness(14,9,14,9), Margin=new Thickness(0,0,8,0), Cursor=System.Windows.Input.Cursors.Hand, MinHeight=36 };
   b.Click+=delegate {action();}; return b;
  }
  void Note(string msg) { status.Text=msg; }
  void Go(string dest) { page=dest; Build(); }
  void Heading(StackPanel target,string title,string subtitle) {
   target.Children.Add(Text(title,26,null,true)); var sub=Text(subtitle,12,muted);sub.Margin=new Thickness(0,6,0,22);target.Children.Add(sub);
  }
  ComboBox Combo(IEnumerable<string> values,string selected,Action<string> action) {
   var cb=new ComboBox { MinWidth=160, MinHeight=36, Margin=new Thickness(0,5,0,8), Padding=new Thickness(8,5,8,5), ItemsSource=values.ToArray(), SelectedItem=selected, Foreground=B(fg), Background=B(panel), BorderBrush=B(line) };
   cb.SelectionChanged+=delegate {if(!building && cb.SelectedItem!=null) action(cb.SelectedItem.ToString());};return cb;
  }
  void LegacyBuild() {
   building=true; live.Clear();
   dark=Pref.Theme=="深色";
   if(Pref.Theme=="跟随系统") { try { dark=Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0; } catch {dark=true;} }
   bg=dark?"#1B1C20":"#F2F3F5";panel=dark?"#232429":"#FFFFFF";card=dark?"#141519":"#FFFFFF";fg=dark?"#F4F4F5":"#20232A";muted=dark?"#9699A3":"#6A6F7C";line=dark?"#303238":"#DFE2E7";
   Resources["PanelBrush"]=B(panel);Resources["TextBrush"]=B(fg);Resources["LineBrush"]=B(line);Resources["HoverBrush"]=B(dark?"#3D4147":"#E5E9ED");
   Background=B(bg); Foreground=B(fg);CaptionTheme();
   shell=new Grid { Background=B(bg) };shell.RowDefinitions.Add(new RowDefinition{Height=new GridLength(62)});shell.RowDefinitions.Add(new RowDefinition());shell.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});
   var top=new DockPanel{Background=B(card),LastChildFill=false};var brand=Text("◈  iCUE",23,null,true);brand.Margin=new Thickness(24,0,28,0);top.Children.Add(brand);
   foreach(var n in new[]{"主页","控制面板","Murals"}) { var key=n;var b=Btn(n,()=>Go(key));b.Background=Brushes.Transparent;b.BorderThickness=new Thickness(0,0,0,page==n?3:0);b.BorderBrush=B(Accent);b.Margin=new Thickness(0,10,10,0);top.Children.Add(b); }
   var tools=Stack(true); DockPanel.SetDock(tools,Dock.Right);tools.VerticalAlignment=VerticalAlignment.Center; tools.Children.Add(Btn(dark?"☀  亮色":"☾  深色",()=>{Pref.Theme=dark?"亮色":"深色";Save();Build();}));tools.Children.Add(Btn("⚙  设置",()=>Go("设置")));top.Children.Add(tools);shell.Children.Add(top);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(76)});body.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(body,1);shell.Children.Add(body);
   var rail=Stack();rail.Background=B(panel);rail.Children.Add(Rail("⌂","主页",()=>Go("主页")));rail.Children.Add(Rail("▤","配置文件",()=>Go("配置文件")));rail.Children.Add(Rail("◉","H100i ELITE LCD",()=>OpenDevice("H100i ELITE LCD")));rail.Children.Add(Rail("▥","DOMINATOR DDR5",()=>OpenDevice("DOMINATOR PLATINUM RGB DDR5")));body.Children.Add(rail);
   content=new Grid{Margin=new Thickness(26,24,26,20)};Grid.SetColumn(content,1);body.Children.Add(content);
   var foot=new DockPanel{Background=B(card),LastChildFill=true};var badge=Text("●  演示模式 · 未连接硬件",11,muted);badge.Margin=new Thickness(18,0,18,0);DockPanel.SetDock(badge,Dock.Right);foot.Children.Add(badge);status=Text("独立预览版  /  基于本机 iCUE 5.52.93 的界面参考",11,muted);status.Margin=new Thickness(24,0,0,0);foot.Children.Add(status);Grid.SetRow(foot,2);shell.Children.Add(foot);
   Content=shell;
   if(page=="主页") Home();else if(page=="控制面板") Dashboard();else if(page=="Murals") MuralPage();else if(page=="设置") Settings();else if(page=="配置文件") Profiles();else Device();
   building=false;
  }
  Button Rail(string glyph,string tooltip,Action a) {var b=Btn(glyph,a);b.ToolTip=tooltip;b.FontSize=24;b.Margin=new Thickness(10,14,10,0);b.Padding=new Thickness(0);b.Height=48;return b;}
  void Scroll(UIElement el) {content.Children.Add(new ScrollViewer{Content=el,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0)});}
  void OpenDevice(string name) { device=name;tab="灯光效果";Go("设备"); }
  UIElement ProfileBar() {
   var d=new DockPanel{Margin=new Thickness(0,0,0,20)};var r=Text("本地配置 · 自动保存",11,muted);DockPanel.SetDock(r,Dock.Right);d.Children.Add(r);
   var s=Stack(true);s.Children.Add(Text("当前配置  ",12,muted));s.Children.Add(Combo(Pref.Profiles.Select(p=>p.Name),P.Name,v=>{Pref.Selected=Pref.Profiles.FindIndex(p=>p.Name==v);Save();Build();}));var edit=Btn("管理配置",()=>Go("配置文件"));edit.Margin=new Thickness(12,4,0,8);s.Children.Add(edit);d.Children.Add(s);return d;
  }
  void Home() {
   var root=Stack();root.Children.Add(ProfileBar());var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(280)});grid.ColumnDefinitions.Add(new ColumnDefinition());root.Children.Add(grid);
   var left=Stack();left.Margin=new Thickness(0,0,22,0);grid.Children.Add(left);var mh=new DockPanel();var more=Btn("↗",()=>Go("Murals"));DockPanel.SetDock(more,Dock.Right);mh.Children.Add(more);mh.Children.Add(Text("Murals",18,null,true));left.Children.Add(mh);
   var tiles=new UniformGrid{Columns=2,Margin=new Thickness(0,14,0,18)};foreach(var m in Murals) tiles.Children.Add(MuralTile(m,64));left.Children.Add(tiles);
   var sh=new DockPanel();var sb=Btn("＋",()=>Go("控制面板"));DockPanel.SetDock(sb,Dock.Right);sh.Children.Add(sb);sh.Children.Add(Text("传感器",17,null,true));left.Children.Add(sh);left.Children.Add(Text("模拟数据 · 仅供界面预览",11,muted));
   var sensors=new UniformGrid{Columns=2,Margin=new Thickness(0,12,0,0)};foreach(var n in Pref.Sensors.Take(4)) sensors.Children.Add(Sensor(n,true));left.Children.Add(sensors);
   var right=Stack();Grid.SetColumn(right,1);grid.Children.Add(right);var title=new DockPanel();var count=Text("02  /  参考设备",11,muted);DockPanel.SetDock(count,Dock.Right);title.Children.Add(count);title.Children.Add(Text("设备",22,null,true));right.Children.Add(title);
   var cards=new UniformGrid{Columns=2,Margin=new Thickness(0,16,0,16)};cards.Children.Add(DeviceCard("H100i ELITE LCD","cooler.png","LCD 一体式水冷",true));cards.Children.Add(DeviceCard("DOMINATOR PLATINUM RGB DDR5","memory.png","RGB 高性能内存",false));right.Children.Add(cards);
   var summary=Stack();summary.Children.Add(Text("让整个桌面，保持同一种节奏。",19,null,true));summary.Children.Add(Text("当前 Murals  ·  "+P.Mural+"    /    亮度  "+P.Brightness.ToString("0")+"%",12,muted));var actions=Stack(true);actions.Margin=new Thickness(0,16,0,0);actions.Children.Add(Btn("编辑全局灯效  →",()=>Go("Murals"),true));actions.Children.Add(Btn("查看控制面板",()=>Go("控制面板")));summary.Children.Add(actions);right.Children.Add(Box(summary,panel,22));Scroll(root);
  }
  Image Asset(string name,double height) {var bm=new BitmapImage();bm.BeginInit();bm.CacheOption=BitmapCacheOption.OnLoad;bm.StreamSource=Assembly.GetExecutingAssembly().GetManifestResourceStream(name);bm.EndInit();var converted=new FormatConvertedBitmap(bm,PixelFormats.Bgra32,null,0);int w=bm.PixelWidth,h=bm.PixelHeight;byte[] pixels=new byte[w*h*4];converted.CopyPixels(pixels,w*4,0);int x0=w,y0=h,x1=0,y1=0;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[(y*w+x)*4+3]>20){x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);}BitmapSource source=x1>x0?new CroppedBitmap(bm,new Int32Rect(x0,y0,x1-x0+1,y1-y0+1)):(BitmapSource)bm;return new Image{Source=source,Height=height,Stretch=Stretch.Uniform,Margin=new Thickness(12)};}
  UIElement DeviceCard(string name,string asset,string desc,bool cooler) {
   var s=Stack();s.Children.Add(Text(name,12,null,true));s.Children.Add(Text(desc,11,muted));var visual=new Grid{Height=260};visual.Children.Add(Asset(asset,235));s.Children.Add(visual);
   var meta=Stack(true);meta.Children.Add(Text(cooler?"28.0":"42.0",25,null,true));meta.Children.Add(Text("  °C",12,muted));meta.Children.Add(Text("    演示",10,muted));s.Children.Add(meta);var b=Btn("设备设置  →",()=>OpenDevice(name));b.Margin=new Thickness(0,18,0,0);s.Children.Add(b);var box=Box(s,card,18);box.Margin=new Thickness(0,0,12,0);return box;
  }
  Brush MuralBrush(string m) {
   string[] colors=m=="CORSAIR 黄"?new[]{"#F5EA20","#E0DE24"}:m=="CORSAIR ONE 蓝"?new[]{"#3679FF","#3455E2"}:m=="彩虹"?new[]{"#FFD86B","#F77BC8","#8E78F2","#6DE4CF"}:m=="星际"?new[]{"#090F39","#713BFF","#FF633A","#19133B"}:m=="抽象"?new[]{"#4722B2","#D45DCF","#309CA8","#181D4B"}:new[]{"#142343","#A042E0","#F7604D","#F5B44E","#18256D"};
   var b=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1)};for(int i=0;i<colors.Length;i++)b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(colors[i]),(double)i/(colors.Length-1)));return b;
  }
  UIElement MuralTile(string m,double h) {
   var s=Stack();var swatch=new Border{Height=h,Background=MuralBrush(m),CornerRadius=new CornerRadius(5),BorderBrush=B(P.Mural==m?Accent:line),BorderThickness=new Thickness(P.Mural==m?3:1)};s.Children.Add(swatch);var t=Text(m+(P.Mural==m?"  ✓":""),11);t.Margin=new Thickness(0,7,0,0);s.Children.Add(t);
   var b=new Button{Content=s,Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,9,12),Padding=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand,ToolTip="应用 "+m+" 到预览"};b.Click+=delegate{P.Mural=m;Save();Build();Note("已将「"+m+"」应用到当前预览配置");};return b;
  }
  double Value(string n) {return Telemetry.Named(n).Value??Double.NaN;}
  UIElement Sensor(string name,bool small=false) {
   string color=name.Contains("转速")||name.Contains("风扇")?"#53ADE9":name.Contains("负载")?"#94CA55":"#EFA34D";
   var s=Stack();s.Children.Add(Text(name,small?10:12,muted));var v=Text("",small?17:30,color,true);v.Margin=new Thickness(0,5,0,2);s.Children.Add(v);
   var canvas=new Canvas{Height=small?30:70,ClipToBounds=true};s.Children.Add(canvas);
   Action update=()=>{v.Text=Value(name).ToString(name.Contains("转速")||name.Contains("风扇")?"0":"0.0")+(name.Contains("转速")||name.Contains("风扇")?" RPM":name.Contains("负载")?" %":" °C");canvas.Children.Clear();var poly=new Polyline{Stroke=B(color),StrokeThickness=1.5};double w=canvas.ActualWidth>0?canvas.ActualWidth:(small?90:220);for(int i=0;i<32;i++)poly.Points.Add(new Point(i*w/31,canvas.Height*(.55+.18*Math.Sin((i+tick)*.7)+.09*Math.Cos(i*2+tick))));canvas.Children.Add(poly);};update();live.Add(update);canvas.SizeChanged+=delegate{update();};var box=Box(s,card,small?10:20);box.Margin=new Thickness(0,0,10,10);return box;
  }
  void Dashboard() {
   var s=Stack();Heading(s,"控制面板","自定义你的监测视图。以下为模拟数据，不读取本机传感器。");var actions=Stack(true);actions.Children.Add(Btn(paused?"▶  继续演示":"Ⅱ  暂停演示",()=>{paused=!paused;Build();}));actions.Children.Add(Btn("选择传感器",SensorPicker));s.Children.Add(actions);var grid=new UniformGrid{Columns=3,Margin=new Thickness(0,22,0,0)};foreach(var n in Pref.Sensors)grid.Children.Add(Sensor(n));s.Children.Add(grid);if(Pref.Sensors.Count==0)s.Children.Add(Text("当前没有显示的传感器。点击「选择传感器」添加。",16,muted));Scroll(s);
  }
  void SensorPicker() {
   var s=Stack();s.Children.Add(Text("选择在主页和控制面板显示的传感器",15,null,true));foreach(var n in SensorNames) {var name=n;var c=new CheckBox{Content=name,IsChecked=Pref.Sensors.Contains(name),Foreground=B(fg),Margin=new Thickness(0,16,0,0)};c.Checked+=delegate{if(!Pref.Sensors.Contains(name))Pref.Sensors.Add(name);};c.Unchecked+=delegate{Pref.Sensors.Remove(name);};s.Children.Add(c);}var d=Dialog("传感器",s);d.Closed+=delegate{Save();Build();};d.ShowDialog();
  }
  void SliderRow(StackPanel s,string label,double val,Action<double> set) {
   var row=new DockPanel{Margin=new Thickness(0,20,0,8)};var num=Text(val.ToString("0")+" %",12);DockPanel.SetDock(num,Dock.Right);row.Children.Add(num);row.Children.Add(Text(label,12,muted));s.Children.Add(row);var sl=new Slider{Minimum=0,Maximum=100,Value=val,TickFrequency=1,IsSnapToTickEnabled=true,MinWidth=200};sl.ValueChanged+=delegate{if(building)return;num.Text=sl.Value.ToString("0")+" %";set(sl.Value);Save();};s.Children.Add(sl);
  }
  void MuralPage() {
   var s=Stack();Heading(s,"Murals","让色彩流动起来。选择场景，调整氛围，并保存到当前配置。");s.Children.Add(ProfileBar());var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
   var preview=Stack();var canvas=new Grid{Height=280};var back=new Border{Background=MuralBrush(P.Mural),CornerRadius=new CornerRadius(9),Opacity=P.Brightness/100};canvas.Children.Add(back);var devices=new UniformGrid{Columns=2};devices.Children.Add(Asset("cooler.png",210));devices.Children.Add(Asset("memory.png",210));canvas.Children.Add(devices);preview.Children.Add(canvas);var tiles=new UniformGrid{Columns=3,Margin=new Thickness(0,20,0,0)};foreach(var m in Murals)tiles.Children.Add(MuralTile(m,84));preview.Children.Add(tiles);preview.Margin=new Thickness(0,0,24,0);grid.Children.Add(preview);
   var options=Stack();options.Children.Add(Text("场景设置",19,null,true));options.Children.Add(Text("已选择  /  "+P.Mural,12,muted));SliderRow(options,"亮度",P.Brightness,v=>{P.Brightness=v;back.Opacity=v/100;});SliderRow(options,"流动速度",P.Speed,v=>P.Speed=v);
   live.Add(()=>{if(P.Mural!="CORSAIR 黄"&&P.Mural!="CORSAIR ONE 蓝") {var b=(LinearGradientBrush)MuralBrush(P.Mural);b.RelativeTransform=new RotateTransform(tick*(P.Speed/8),.5,.5);back.Background=b;}});
   var info=Text("设备范围\nH100i ELITE LCD\nDOMINATOR PLATINUM RGB DDR5\n\n灯效仅应用在此预览窗口内。",12,muted);info.Margin=new Thickness(0,30,0,0);info.LineHeight=24;options.Children.Add(info);var box=Box(options,panel);Grid.SetColumn(box,1);grid.Children.Add(box);s.Children.Add(grid);Scroll(s);
  }
  void Device() {
   var root=Stack();root.Children.Add(Btn("←  返回主页",()=>Go("主页")));var h=Text(device,25,null,true);h.Margin=new Thickness(0,20,0,6);root.Children.Add(h);root.Children.Add(Text("设备预览  /  "+P.Name+"  /  设置自动保存",12,muted));
   bool cooler=device.StartsWith("H100");var tabs=Stack(true);tabs.Margin=new Thickness(0,20,0,20);foreach(var t in (cooler?new[]{"灯光效果","屏幕设置","冷却","设备设置"}:new[]{"灯光效果","灯光设置","设备设置"})) {var local=t;tabs.Children.Add(Btn(t,()=>{tab=local;Build();},t==tab));}root.Children.Add(tabs);
   var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.25,GridUnitType.Star)});g.ColumnDefinitions.Add(new ColumnDefinition());var view=Stack();var preview=new Grid{Height=350};preview.Children.Add(new Border{Background=MuralBrush(P.Mural),Opacity=.09,CornerRadius=new CornerRadius(8)});preview.Children.Add(Asset(cooler?"cooler.png":"memory.png",280));view.Children.Add(preview);
   var label=Text("",21,null,true);label.HorizontalAlignment=HorizontalAlignment.Center;label.Margin=new Thickness(0,16,0,8);view.Children.Add(label);Action update=()=>{label.Text=cooler?(P.Screen=="时钟"?DateTime.Now.ToString("HH:mm:ss"):P.Screen+"  "+(P.Screen=="水泵转速"?Value("水泵转速").ToString("0")+" RPM":Value(P.Screen=="CPU 温度"?"CPU 温度":"冷却液温度").ToString("0.0")+" °C")):"12 区域 CAPELLIX  /  演示预览";};update();live.Add(update);
   var vbox=Box(view,card,18);vbox.Margin=new Thickness(0,0,24,0);g.Children.Add(vbox);var opts=Stack();opts.Children.Add(Text(tab,20,null,true));
   if(tab=="灯光效果") {
    opts.Children.Add(Text("效果类型",12,muted));opts.Children.Add(Combo(new[]{"彩虹波浪","静态颜色","呼吸","色彩循环"},P.Light,v=>{P.Light=v;Save();Build();}));
    var lightPreview=new Border{Height=56,Background=P.Light=="静态颜色"?B(P.Color):MuralBrush(P.Mural),CornerRadius=new CornerRadius(6),Margin=new Thickness(0,14,0,8),Opacity=P.Brightness/100};opts.Children.Add(lightPreview);
    opts.Children.Add(Text("颜色  /  HEX",12,muted));var color=new TextBox{Text=P.Color,Padding=new Thickness(10),Margin=new Thickness(0,7,0,8)};opts.Children.Add(color);opts.Children.Add(Btn("应用颜色",()=>{try{var c=(Color)ColorConverter.ConvertFromString(color.Text.Trim());P.Color=c.ToString();P.Light="静态颜色";Save();Build();Note("预览颜色已保存");}catch{Note("颜色格式无效，请输入例如 #A3E635");}}));
    SliderRow(opts,"亮度",P.Brightness,v=>{P.Brightness=v;lightPreview.Opacity=v/100;});SliderRow(opts,"速度",P.Speed,v=>P.Speed=v);
    live.Add(()=>{if(P.Light=="呼吸")lightPreview.Opacity=P.Brightness/100*(.3+.7*(Math.Sin(tick*P.Speed/100)+1)/2);else if(P.Light!="静态颜色"){var br=(LinearGradientBrush)MuralBrush(P.Mural);br.RelativeTransform=new RotateTransform(tick*P.Speed/5,.5,.5);lightPreview.Background=br;}});
   } else if(tab=="屏幕设置") {
    opts.Children.Add(Text("屏幕显示内容",12,muted));opts.Children.Add(Combo(new[]{"CPU 温度","冷却液温度","水泵转速","时钟"},P.Screen,v=>{P.Screen=v;Save();Build();}));opts.Children.Add(Text("左侧文字预览随选择更新；设备图片为原版静态素材。",12,muted));
   } else if(tab=="冷却") {
    opts.Children.Add(Text("散热预设",12,muted));opts.Children.Add(Combo(new[]{"静音","均衡","极限","自定义"},P.Cooling,v=>{P.Cooling=v;Save();Build();}));var c=new Canvas{Height=160,Background=B(bg),Margin=new Thickness(0,16,0,8)};opts.Children.Add(c);var pts=new Polyline{Stroke=B(Accent),StrokeThickness=3};double[] yy=P.Cooling=="静音"?new[]{135d,125,110,85,35}:P.Cooling=="极限"?new[]{100d,65,35,20,12}:new[]{130d,110,80,45,15};for(int i=0;i<5;i++)pts.Points.Add(new Point(20+i*65,yy[i]));c.Children.Add(pts);opts.Children.Add(Text("20°C           40°C           60°C           80°C",11,muted));
    if(P.Cooling=="自定义")SliderRow(opts,"固定风扇占空比",P.Fan,v=>{P.Fan=v;pts.Points.Clear();pts.Points.Add(new Point(20,150-v*1.3));pts.Points.Add(new Point(280,150-v*1.3));});opts.Children.Add(Text("曲线为预览示意，不会更改风扇或水泵的实际转速。",12,muted));
   } else if(tab=="灯光设置") {
    opts.Children.Add(Text("灯珠区域",12,muted));opts.Children.Add(Combo(new[]{"全部灯珠","左侧灯珠","右侧灯珠","交替灯珠"},P.Zones,v=>{P.Zones=v;Save();Build();}));var leds=new UniformGrid{Columns=6,Margin=new Thickness(0,25,0,20)};for(int i=0;i<12;i++){bool on=P.Zones=="全部灯珠"||P.Zones=="左侧灯珠"&&i<6||P.Zones=="右侧灯珠"&&i>=6||P.Zones=="交替灯珠"&&i%2==0;leds.Children.Add(new Border{Height=35,Margin=new Thickness(4),Background=B(on?P.Color:line),CornerRadius=new CornerRadius(4)});}opts.Children.Add(leds);opts.Children.Add(Text("区域选择已保存在当前配置。",12,muted));
   } else {
    opts.Children.Add(Text("参考型号",12,muted));opts.Children.Add(Text(device,15));var t=Text("连接状态    演示设备\n硬件通信    未启用\n配置存储    独立本地配置\n参考软件    iCUE 5.52.93",13,muted);t.LineHeight=30;t.Margin=new Thickness(0,20,0,20);opts.Children.Add(t);opts.Children.Add(Btn("重置当前配置的设备选项",()=>{P.Light="彩虹波浪";P.Screen="CPU 温度";P.Cooling="均衡";P.Fan=65;P.Zones="全部灯珠";Save();Build();Note("设备预览选项已重置");}));
   }
   var ob=Box(opts,panel,24);Grid.SetColumn(ob,1);g.Children.Add(ob);root.Children.Add(g);Scroll(root);
  }
  Window Dialog(string title,StackPanel s) {var d=new Window{Title=title,Owner=this,Width=450,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=B(panel),Foreground=B(fg),FontFamily=FontFamily};s.Margin=new Thickness(24);var close=Btn("完成",()=>d.Close(),true);close.Margin=new Thickness(0,24,0,0);s.Children.Add(close);d.Resources=Resources;d.Content=s;return d;}
  void AskName(string title,string value,Action<string> apply) {
   var s=Stack();s.Children.Add(Text("配置名称（最多 40 个字符）",14));var tb=new TextBox{Text=value,MaxLength=40,Padding=new Thickness(10),Margin=new Thickness(0,14,0,10)};s.Children.Add(tb);var error=Text("",12,"#E59155");s.Children.Add(error);var d=new Window{Title=title,Owner=this,Width=430,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B(panel),ResizeMode=ResizeMode.NoResize};var buttons=Stack(true);buttons.Children.Add(Btn("保存",()=>{var name=tb.Text.Trim();if(name.Length==0||Pref.Profiles.Any(p=>p.Name==name&&name!=value)){error.Text="请输入不重复的配置名称。";return;}apply(name);Save();d.Close();Build();},true));buttons.Children.Add(Btn("取消",()=>d.Close()));s.Children.Add(buttons);s.Margin=new Thickness(24);d.Resources=Resources;d.Content=s;d.Loaded+=delegate{tb.Focus();tb.SelectAll();};d.ShowDialog();
  }
  void Profiles() {
   var s=Stack();Heading(s,"配置文件","为不同场景保留灯效、屏幕和冷却选项。所有配置独立于原版 iCUE。");var a=Stack(true);a.Children.Add(Btn("＋  新建配置",()=>AskName("新建配置","",n=>{Pref.Profiles.Add(new Profile{Name=n});Pref.Selected=Pref.Profiles.Count-1;}),true));a.Children.Add(Btn("导入配置",Import));a.Children.Add(Btn("导出全部",Export));s.Children.Add(a);
   for(int i=0;i<Pref.Profiles.Count;i++){int idx=i;var p=Pref.Profiles[i];var row=new DockPanel{LastChildFill=true};var buttons=Stack(true);DockPanel.SetDock(buttons,Dock.Right);buttons.Children.Add(Btn(idx==Pref.Selected?"使用中":"启用",()=>{Pref.Selected=idx;Save();Build();},idx==Pref.Selected));buttons.Children.Add(Btn("重命名",()=>AskName("重命名",p.Name,n=>p.Name=n)));buttons.Children.Add(Btn("复制",()=>{var copy=new Profile{Name=UniqueName(p.Name+" 副本"),Mural=p.Mural,Color=p.Color,Brightness=p.Brightness,Speed=p.Speed,Cooling=p.Cooling,Screen=p.Screen,Light=p.Light,Fan=p.Fan,Zones=p.Zones};Pref.Profiles.Add(copy);Pref.Selected=Pref.Profiles.Count-1;Save();Build();}));var del=Btn("删除",()=>{if(MessageBox.Show(this,"删除配置「"+p.Name+"」？","删除预览配置",MessageBoxButton.YesNo)==MessageBoxResult.Yes){Pref.Profiles.RemoveAt(idx);if(Pref.Selected>idx)Pref.Selected--;else if(Pref.Selected>=Pref.Profiles.Count)Pref.Selected=Pref.Profiles.Count-1;Save();Build();}});del.IsEnabled=Pref.Profiles.Count>1;buttons.Children.Add(del);row.Children.Add(buttons);var desc=Stack();desc.Children.Add(Text(p.Name,16,null,true));desc.Children.Add(Text(p.Mural+" · "+p.Brightness.ToString("0")+"% 亮度 · "+p.Cooling,11,muted));row.Children.Add(desc);var box=Box(row);box.Margin=new Thickness(0,14,0,0);s.Children.Add(box);}Scroll(s);
  }
  string UniqueName(string name){string result=name;int n=2;while(Pref.Profiles.Any(p=>p.Name==result))result=name+" "+n++;return result;}
  void Export() {var d=new SaveFileDialog{Filter="iCUE Studio 配置 (*.xml)|*.xml",FileName="iCUE-Studio-Profiles.xml"};if(d.ShowDialog(this)==true){try{using(var f=File.Create(d.FileName))new XmlSerializer(typeof(Preferences)).Serialize(f,Pref);Note("配置已导出");}catch(Exception e){Note("导出失败："+e.Message);}}}
  void Import() {var d=new OpenFileDialog{Filter="iCUE Studio 配置 (*.xml)|*.xml"};if(d.ShowDialog(this)==true){try{if(new FileInfo(d.FileName).Length>1024*1024)throw new InvalidDataException("文件过大");Preferences imported;using(var f=File.OpenRead(d.FileName))imported=(Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(f);Validate(imported);if(Pref.Profiles.Count+imported.Profiles.Count>50)throw new InvalidDataException("最多支持 50 个配置");foreach(var p in imported.Profiles){p.Name=UniqueName(p.Name);Pref.Profiles.Add(p);}Save();Build();Note("已导入 "+imported.Profiles.Count+" 个配置");}catch(Exception e){Note("无法导入："+e.Message);}}}
  void Settings() {
   var s=Stack();Heading(s,"设置","把工作台调整为适合你的样子。");var appearance=Stack();appearance.Children.Add(Text("外观",19,null,true));appearance.Children.Add(Text("主题模式",12,muted));appearance.Children.Add(Combo(new[]{"深色","亮色","跟随系统"},Pref.Theme,v=>{Pref.Theme=v;Save();Build();}));appearance.Children.Add(Text("跟随系统会响应 Windows 应用颜色模式的变化。",12,muted));s.Children.Add(Box(appearance));
   var storage=Stack();storage.Children.Add(Text("配置与数据",19,null,true));storage.Children.Add(Text("更改即时保存在此副本专用目录，可导出并迁移到另一台电脑。",12,muted));var actions=Stack(true);actions.Margin=new Thickness(0,16,0,0);actions.Children.Add(Btn("管理配置",()=>Go("配置文件")));actions.Children.Add(Btn("导出备份",Export));storage.Children.Add(actions);var path=Text(Store,11,muted);path.Margin=new Thickness(0,16,0,0);storage.Children.Add(path);var st=Box(storage);st.Margin=new Thickness(0,16,0,16);s.Children.Add(st);
   var about=Stack();about.Children.Add(Text("iCUE Studio  /  0.1.0",19,null,true));var text=Text("独立交互预览 · 参考本机 iCUE 5.52.93\n主页、控制面板、Murals、设备选项、配置管理与深浅色主题已实现。\n此版本不控制硬件；设备数值为模拟数据。原版安装文件与配置未被修改。\nCORSAIR / iCUE 名称和设备素材归原权利人所有。本地预览构建并非官方软件。",12,muted);text.LineHeight=26;about.Children.Add(text);s.Children.Add(Box(about));Scroll(s);
  }
  public void LegacyVerify(string dir) {
   Pref=new Preferences {Profiles=new List<Profile>{new Profile()},Sensors=new List<string>(SensorNames)};
   Directory.CreateDirectory(dir);var messages=new List<string>();
   foreach(var theme in new[]{"深色","亮色"})foreach(var pg in new[]{"主页","控制面板","Murals","配置文件","设置","设备"}){Pref.Theme=theme;page=pg;Build();UpdateLayout();var image=new RenderTargetBitmap((int)shell.ActualWidth,(int)shell.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(shell);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(System.IO.Path.Combine(dir,theme+"-"+pg+".png")))png.Save(f);messages.Add(theme+" / "+pg+": render OK");}
   foreach(var t in new[]{"灯光效果","屏幕设置","冷却","设备设置"}){tab=t;Build();UpdateLayout();messages.Add("H100i / "+t+": OK");}device="DOMINATOR PLATINUM RGB DDR5";foreach(var t in new[]{"灯光效果","灯光设置","设备设置"}){tab=t;Build();UpdateLayout();messages.Add("DDR5 / "+t+": OK");}
   var original=P.Name;P.Name="验证配置";P.Brightness=37;Save();var restored=Read(Store);if(restored.Profiles[restored.Selected].Name!="验证配置"||restored.Profiles[restored.Selected].Brightness!=37)throw new Exception("配置持久化验证失败");messages.Add("Configuration round trip: OK");P.Name=original;
   File.WriteAllLines(System.IO.Path.Combine(dir,"verification.txt"),messages);Close();
  }
 }
 public static class Program {
  [STAThread] public static void Main(string[] args) {
   bool verify=args.Length==2&&args[0]=="--verify";if(verify)MainWindow.Store=System.IO.Path.Combine(System.IO.Path.GetFullPath(args[1]),"test-settings.xml");
   var app=new Application();app.DispatcherUnhandledException+=delegate(object s,DispatcherUnhandledExceptionEventArgs e){MessageBox.Show("运行失败："+e.Exception.Message,"iCUE Studio");};var w=new MainWindow();if(verify)w.Loaded+=delegate{w.Dispatcher.BeginInvoke(new Action(()=>w.Verify(args[1])),DispatcherPriority.ApplicationIdle);};app.Run(w);
  }
 }
}


