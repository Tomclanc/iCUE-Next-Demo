using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.IO.Path;
namespace CueStudio {public partial class MainWindow {
 void VerifyCoolingHistory(System.Collections.Generic.List<string> log,Action<string> capture){
 page="设备";tab="散热";device="H100i ELITE LCD";
 if(!IsButtonOrigin(new ComboBoxItem())||!IsButtonOrigin(new System.Windows.Controls.Primitives.ToggleButton()))throw new Exception("下拉选项被卡片点击拦截");log.Add("Cooling dropdown item and toggle excluded from history click: OK");
 foreach(var theme in new[]{"亮色","深色"}){Pref.Theme=theme;coolingEdit=true;Build();var host=surface.Children.OfType<Border>().First(e=>Canvas.GetLeft(e)==384&&Canvas.GetTop(e)==172);var card=(Canvas)host.Child;
 card.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent,Source=card});
 if(coolingEdit||coolingHistorySensor!="风扇 #5"||pendingCooling.Count!=0)throw new Exception("历史曲线卡片点击失败");capture(theme+"-风扇历史曲线");}
 coolingEdit=true;Build();capture("深色-返回预设曲线");log.Add("Cooling card click opens history without applying fan settings: OK");
 device="DOMINATOR PLATINUM RGB DDR5";foreach(var theme in new[]{"亮色","深色"}){Pref.Theme=theme;Build();capture(theme+"-内存散热");var host=surface.Children.OfType<Border>().First(e=>Canvas.GetLeft(e)==688&&Canvas.GetTop(e)==172);var card=(Canvas)host.Child;card.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent,Source=card});if(memoryCoolingSensor!="总功率消耗"||pendingCooling.Count!=0)throw new Exception("内存历史图切换失败");capture(theme+"-内存功率曲线");}log.Add("Memory cooling cards and history selection: OK");
 }
 string memoryCoolingSensor="温度";bool memoryHistoryVisible=true;
 void MemoryCooling(){
 string[] names={"温度","VDD","VDDQ","VPP","总功率消耗"};
 for(int i=0;i<names.Length;i++){string name=names[i],unit=i==0?"°C":i==4?"瓦":"伏";var card=Pane(surface,384+(i==4?304:0),172+(i==4?0:i*80),288,64,railBg);card.Background=Brushes.Transparent;
 DrawSensor(card,0,0,128,64,name,"",0,unit,i,true,device);Label(card,name,136,20,144,24,13);
 card.PreviewMouseLeftButtonUp+=(sender,e)=>{e.Handled=true;memoryCoolingSensor=name;memoryHistoryVisible=true;Build();};}
 if(memoryHistoryVisible)CoolingHistory(true);
 }
 string coolingHistorySensor;
 void CoolingHistory(bool memory=false){
 double width=memory?1278:1014,cw=width-114;
 var panel=Pane(surface,memory?96:360,512,width,312,tileBg);Label(panel,memory?memoryCoolingSensor:coolingHistorySensor,16,8,500,28,15);Icon(panel,"×",width-36,8,()=>{if(memory)memoryHistoryVisible=false;else coolingHistorySensor=null;Build();});
 string sensor=memory?memoryCoolingSensor:coolingHistorySensor,unit=memory?(sensor=="温度"?"°C":sensor=="总功率消耗"?"瓦":"伏"):(sensor=="冷却液温度"?"°C":"RPM"),color=unit=="RPM"?"#29ADFF":unit=="伏"||unit=="瓦"?"#A052DA":"#F59A34";
 var value=Label(panel,"",width-364,8,320,28,15,color);var chart=new Canvas{Width=cw,Height=202,ClipToBounds=true};Put(panel,chart,32,50,cw,202);
 var maximum=Label(panel,"",cw+40,44,70,24,11,muted);var minimum=Label(panel,"",cw+40,232,70,24,11,muted);
 Label(panel,"最近 60 次采样",32,268,250,24,12,muted);Label(panel,memory?"内存传感器历史记录":"实时转速 / 温度记录",width-294,268,250,24,12,muted);
 Action render=()=>{var r=Telemetry.Resolve(device,sensor,unit);value.Text=ReadingText(r);chart.Children.Clear();
 for(int i=0;i<=12;i++)Put(chart,new Rectangle{Fill=B(line),Opacity=.55},i*cw/12,0,1,202);
 for(int i=0;i<=5;i++)Put(chart,new Rectangle{Fill=B(line),Opacity=.55},0,i*202.0/5,cw,1);
 if(!r.Value.HasValue||r.History.Length<2){Label(chart,"等待有效采样…",20,80,500,30,13,muted);maximum.Text=minimum.Text="";return;}
 double low=r.History.Min(),high=r.History.Max(),margin=Math.Max(unit=="RPM"?20:unit=="伏"||unit=="瓦"?.05:1,(high-low)*.15);low=Math.Max(0,low-margin);high+=margin;maximum.Text=high.ToString("0.#");minimum.Text=low.ToString("0.#");
 var pts=new PointCollection();for(int i=0;i<r.History.Length;i++)pts.Add(new Point(i*cw/(r.History.Length-1),202-(r.History[i]-low)/(high-low)*202));
 var fill=new PointCollection(pts);fill.Add(new Point(cw,202));fill.Add(new Point(0,202));
 chart.Children.Add(new Polygon{Points=fill,Fill=new LinearGradientBrush(Color.FromArgb(100,B(color).Color.R,B(color).Color.G,B(color).Color.B),Color.FromArgb(5,B(color).Color.R,B(color).Color.G,B(color).Color.B),90),IsHitTestVisible=false});
 chart.Children.Add(new Polyline{Points=pts,Stroke=B(color),StrokeThickness=1.6,IsHitTestVisible=false});};render();live.Add(render);
 }
 readonly System.Collections.Generic.Dictionary<string,string> originalCoolingBindings=new System.Collections.Generic.Dictionary<string,string>();
 string OriginalCoolingBindingText(string channel,string requested){string actual;if(!originalCoolingBindings.TryGetValue(channel,out actual))return "原版已保存绑定：尚未读取";return "原版已保存绑定："+actual+(actual==requested?"":" · 本地选择未应用");}
 bool coolingApplying;string coolingApplyStatus="已有预设可即时切换；原版需停留在散热页面";string coolingTarget="风扇 #5";
 bool coolingToolsExpanded;
 void CoolingPresetMenu(System.Windows.Controls.Canvas panel,string name){var curve=DS.Curves.Find(c=>c.Name==name);if(curve==null)return;Menu(panel,0,0,new[]{"重命名","复制","删除"},action=>{if(action==0)RenameText(name,newName=>{if(DS.Curves.Any(c=>c!=curve&&c.Name==newName)){Info("名称已存在","请使用其他名称。");return;}curve.Name=newName;curve.Edited=true;if(DS.Fan==name)DS.Fan=newName;if(DS.Fan6==name)DS.Fan6=newName;if(DS.Pump==name)DS.Pump=newName;selectedCooling=newName;QueueBoundCooling(curve);Save();Build();});else if(action==1){string copyName=name+" 副本";int i=2;while(DS.Curves.Any(c=>c.Name==copyName))copyName=name+" 副本 "+i++;var copy=new CurveState{Name=copyName,Sensor=curve.Sensor,Mode=curve.Mode,Edited=true,FixedPercent=curve.FixedPercent,FixedRpm=curve.FixedRpm,Values=new System.Collections.Generic.List<double>(curve.Values),Temperatures=new System.Collections.Generic.List<double>(curve.Temperatures)};DS.Curves.Add(copy);selectedCooling=copyName;coolingEdit=true;Save();Build();}else{if(DS.Fan==name||DS.Fan6==name||DS.Pump==name){Info("预设正在使用","请先为使用它的风扇或泵选择其他预设。");return;}if(!string.IsNullOrEmpty(curve.OriginalId))SetOption("hiddenCooling:"+curve.OriginalId,"1");DS.Curves.Remove(curve);if(selectedCooling==name){selectedCooling=DS.Curves.Select(c=>c.Name).FirstOrDefault()??"";coolingEdit=false;}Save();Build();}});}
 System.Windows.Threading.DispatcherTimer coolingTimer;
 readonly System.Collections.Generic.Dictionary<int,CurveState> pendingCooling=new System.Collections.Generic.Dictionary<int,CurveState>();
 CurveState CoolingSnapshot(CurveState c){return new CurveState{Name=c.Name,OriginalId=c.OriginalId,Edited=c.Edited,Sensor=c.Sensor,Mode=c.Mode,FixedPercent=c.FixedPercent,FixedRpm=c.FixedRpm,Temperatures=new System.Collections.Generic.List<double>(c.Temperatures),Values=new System.Collections.Generic.List<double>(c.Values)};}
 void QueueCooling(CurveState curve,int fan,bool manual=false){if(Store.EndsWith("test-settings.xml"))return;if(!manual){coolingApplyStatus="曲线修改仅保存在副本，尚未下发";return;}pendingCooling[fan]=CoolingSnapshot(curve);coolingApplyStatus="等待手动应用（将重载 iCUE）";if(coolingTimer==null){coolingTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};coolingTimer.Tick+=(s,e)=>{if(coolingApplying)return;coolingTimer.Stop();FlushCooling();};}coolingTimer.Stop();coolingTimer.Start();}
 async void AssignCoolingLive(string label,string name){
 if(Store.EndsWith("test-settings.xml"))return;
 if(coolingApplying||memoryModeApplying||screenApplying){coolingApplyStatus="正在切换，请稍后再试";Build();return;}
 var curve=DS.Curves.Find(c=>c.Name==name);
 if(curve==null||string.IsNullOrEmpty(curve.OriginalId)||curve.Edited){coolingApplyStatus="该预设有本地修改，尚不能即时应用；请先在原版保存预设";Build();return;}
 int channel=label=="泵"?0:label=="风扇 #6"?6:5;
 coolingApplying=true;coolingApplyStatus="正在通过原版菜单切换 "+label+"…";Build();
 try{var actual=await Task.Run(()=>OriginalCoolingUi.Assign(channel,curve.OriginalId,curve.Name));
 P.Cooler.Fan=actual[0];P.Cooler.Fan6=actual[1];P.Cooler.Pump=actual[2];P.Cooler.CoolingBindingsEdited=false;
 originalCoolingBindings["风扇 #5"]=actual[0];originalCoolingBindings["风扇 #6"]=actual[1];originalCoolingBindings["泵"]=actual[2];
 File.AppendAllText(Path.Combine(Path.GetDirectoryName(Store),"cooling-live.log"),DateTime.Now.ToString("s")+" "+label+" -> "+name+"; readback="+string.Join(" / ",actual)+Environment.NewLine);
coolingApplyStatus=label+" 已切换为 "+name+"，等待实际转速稳定";Save();
 }catch(Exception ex){coolingApplyStatus="未切换："+ex.Message;}
 finally{coolingApplying=false;Build();Activate();}
 }
 bool memoryModeApplying;
 async void SetMemoryModeLive(bool enabled){
 if(memoryModeApplying||coolingApplying||screenApplying){Build();return;}
 if(Store.EndsWith("test-settings.xml")){DS.MemoryMode=enabled;Build();return;}
 string selected=device;var state=DS;memoryModeApplying=true;string error=null;
 try{bool actual=await Task.Run(()=>OriginalCoolingUi.SetMemoryMode(selected,enabled));state.MemoryMode=actual;editing=false;activeLayer=-1;Save();
 File.AppendAllText(Path.Combine(Path.GetDirectoryName(Store),"cooling-live.log"),DateTime.Now.ToString("s")+" "+selected+" MemoryMode="+actual+Environment.NewLine);
 }catch(Exception ex){error=ex.Message;}
 finally{memoryModeApplying=false;Build();Activate();}
 if(error!=null)Info("设备内存模式未切换",error);
 }
 void QueueBoundCooling(CurveState curve){if(DS.Fan==curve.Name)QueueCooling(curve,5);if(DS.Fan6==curve.Name)QueueCooling(curve,6);}
 void ApplyCooling(){QueueCooling(CurrentCurve(),coolingTarget=="风扇 #6"?6:5,true);}
 string CoolingQuote(string s){return "\""+(s??"").Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t")+"\"";}
 string CoolingJson(int fan,CurveState curve){
 if(!curve.Edited&&!string.IsNullOrEmpty(curve.OriginalId))return "{\"fan\":"+fan+",\"name\":"+CoolingQuote(curve.Name)+",\"originalId\":"+CoolingQuote(curve.OriginalId)+",\"edited\":false}";
 if(curve.Sensor!="H100i ELITE LCD 冷却液温度"&&curve.Mode=="自定义曲线")throw new Exception("CPU 温度曲线尚未接通，未下发。");
 var ts=curve.Mode=="自定义曲线"?curve.Temperatures.ToArray():new double[]{20,100};var vs=curve.Mode=="自定义曲线"?curve.Values.ToArray():new double[]{curve.FixedPercent,curve.FixedPercent};
 if(ts.Length<2||ts.Length>7||vs.Length!=ts.Length||ts.Where((v,i)=>double.IsNaN(v)||double.IsInfinity(v)||v<20||v>100||(i>0&&v<=ts[i-1])).Any()||vs.Any(v=>double.IsNaN(v)||double.IsInfinity(v)||v<20||v>100))throw new Exception("需使用 2–7 个递增节点，温度 20–100°C，占空比 20–100%。");
 if(curve.Mode=="固定 RPM"&&(curve.FixedRpm<500||curve.FixedRpm>3000))throw new Exception("支持的目标转速为 500–3000 RPM。");
 return "{\"name\":"+CoolingQuote(curve.Name)+",\"originalId\":"+CoolingQuote(curve.OriginalId)+",\"edited\":"+(curve.Edited?"true":"false")+",\"fan\":"+fan+",\"mode\":\""+(curve.Mode=="固定 RPM"?"rpm":"curve")+"\",\"rpm\":"+Math.Round(curve.FixedRpm).ToString(CultureInfo.InvariantCulture)+",\"points\":["+string.Join(",",ts.Select((v,i)=>"{\"temperature\":"+Math.Round(v).ToString(CultureInfo.InvariantCulture)+",\"duty\":"+Math.Round(vs[i]).ToString(CultureInfo.InvariantCulture)+"}"))+"]}";
 }
 async void FlushCooling(){if(coolingApplying||pendingCooling.Count==0)return;var batch=pendingCooling.ToArray();pendingCooling.Clear();string failure=null;try{
 string json="{\"requests\":["+string.Join(",",batch.Select(k=>CoolingJson(k.Key,k.Value)))+"]}";
 string script=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Apply-IcueCooling.ps1");if(!File.Exists(script))throw new Exception("缺少调速助手文件。");
 string dir=Path.Combine(Path.GetDirectoryName(Store),"cooling-control");Directory.CreateDirectory(dir);string request=Path.Combine(dir,"request-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(request,json);
 coolingApplying=true;coolingApplyStatus="手动应用中，正在重载 iCUE…";Save();if(page=="设备"&&tab=="散热")Build();
 await Task.Run(()=>{var psi=new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -RequestPath \""+request+"\" -Apply",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};using(var process=Process.Start(psi)){var stderr=process.StandardError.ReadToEndAsync();string stdout=process.StandardOutput.ReadToEnd();process.WaitForExit();string error=stderr.Result;File.WriteAllText(request+".log",stdout+"\n"+error);if(process.ExitCode!=0)throw new Exception(error);}});
 coolingApplyStatus="已下发，等待实际转速稳定";P.Cooler.CoolingBindingsEdited=true;Save();
 // Allow the original process to initialize before applying another queued batch.
 await Task.Delay(8000);
 if(pendingCooling.Count==0){foreach(var item in batch){var local=P.Cooler.Curves.Find(c=>c.Name==item.Value.Name);if(local!=null&&CoolingJson(item.Key,local)==CoolingJson(item.Key,item.Value))local.Edited=false;}P.Cooler.CoolingBindingsEdited=false;originalCoolingStamp=DateTime.MinValue;originalProfileStamp=DateTime.MinValue;SyncOriginalProfile();Save();}
 }catch(Exception ex){coolingApplyStatus="未应用："+ex.Message;failure=ex.Message;}finally{coolingApplying=false;if(page=="设备"&&tab=="散热")Build();if(pendingCooling.Count>0){coolingTimer.Stop();coolingTimer.Start();}}if(failure!=null)Info("调速未应用",failure);}
}}


