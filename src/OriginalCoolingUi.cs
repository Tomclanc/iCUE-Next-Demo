using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using System.Xml.Linq;
namespace CueStudio {
 static partial class OriginalCoolingUi {
  [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h,ref NativePoint p);
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
  static void ClickControl(AutomationElement element,IntPtr fallback){
   var rect=element.Current.BoundingRectangle;if(rect.IsEmpty||rect.Width<2||rect.Height<2)throw new Exception("原版控件位置无效");
   IntPtr hwnd=IntPtr.Zero;var parent=element;for(int i=0;i<16&&parent!=null;i++){int h=parent.Current.NativeWindowHandle;if(h!=0){hwnd=new IntPtr(h);break;}parent=TreeWalker.ControlViewWalker.GetParent(parent);}if(hwnd==IntPtr.Zero)hwnd=fallback;
   var point=new NativePoint{X=(int)(rect.Left+rect.Width/2),Y=(int)(rect.Top+rect.Height/2)};
   if(!ScreenToClient(hwnd,ref point))throw new Exception("无法定位原版控件");
   var pos=new IntPtr((point.Y<<16)|(point.X&65535));PostMessage(hwnd,0x200,IntPtr.Zero,pos);PostMessage(hwnd,0x201,new IntPtr(1),pos);PostMessage(hwnd,0x202,IntPtr.Zero,pos);
  }
  public static bool SetMemoryMode(string device,bool enabled){
   int target=device=="H100i ELITE LCD"?0:device=="DOMINATOR PLATINUM RGB DDR5"?1:-1;
   if(target<0)throw new Exception("尚未识别该设备的原版开关");
   var processes=Process.GetProcessesByName("iCUE").Where(p=>p.MainWindowHandle!=IntPtr.Zero).ToArray();
   if(processes.Length!=1)throw new Exception("请打开原版 iCUE 的设备页面");
   var process=processes[0];var root=AutomationElement.FromHandle(process.MainWindowHandle);
   var devices=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen&&System.Text.RegularExpressions.Regex.IsMatch(e.Current.AutomationId,@"\.DevicesList_QMLTYPE_\d+$")).OrderBy(e=>e.Current.BoundingRectangle.Left).ToArray();
   if(devices.Length!=2)throw new Exception("原版设备栏与已验证的双设备布局不一致，请打开设备页面");
   ClickControl(devices[target],process.MainWindowHandle);Thread.Sleep(350);
   Func<AutomationElement> find=()=>root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.CheckBox)).Cast<AutomationElement>().Single(e=>!e.Current.IsOffscreen&&System.Text.RegularExpressions.Regex.IsMatch(e.Current.AutomationId,@"\.Switch_QMLTYPE_\d+$"));
   var toggle=find();object pattern;
   if(!toggle.TryGetCurrentPattern(TogglePattern.Pattern,out pattern))throw new Exception("原版未提供开关状态，已取消切换");
   Func<bool> read=()=>((TogglePattern)find().GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState==ToggleState.On;
   if(read()!=enabled){ClickControl(toggle,process.MainWindowHandle);for(int i=0;i<40;i++){Thread.Sleep(100);if(read()==enabled)break;}}
   if(read()!=enabled)throw new Exception("原版未确认设备内存模式切换");
   return enabled;
  }
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd,int command);
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
  static AutomationElement LightingPopup(AutomationElement root){return root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Window)).Cast<AutomationElement>().FirstOrDefault(e=>e.Current.AutomationId.EndsWith(".LightingSetupWizardPopup")&&!e.Current.IsOffscreen);}
  static AutomationElement WaitLighting(Func<AutomationElement> find){for(int i=0;i<40;i++){var e=find();if(e!=null)return e;Thread.Sleep(100);}throw new Exception("原版向导未能打开，请关闭原版中其他弹窗后重试。");}
  public static void ContinueLightingWizard(){
   var process=Process.GetProcessesByName("iCUE").SingleOrDefault(p=>p.MainWindowHandle!=IntPtr.Zero);
   if(process==null)throw new Exception("原版 iCUE 未运行，请启动后重试。");
   if(IsIconic(process.MainWindowHandle))ShowWindow(process.MainWindowHandle,9);
   var root=AutomationElement.FromHandle(process.MainWindowHandle);
   var popup=LightingPopup(root);
   if(popup==null){
    var devices=EditorControls(root,ControlType.Button,".DevicesList_QMLTYPE_").OrderBy(e=>e.Current.BoundingRectangle.Left).ToArray();
    if(devices.Length!=2)throw new Exception("未识别到原版设备栏，请打开原版设备页面后重试。");
    ClickControl(devices[0],process.MainWindowHandle);
    var setup=WaitLighting(()=>EditorControls(root,ControlType.Button,".BaseSubScreenItemView_QMLTYPE_").FirstOrDefault(e=>e.Current.AutomationId.Contains(".ComponentLoader_QMLTYPE_")&&System.Text.RegularExpressions.Regex.Matches(e.Current.AutomationId,"ComponentLoader_QMLTYPE_").Count==2));
    ClickControl(setup,process.MainWindowHandle);
    var run=WaitLighting(()=>EditorControls(root,ControlType.Button).SingleOrDefault(e=>e.Current.Name=="运行安装向导"));
    ClickControl(run,process.MainWindowHandle);
    popup=WaitLighting(()=>LightingPopup(root));
   }
   var intro=popup.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text)).Cast<AutomationElement>().Any(e=>e.Current.Name=="这是您的4针端口");
   if(intro){
    var next=popup.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)).Cast<AutomationElement>().SingleOrDefault(e=>e.Current.Name=="下一步"&&e.Current.IsEnabled);
    if(next==null)throw new Exception("原版向导尚未就绪，请重试。");
    ClickControl(next,process.MainWindowHandle);
    WaitLighting(()=>{var current=LightingPopup(root);return current!=null&&current.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text)).Cast<AutomationElement>().Any(e=>e.Current.Name.Contains("您希望如何设置端口"))?current:null;});
   }
  }
  const string Serial="430a201134895aaab9f09f061091005f";
  static string Local(string s){return s=="Quiet"?"静音":s=="Balanced"?"均衡":s=="Extreme"?"极限":s=="Zero RPM"?"零 RPM":s=="Variable Speed"?"变速":s;}
  public static string[] Assign(int channel,string presetId,string name){
   if(channel!=5&&channel!=6&&channel!=0)throw new Exception("未知散热通道");
   var processes=Process.GetProcessesByName("iCUE").Where(p=>p.MainWindowHandle!=IntPtr.Zero).ToArray();
   if(processes.Length!=1)throw new Exception("请打开原版 iCUE 并停留在 H100i ELITE LCD 的散热页面");
   var process=processes[0];int pid=process.Id;var root=AutomationElement.FromHandle(process.MainWindowHandle);
   var library=XElement.Load(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Corsair","CUE5","cooling","configurations"));
   var storage=library.Descendants("storages").Elements().Single(e=>(string)e.Element("key")==Serial);
   var records=storage.Descendants("software").Descendants("configurations").Elements().Select(e=>e.Element("ptr_wrapper")).Where(e=>e!=null).Select(e=>e.Element("data")).Where(e=>e!=null&&e.Element("base")!=null).Select(e=>e.Element("base")).ToArray();
   var menu=records.Where(e=>channel==0?new[]{"Quiet","Balanced","Extreme"}.Contains((string)e.Element("name")):(string)e.Element("name")!="Variable Speed").ToArray();
   int index=Array.FindIndex(menu,e=>(string)e.Element("id")==presetId&&Local((string)e.Element("name"))==name);
   if(index<0)throw new Exception("该预设尚未存在于原版的通道菜单中");
   var combos=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ComboBox)).Cast<AutomationElement>().Where(e=>e.Current.AutomationId.Contains("CueComboBox")&&e.Current.AutomationId.Contains("_QML_")&&!e.Current.IsOffscreen).OrderBy(e=>e.Current.BoundingRectangle.Top).ToArray();
   if(combos.Length!=3||combos.Select(e=>e.Current.BoundingRectangle.Left).Max()-combos.Select(e=>e.Current.BoundingRectangle.Left).Min()>5)throw new Exception("原版未显示三个散热通道，请停留在 H100i ELITE LCD 散热页面");
   int target=channel==5?0:channel==6?1:2;var before=combos.Select(e=>e.Current.Name).ToArray();
   if(before[target]==name)return before;
   object pattern;
   ClickControl(combos[target],process.MainWindowHandle);
   try {
    AutomationElement[] items=null;
    for(int i=0;i<20;i++){items=AutomationElement.RootElement.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty,pid),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem))).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen).OrderBy(e=>e.Current.BoundingRectangle.Top).ToArray();if(items.Length>0)break;Thread.Sleep(50);}
    if(items==null||items.Length!=menu.Length)throw new Exception("原版菜单与预设列表不一致，已取消切换："+(items==null?0:items.Length)+" / "+menu.Length+"");
    var item=items[index];string accessible=item.Current.Name;
    if(!string.IsNullOrWhiteSpace(accessible)&&accessible!=name)throw new Exception("原版菜单名称不匹配，已取消切换");
    ClickControl(item,process.MainWindowHandle);
    for(int i=0;i<30&&combos[target].Current.Name!=name;i++)Thread.Sleep(100);
    if(combos[target].Current.Name!=name)throw new Exception("原版未确认预设切换");
    for(int i=0;i<3;i++)if(i!=target&&combos[i].Current.Name!=before[i])throw new Exception("检测到其他通道变化，请检查原版绑定");
    if(process.HasExited||process.Id!=pid)throw new Exception("原版进程已退出，无法确认结果");
    return combos.Select(e=>e.Current.Name).ToArray();
   } finally {try{if(combos[target].TryGetCurrentPattern(ExpandCollapsePattern.Pattern,out pattern))((ExpandCollapsePattern)pattern).Collapse();}catch{}}
  }
 }
}
