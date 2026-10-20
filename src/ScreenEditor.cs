using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Automation;
using System.Xml.Linq;
namespace CueStudio {
 static partial class OriginalCoolingUi {
  public static XElement ScreenData(){
   var dir=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Corsair","CUE5","profiles");
   var files=Directory.GetFiles(dir,"*.cueprofiledata");if(files.Length!=1)throw new Exception("无法确定当前原版配置");
   var n=XElement.Load(files[0]).Descendants().First(e=>(string)e.Element("key")=="LightingNode");
   return new XElement(n.Descendants("effect").First(e=>((string)e.Element("polymorphic_name")??"").EndsWith("ScreenEffectSettings")).Element("ptr_wrapper").Element("data"));
  }
  static AutomationElement EditorRoot(out Process p,string expected){var r=ScreenRoot(out p);if(SavedScreenClass()!=expected)throw new Exception("原版屏幕类型已改变，请重新选择屏幕类型");return r;}
  static AutomationElement[] EditorControls(AutomationElement root,ControlType type,string id=""){
   return root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,type)).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen&&e.Current.AutomationId.Contains(id)).OrderBy(e=>e.Current.BoundingRectangle.Top).ThenBy(e=>e.Current.BoundingRectangle.Left).ToArray();
  }
  public static void EditorCombo(int index,string value){
   Process p;var r=EditorRoot(out p,"ConstructorScreenEffectSettings");var cs=EditorControls(r,ControlType.ComboBox);if(index>=cs.Length)throw new Exception("未找到原版选项");
   var c=cs[index];if(c.Current.Name==value)return;ClickControl(c,p.MainWindowHandle);Thread.Sleep(150);
   var search=EditorControls(r,ControlType.Edit,".ComboBoxPopup.").FirstOrDefault();if(search!=null){((ValuePattern)search.GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);Thread.Sleep(150);}
   var items=EditorControls(r,ControlType.ListItem,".ComboBoxPopup.").Where(e=>e.Current.Name==value).ToArray();if(items.Length!=1)throw new Exception("原版未提供该选项："+value);ClickControl(items[0],p.MainWindowHandle);
   Thread.Sleep(500);if(c.Current.Name!=value)throw new Exception("原版未确认选项");
  }
  public static void EditorText(int index,string value){
   Process p;var r=EditorRoot(out p,"ConstructorScreenEffectSettings");var fields=EditorControls(r,ControlType.Edit,"TextFieldWithMenu");if(index>=fields.Length)throw new Exception("未找到原版文本框");var field=fields[index];((ValuePattern)field.GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);ClickControl(field,p.MainWindowHandle);PostMessage(p.MainWindowHandle,0x100,new IntPtr(13),IntPtr.Zero);PostMessage(p.MainWindowHandle,0x101,new IntPtr(13),IntPtr.Zero);Thread.Sleep(250);
   var entry=ScreenData().Element("sensorsData").Elements().ElementAt(index);if((string)entry.Element("name")!=value)throw new Exception("原版尚未保存文本");
  }
  public static void EditorOrientation(int index,int value){
   Process p;var r=EditorRoot(out p,"ConstructorScreenEffectSettings");var fields=EditorControls(r,ControlType.Edit,"TextFieldWithMenu");if(index>=fields.Length)throw new Exception("未找到原版文本设置");var box=fields[index].Current.BoundingRectangle;
   var choices=EditorControls(r,ControlType.CheckBox).Where(e=>e.Current.BoundingRectangle.Top>=box.Bottom&&e.Current.BoundingRectangle.Top<box.Bottom+45&&e.Current.BoundingRectangle.Left>=box.Left-5&&e.Current.BoundingRectangle.Left<box.Right).OrderBy(e=>e.Current.BoundingRectangle.Left).ToArray();if(choices.Length!=2)throw new Exception("原版文字位置布局不匹配");ClickControl(choices[value],p.MainWindowHandle);Thread.Sleep(250);
  }
  public static void OpenMedia(string expected){Process p;var r=EditorRoot(out p,expected);var add=EditorControls(r,ControlType.Button,"LoaderWithProxyFocus").Where(e=>e.Current.AutomationId.EndsWith(".QQuickButton")).Single();ClickControl(add,p.MainWindowHandle);}
  public static void EditorMedia(string expected,string action,double value){
   Process p;var r=EditorRoot(out p,expected);var slider=EditorControls(r,ControlType.Slider).Single();var rect=slider.Current.BoundingRectangle;
   if(action=="scale"){var pat=(RangeValuePattern)slider.GetCurrentPattern(RangeValuePattern.Pattern);double target=Math.Max(pat.Current.Minimum,Math.Min(pat.Current.Maximum,value));pat.SetValue(target);slider.SetFocus();int first=target>=pat.Current.Maximum?37:39;int second=first==37?39:37;foreach(int key in new[]{first,second}){PostMessage(p.MainWindowHandle,0x100,new IntPtr(key),IntPtr.Zero);PostMessage(p.MainWindowHandle,0x101,new IntPtr(key),IntPtr.Zero);}}
   else if(action=="blur"){var check=EditorControls(r,ControlType.CheckBox,"StyledSwitch").Where(e=>Math.Abs(e.Current.BoundingRectangle.Top-rect.Top)<25).Single();ClickControl(check,p.MainWindowHandle);}
   else if(action=="asset") {var assets=EditorControls(r,ControlType.Button,"AssetIcon");if(value<0||value>=assets.Length)throw new Exception("原版背景素材列表不匹配");ClickControl(assets[(int)value],p.MainWindowHandle);}
   else if(action=="rotate") {var buttons=EditorControls(r,ControlType.Button).Where(e=>e.Current.BoundingRectangle.Left>rect.Right&&Math.Abs(e.Current.BoundingRectangle.Top-rect.Top)<25).OrderBy(e=>e.Current.BoundingRectangle.Left).ToArray();if(buttons.Length!=3)throw new Exception("原版旋转按钮布局不匹配");ClickControl(buttons[value<0?1:2],p.MainWindowHandle);}
   Thread.Sleep(500);
   if(action=="scale"){var data=ScreenData();var image=expected=="ConstructorScreenEffectSettings"?data.Element("base"):data;double actual=double.Parse((string)image.Element("scaling"),System.Globalization.CultureInfo.InvariantCulture);if(Math.Abs(actual-value/100)>0.015)throw new Exception("原版尚未保存缩放，请重试");}
  }
 }
 public partial class MainWindow {
  static string SX(XElement x,string name,string fallback=""){return x==null?fallback:(string)x.Element(name)??fallback;}
  static double SN(XElement x,string name,double fallback=0){double n;return double.TryParse(SX(x,name),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out n)?n:fallback;}
  static string SC(XElement x,string name){var v=SX(x,name,"rgb(1 1 1)");try{if(!v.StartsWith("rgb("))return v;var a=v.Substring(4,v.Length-5).Split(' ').Select(t=>double.Parse(t,System.Globalization.CultureInfo.InvariantCulture)).ToArray();return string.Format("#{0:X2}{1:X2}{2:X2}",(int)Math.Round(a[0]*255),(int)Math.Round(a[1]*255),(int)Math.Round(a[2]*255));}catch{return "#FFFFFF";}}
  async void OpenOriginalMediaDialog(string expected){if(screenApplying)return;screenApplying=true;try{await System.Threading.Tasks.Task.Run(()=>OriginalCoolingUi.OpenMedia(expected));}catch(Exception ex){Info("打开图像",ex.Message);}finally{screenApplying=false;}}
  void ConstructorEditor(){
   bool constructor=DS.ScreenType=="构造程序";string expected=constructor?"ConstructorScreenEffectSettings":"ImageScreenEffectSettings";XElement d;
   try{d=OriginalCoolingUi.ScreenData();}catch(Exception ex){var error=Pane(surface,360,512,1014,312,tileBg);Label(error,ex.Message,16,40,960,50);return;}
   var settings=Pane(surface,360,512,384,312,tileBg);Label(settings,"屏幕设置",16,8,340,28,15);
   if(constructor){
    var types=new[]{"1个传感器","2个传感器","3个传感器","时间"};int kind=(int)SN(d,"dataType");Label(settings,"数据类型",16,44,100,24,12);Select(settings,types,types[Math.Max(0,Math.Min(3,kind))],136,44,232,v=>ApplyScreenChange(()=>OriginalCoolingUi.EditorCombo(0,v),()=>{}));
    var entries=d.Element("sensorsData");if(entries!=null){int row=0;foreach(var e in entries.Elements().Take(3)){int index=row;double y=80+row*72;string text=SX(e,"name");string sid=SX(e,"sensorId");string sensorName=sid.Contains("TemperaturePackage")?"AMD Ryzen 9 9900X 包":sid.Contains("430a201134895aaab9f09f061091005f")?"H100i ELITE LCD 冷却液温度":"原版传感器";
     if(kind!=3){Label(settings,"传感器"+(row+1),16,y,80,24,12);ColorButton(settings,SC(e,"valueColor"),100,y,v=>ApplyScreenChange(()=>OriginalCoolingUi.SetScreenColor(index*2,v,expected),()=>{}));Select(settings,new[]{"H100i ELITE LCD 冷却液温度","AMD Ryzen 9 9900X 包","NVIDIA GeForce RTX 3090 温度 #1"},sensorName,136,y,232,v=>ApplyScreenChange(()=>OriginalCoolingUi.EditorCombo(index+1,v),()=>{}));}
     else Label(settings,"时间文字",16,y,100,24,12);
     ColorButton(settings,SC(e,"nameColor"),100,y+28,v=>ApplyScreenChange(()=>OriginalCoolingUi.SetScreenColor(kind==3?0:index*2+1,v,expected),()=>{}));
     var input=new TextBox{Text=text,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(5,0,5,0)};Put(settings,input,136,y+28,232,24);input.LostFocus+=delegate{if(input.Text!=text)ApplyScreenChange(()=>OriginalCoolingUi.EditorText(index,input.Text),()=>{});};
     row++;
    }
    if(row==1){var entry=entries.Elements().First();int pos=(int)SN(entry,"orientation");Click(settings,"顶部",136,218,112,24,()=>ApplyScreenChange(()=>OriginalCoolingUi.EditorOrientation(0,0),()=>{}),pos==0?selectedBg:railBg);Click(settings,"底部",252,218,116,24,()=>ApplyScreenChange(()=>OriginalCoolingUi.EditorOrientation(0,1),()=>{}),pos==1?selectedBg:railBg);}
   }}else Label(settings,"选择背景素材，调整缩放、旋转和模糊。",16,60,350,70,13,muted);
   var media=Pane(surface,752,512,622,312,tileBg);Label(media,"背景设置",16,8,180,28,15);Label(media,"编辑媒体",250,8,300,28,15);
   Label(media,"本地图像",16,44,145,24,12);Click(media,"＋",182,42,30,28,()=>OpenOriginalMediaDialog(expected),railBg);var imageData=constructor?d.Element("base"):d;var source=SX(imageData,"backgroundSource");string root=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ScreenRenderer","screen-runtime","resources","cue-assets","4.0","screen-effect-images");string[] assets={"black_image.png","logo_01.png","logo_02.png","logo_03.png","logo_04.gif","logo_05.gif"};
   for(int i=0;i<assets.Length;i++){int index=i;string file=System.IO.Path.Combine(root,assets[i]);var b=Click(media,"",16+(i%3)*68,80+(i/3)*64,60,56,()=>ApplyScreenChange(()=>OriginalCoolingUi.EditorMedia(expected,"asset",index),()=>{}),source.EndsWith(assets[i])?selectedBg:railBg);if(File.Exists(file)){var bi=new BitmapImage(new Uri(file));b.Content=new Image{Source=bi,Width=50,Height=46,Stretch=Stretch.Uniform};}b.ToolTip=new[]{"黑色背景","CORSAIR 标志","iCUE 标志","彩色环","iCUE 动画","粒子动画"}[i];}
   var crop=Pane(media,250,48,350,214,railBg);string path=source.StartsWith("qrc:/")?System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ScreenRenderer","screen-runtime","resources",source.Substring(5).Replace('/',System.IO.Path.DirectorySeparatorChar)):source.StartsWith("file:")?new Uri(source).LocalPath:source;
   if(File.Exists(path)){try{Put(crop,new Image{Source=new BitmapImage(new Uri(path)),Stretch=Stretch.Uniform,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new RotateTransform(SN(imageData,"rotationAngle"))},70,2,210,210);}catch{}}
   Put(crop,new Ellipse{Stroke=Brushes.White,StrokeThickness=1,IsHitTestVisible=false},70,2,210,210);
   Label(media,"图片模糊",16,274,100,24,12);Toggle(media,SX(imageData,"blurActive")=="true",130,274,v=>ApplyScreenChange(()=>OriginalCoolingUi.EditorMedia(expected,"blur",v?1:0),()=>{}));
   var scale=new Slider{Minimum=1,Maximum=200,Value=SN(imageData,"scaling",.5)*100};Put(media,scale,250,274,200,24);
   var scaleDelay=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};scaleDelay.Tick+=delegate{scaleDelay.Stop();if(!scale.IsLoaded)return;if(screenApplying){scaleDelay.Start();return;}double requestedScale=scale.Value;ApplyScreenChange(()=>OriginalCoolingUi.EditorMedia(expected,"scale",requestedScale),()=>{});};scale.ValueChanged+=delegate{scaleDelay.Stop();scaleDelay.Start();};scale.Unloaded+=delegate{scaleDelay.Stop();};
   Click(media,"↶",478,272,48,28,()=>ApplyScreenChange(()=>OriginalCoolingUi.EditorMedia(expected,"rotate",-1),()=>{}),railBg);Click(media,"↷",536,272,48,28,()=>ApplyScreenChange(()=>OriginalCoolingUi.EditorMedia(expected,"rotate",1),()=>{}),railBg);
  }
 }
}

