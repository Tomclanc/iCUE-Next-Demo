using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Xml.Linq;
namespace CueStudio {
 static partial class OriginalCoolingUi {
  internal static readonly string[] ScreenClasses={"ConcentricFill","SingleBar","FadeFill","Monitor","Image","ApertureFill","DynamicBar","PatternFill","DualBar","Turbo","Blank","Constructor"};
  static AutomationElement ScreenRoot(out Process process){
   process=Process.GetProcessesByName("iCUE").SingleOrDefault(p=>p.MainWindowHandle!=IntPtr.Zero);
   if(process==null)throw new Exception("请打开原版 iCUE 的 H100i 屏幕设置页面");
   var root=AutomationElement.FromHandle(process.MainWindowHandle);
   if(!root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)).Cast<AutomationElement>().Any(e=>e.Current.Name=="定制屏幕列表"))
    throw new Exception("请将原版停留在 H100i ELITE LCD 的屏幕设置页面");
   return root;
  }
  static string SavedScreenClass(){
   var files=Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Corsair","CUE5","profiles"),"*.cueprofiledata");
   if(files.Length!=1)throw new Exception("无法唯一识别当前原版配置文件");
   var root=XElement.Load(files[0]);
   var node=root.Descendants().First(e=>(string)e.Element("key")=="LightingNode");
   return node.Descendants("effect").Select(e=>(string)e.Element("polymorphic_name")).FirstOrDefault(n=>n!=null&&n.EndsWith("ScreenEffectSettings"));
  }
  public static void SelectScreen(int index){
   Process process;var root=ScreenRoot(out process);
   var heading=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)).Cast<AutomationElement>().Single(e=>e.Current.Name=="预置"&&e.Current.AutomationId.Contains("GridHeadingDelegate"));
   var rect=heading.Current.BoundingRectangle;
   var scroll=TreeWalker.ControlViewWalker.GetParent(heading);
   var bars=scroll.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ScrollBar)).Cast<AutomationElement>();
   foreach(var bar in bars){object pat;if(bar.TryGetCurrentPattern(RangeValuePattern.Pattern,out pat))((RangeValuePattern)pat).SetValue(((RangeValuePattern)pat).Current.Minimum);}
   Thread.Sleep(100);rect=heading.Current.BoundingRectangle;
   double scale=rect.Width/232.0;
   var point=new NativePoint{X=(int)(rect.Left+(index%3+.5)*rect.Width/3),Y=(int)(rect.Bottom+(24+64*(index/3))*scale)};
   if(!scroll.Current.BoundingRectangle.Contains(new System.Windows.Point(point.X,point.Y)))throw new Exception("原版屏幕预置不在可见区域，请扩大原版窗口");
   if(!ScreenToClient(process.MainWindowHandle,ref point))throw new Exception("原版屏幕坐标不可用");
   var pos=new IntPtr((point.Y<<16)|(point.X&65535));
   PostMessage(process.MainWindowHandle,0x201,new IntPtr(1),pos);PostMessage(process.MainWindowHandle,0x202,IntPtr.Zero,pos);
   string expected=ScreenClasses[index]+"ScreenEffectSettings";
   for(int i=0;i<50;i++){Thread.Sleep(100);try{if(SavedScreenClass()==expected)return;}catch(IOException){}}
   throw new Exception("原版尚未确认屏幕类型，请检查原版屏幕设置");
  }
  public static void SetScreenNumber(int index,double value){
   Process process;var root=ScreenRoot(out process);
   if(SavedScreenClass()!="DualBarScreenEffectSettings")throw new Exception("请先在原版选择“双条”屏幕类型");
   var fields=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit)).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen&&e.Current.AutomationId.Contains("TextFieldWithMenu")).ToArray();
   if(fields.Length!=5)throw new Exception("原版双条参数布局不匹配");
   var field=fields[index];var pattern=(ValuePattern)field.GetCurrentPattern(ValuePattern.Pattern);
   string text=value.ToString("0",System.Globalization.CultureInfo.InvariantCulture);
   pattern.SetValue(text);
   ClickControl(field,process.MainWindowHandle);
   PostMessage(process.MainWindowHandle,0x100,new IntPtr(13),IntPtr.Zero);
   PostMessage(process.MainWindowHandle,0x101,new IntPtr(13),IntPtr.Zero);
   Thread.Sleep(200);
   if(pattern.Current.Value!=text)throw new Exception("原版未接受该参数");
  }
  public static void SetScreenColor(int index,string color,string expected="DualBarScreenEffectSettings"){
   Process process;var root=ScreenRoot(out process);
   if(SavedScreenClass()!=expected)throw new Exception("原版屏幕类型已改变，请刷新后重试");
   var colors=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)).Cast<AutomationElement>().Where(e=>e.Current.AutomationId.Contains("ColorCellWithColorPicker")&&!e.Current.IsOffscreen).ToArray();
   if(index<0||index>=colors.Length)throw new Exception("原版颜色设置布局不匹配");
   ClickControl(colors[index],process.MainWindowHandle);
   AutomationElement popup=null;
   for(int i=0;i<20&&popup==null;i++){Thread.Sleep(100);popup=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Window)).Cast<AutomationElement>().FirstOrDefault(e=>e.Current.AutomationId.EndsWith(".ColorPickerPopup"));}
   if(popup==null)throw new Exception("原版未打开颜色面板");
   var fields=popup.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit)).Cast<AutomationElement>().ToArray();
   if(fields.Length!=4)throw new Exception("无法识别原版十六进制颜色输入框");
   var field=fields[3];var pattern=(ValuePattern)field.GetCurrentPattern(ValuePattern.Pattern);
   pattern.SetValue(color.TrimStart('#'));
   ClickControl(field,process.MainWindowHandle);
   PostMessage(process.MainWindowHandle,0x100,new IntPtr(13),IntPtr.Zero);PostMessage(process.MainWindowHandle,0x101,new IntPtr(13),IntPtr.Zero);
   Thread.Sleep(200);
   if(!pattern.Current.Value.Equals(color.TrimStart('#'),StringComparison.OrdinalIgnoreCase))throw new Exception("原版未接受颜色值");
   var close=popup.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)).Cast<AutomationElement>().Single(e=>e.Current.AutomationId.EndsWith(".QQuickButton"));
   ClickControl(close,process.MainWindowHandle);
  }
  public static void SetScreenSensor(int index,string name){
   Process process;var root=ScreenRoot(out process);
   var combos=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ComboBox)).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen).OrderBy(e=>e.Current.BoundingRectangle.Left).ToArray();
   if(combos.Length!=2||SavedScreenClass()!="DualBarScreenEffectSettings")throw new Exception("请先选择“双条”屏幕类型");
   var combo=combos[index];if(combo.Current.Name==name)return;
   ClickControl(combo,process.MainWindowHandle);Thread.Sleep(200);
   var search=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit)).Cast<AutomationElement>().Single(e=>e.Current.AutomationId.Contains(".ComboBoxPopup."));
   ((ValuePattern)search.GetCurrentPattern(ValuePattern.Pattern)).SetValue(name);Thread.Sleep(200);
   var items=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem)).Cast<AutomationElement>().Where(e=>e.Current.AutomationId.Contains(".ComboBoxPopup.")&&e.Current.Name==name&&!e.Current.IsOffscreen).ToArray();
   if(items.Length!=1)throw new Exception("原版传感器菜单中未找到："+name);
   ClickControl(items[0],process.MainWindowHandle);
   for(int i=0;i<20&&combo.Current.Name!=name;i++)Thread.Sleep(100);
   if(combo.Current.Name!=name)throw new Exception("原版未确认传感器选择");
  }
 }
 public partial class MainWindow {
  bool screenApplying;
  async void ApplyScreenChange(Action apply,Action confirmed){
   if(screenApplying||memoryModeApplying||coolingApplying){Build();return;}
   if(Store.EndsWith("test-settings.xml")){confirmed();Build();return;}
   screenApplying=true;string error=null;
   try{await Task.Run(apply);confirmed();Save();}
   catch(Exception ex){error=ex.Message;}
   finally{screenApplying=false;Build();Activate();}
   if(error!=null)Info("屏幕设置未确认",error);
  }
  void SelectScreenLive(string type){var state=DS;int i=Array.IndexOf(screenTypes,type);ApplyScreenChange(()=>OriginalCoolingUi.SelectScreen(i),()=>state.ScreenType=type);}
  void ScreenColorLive(int index,string color){var state=DS;ApplyScreenChange(()=>OriginalCoolingUi.SetScreenColor(index,color),()=>{if(index==0)state.ScreenColor1=color;else if(index==1)state.ScreenColor2=color;else if(index==2)state.ScreenBackground=color;else state.ScreenLabel=color;});}
  void ScreenSensorLive(int index,string name){var state=DS;ApplyScreenChange(()=>OriginalCoolingUi.SetScreenSensor(index,name),()=>{if(index==0)state.Sensor1=name;else state.Sensor2=name;});}
  void ScreenNumberLive(int index,double value){
   var state=DS;
   if(value!=(int)value||value<(index==4?1:0)||value>(index==4?30:100)){Info("数值范围","温度范围为 0–100，帧率为 1–30。");return;}
   if((index==0&&value<=state.MinTemp)||(index==1&&value>=state.MaxTemp)||(index==2&&value<=state.MinTemp2)||(index==3&&value>=state.MaxTemp2)){Info("温度范围","最高温度必须大于最低温度。");return;}
   ApplyScreenChange(()=>OriginalCoolingUi.SetScreenNumber(index,value),()=>{
    if(index==0)state.MaxTemp=value;else if(index==1)state.MinTemp=value;else if(index==2)state.MaxTemp2=value;else if(index==3)state.MinTemp2=value;else state.Fps=value;
   });
  }
 }
}
