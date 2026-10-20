using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
namespace CueStudio {
 sealed class LightingWizardItem {public AutomationElement Element;public string Name,Kind;public double X,Y,W,H;public bool Enabled,Selected;}
 sealed class LightingWizardState {public List<LightingWizardItem> Items=new List<LightingWizardItem>();public bool Closed,Ports;}
 static partial class OriginalCoolingUi {
  static AutomationElement WizardRoot(out Process process){process=Process.GetProcessesByName("iCUE").SingleOrDefault(p=>p.MainWindowHandle!=IntPtr.Zero);if(process==null)throw new Exception("原版 iCUE 未运行");return AutomationElement.FromHandle(process.MainWindowHandle);}
  public static LightingWizardState ReadLightingWizard(){
   Process process;var root=WizardRoot(out process);var popup=LightingPopup(root);var result=new LightingWizardState();if(popup==null){result.Closed=true;return result;}
   var bounds=popup.Current.BoundingRectangle;if(bounds.Width<10||bounds.Height<10)throw new Exception("原版向导暂时不可读取");
   var all=popup.FindAll(TreeScope.Descendants,System.Windows.Automation.Condition.TrueCondition).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen).ToArray();
   result.Ports=all.Any(e=>e.Current.Name.Contains("您希望如何设置端口"));int checkIndex=0;
   foreach(var e in all){var c=e.Current;string kind=c.ControlType==ControlType.Text?"text":c.ControlType==ControlType.Button?"button":c.ControlType==ControlType.CheckBox?"check":c.ControlType==ControlType.ComboBox?"combo":null;if(kind==null)continue;
    var b=c.BoundingRectangle;if(b.IsEmpty||b.Width<=0||b.Height<=0||double.IsInfinity(b.Left)||double.IsInfinity(b.Top)||double.IsNaN(b.Left)||double.IsNaN(b.Top))continue;double x=(b.Left-bounds.Left)*1230/bounds.Width,y=(b.Top-bounds.Top)*610/bounds.Height;if(y<42)continue;
    string name=c.Name;bool selected=false;object pattern;if(kind=="check"&&e.TryGetCurrentPattern(TogglePattern.Pattern,out pattern))selected=((TogglePattern)pattern).Current.ToggleState==ToggleState.On;
    if(kind=="check"&&result.Ports){name=checkIndex++%2==0?"运行自动检测":"手动设置";}
    if(string.IsNullOrWhiteSpace(name)&&kind=="button")continue;
    result.Items.Add(new LightingWizardItem{Element=e,Name=name,Kind=kind,X=x,Y=y,W=b.Width*1230/bounds.Width,H=b.Height*610/bounds.Height,Enabled=c.IsEnabled,Selected=selected});
   }
   if(all.Any(e=>e.Current.Name=="选择风扇系列。")){
    string[] names={"HD 风扇系列","LL 风扇系列","ML PRO RGB系列风扇","QL RGB 系列风扇","8-LED风扇系列"};
    for(int i=0;i<5;i++)result.Items.Add(new LightingWizardItem{Name=names[i],Kind="fan",X=362+(i%3)*185,Y=168+(i/3)*120,W=169,H=104,Enabled=true});
   }return result;
  }
  public static LightingWizardState LightingWizardClick(LightingWizardItem item){Process p;var root=WizardRoot(out p);var popup=LightingPopup(root);if(popup==null)throw new Exception("原版向导已关闭，请重新打开");if(item.Kind=="fan"){
    if(!popup.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text)).Cast<AutomationElement>().Any(e=>e.Current.Name=="选择风扇系列。"))throw new Exception("向导步骤已变化");
    var b=popup.Current.BoundingRectangle;var point=new NativePoint{X=(int)(b.Left+(item.X+item.W/2)*b.Width/1230),Y=(int)(b.Top+(item.Y+item.H/2)*b.Height/610)};
    if(!ScreenToClient(p.MainWindowHandle,ref point))throw new Exception("无法定位向导");var pos=new IntPtr((point.Y<<16)|(point.X&65535));PostMessage(p.MainWindowHandle,0x200,IntPtr.Zero,pos);PostMessage(p.MainWindowHandle,0x201,new IntPtr(1),pos);PostMessage(p.MainWindowHandle,0x202,IntPtr.Zero,pos);Thread.Sleep(300);return ReadLightingWizard();
   }if(item.Element.Current.IsOffscreen||!item.Element.Current.IsEnabled)throw new Exception("向导已变化，请刷新后重试");ClickControl(item.Element,p.MainWindowHandle);Thread.Sleep(300);return ReadLightingWizard();}
  public static string[] LightingWizardChoices(LightingWizardItem item){Process p;var root=WizardRoot(out p);ClickControl(item.Element,p.MainWindowHandle);for(int i=0;i<20;i++){Thread.Sleep(100);var items=EditorControls(root,ControlType.ListItem).Select(e=>e.Current.Name).Where(n=>!string.IsNullOrWhiteSpace(n)).ToArray();if(items.Length>0)return items;}throw new Exception("未读取到原版选项");}
  public static LightingWizardState LightingWizardChoose(string value){Process p;var root=WizardRoot(out p);var item=EditorControls(root,ControlType.ListItem).Single(e=>e.Current.Name==value);ClickControl(item,p.MainWindowHandle);Thread.Sleep(300);return ReadLightingWizard();}
  public static void CancelLightingWizard(){Process p;var root=WizardRoot(out p);var popup=LightingPopup(root);if(popup==null)return;var close=popup.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)).Cast<AutomationElement>().FirstOrDefault(e=>e.Current.AutomationId.EndsWith(".QQuickButton"));if(close!=null)ClickControl(close,p.MainWindowHandle);}
 }
 public partial class MainWindow {
  bool lightingWizardLive; string lightingWizardSignature="";
  System.Windows.Threading.DispatcherTimer lightingWizardRefresh;
  void RenderLightingWizard(LightingWizardState state){
   if(state.Closed){lightingWizardLive=false;CloseModal();return;}
   var signature=string.Join("|",state.Items.Select(i=>i.Kind+":"+i.Name+":"+i.Selected+":"+i.Enabled+":"+i.X+":"+i.Y));if(lightingWizardLive&&signature==lightingWizardSignature)return;lightingWizardSignature=signature;lightingWizardLive=true;var p=Modal("灯光效果设置4针向导",1230,610);
   // Replace the modal close action so closing the replica cancels the same native wizard.
   var close=p.Children.OfType<Button>().FirstOrDefault();if(close!=null)p.Children.Remove(close);
   Icon(p,"×",1194,8,CancelReplicaLightingWizard,"关闭");
   foreach(var item in state.Items){var current=item;double w=Math.Max(24,Math.Min(item.W,1230-item.X)),h=Math.Max(24,item.H);
    if(item.Kind=="text"){var t=Label(p,item.Name.Contains("拖放")?"确认设备布局后点击完成。":item.Name,item.Y<140?200:item.X,item.Y,item.Y<140?830:w,h,item.Y<100?16:12,item.Y<100?(dark?"#EEE915":"#777500"):muted);if(item.Y<140||item.W>250)t.TextAlignment=TextAlignment.Center;}
    else {var button=Click(p,item.Name+(item.Kind=="combo"?"  ▾":""),item.X,item.Y,w,h,()=>{if(current.Kind=="combo")ChooseLightingWizard(p,current);else RunLightingWizard(()=>OriginalCoolingUi.LightingWizardClick(current));},item.Selected?selectedBg:railBg);button.IsEnabled=item.Enabled;if(item.Kind=="check"&&item.Selected){button.BorderBrush=B("#B6BD32");button.BorderThickness=new Thickness(1);}}
   }
   if(state.Items.Any(i=>i.Name=="这是您的设备布局")){
    string[] ports={"环","RGB端口 #1","RGB端口 #5","RGB端口 #6"};
    for(int i=0;i<4;i++){var card=Pane(p,32+i*169,244,153,176,tileBg);Label(card,ports[i],8,0,140,24,13);if(i==0){for(int j=0;j<24;j++){double a=j*Math.PI/12;Put(card,new System.Windows.Shapes.Ellipse{Fill=B("#777777")},72+52*Math.Sin(a),96+52*Math.Cos(a),8,8);}}else Fan(card,20,44,112,true);}
   }
   if(state.Ports){var checks=state.Items.Where(i=>i.Kind=="check").ToArray();for(int i=0;i<checks.Length;i+=2)Label(p,"RGB端口 #"+(i/2+1),checks[i].X-105,checks[i].Y,98,24,12);}
   if(lightingWizardRefresh==null){lightingWizardRefresh=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};lightingWizardRefresh.Tick+=async delegate{if(!lightingWizardLive){lightingWizardRefresh.Stop();return;}if(lightingWizardBusy)return;lightingWizardBusy=true;try{var updated=await Task.Run(()=>OriginalCoolingUi.ReadLightingWizard());RenderLightingWizard(updated);}catch{lightingWizardRefresh.Stop();}finally{lightingWizardBusy=false;}};}
   lightingWizardRefresh.Start();
  }
  async void RunLightingWizard(Func<LightingWizardState> work){if(lightingWizardBusy)return;lightingWizardBusy=true;try{var state=await Task.Run(work);RenderLightingWizard(state);Activate();}catch(Exception ex){lightingWizardLive=false;Info("灯光设置未完成",ex.Message);}finally{lightingWizardBusy=false;}}
  async void ChooseLightingWizard(Canvas panel,LightingWizardItem item){if(lightingWizardBusy)return;lightingWizardBusy=true;try{var choices=await Task.Run(()=>OriginalCoolingUi.LightingWizardChoices(item));Activate();Menu(panel,item.X,item.Y,choices,index=>RunLightingWizard(()=>OriginalCoolingUi.LightingWizardChoose(choices[index])));}catch(Exception ex){Info("读取向导选项",ex.Message);}finally{lightingWizardBusy=false;}}
  async void CancelReplicaLightingWizard(){if(lightingWizardBusy)return;lightingWizardLive=false;lightingWizardBusy=true;try{await Task.Run(()=>OriginalCoolingUi.CancelLightingWizard());CloseModal();}catch(Exception ex){Info("关闭向导",ex.Message);}finally{lightingWizardBusy=false;}}
 }
}

