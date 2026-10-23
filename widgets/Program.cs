using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Windows.Widgets.Providers;
using WinRT;
namespace CueWidgets;
[ComVisible(true),Guid("BC51EA37-A46D-48D5-BD08-F5DA86371234")]
public sealed class Provider : IWidgetProvider {
 readonly System.Collections.Concurrent.ConcurrentDictionary<string,bool> active=new();
 readonly System.Threading.Timer timer;
 readonly long started=Environment.TickCount64;
 long lastRefresh;int ticking;bool enumerated;
 public Provider(){
  // Return promptly to the widget host; neither COM enumeration nor bridge launch blocks activation.
  timer=new System.Threading.Timer(_=>Tick(),null,0,1000);
  Log("Provider started asynchronously; shared root="+DataRoot);
 }
 void Tick(){
  if(System.Threading.Interlocked.Exchange(ref ticking,1)!=0)return;
  try{
   EnsureBridge();
   if(!enumerated){try{var infos=WidgetManager.GetDefault()?.GetWidgetInfos();if(infos!=null){foreach(var info in infos)active.TryAdd(info.WidgetContext.Id,true);enumerated=true;}}catch(Exception ex){Log("Host not ready; retrying: "+ex.Message);}}
   long now=Environment.TickCount64;
   if(now-started>60000&&now-lastRefresh<5000)return;
   lastRefresh=now;
   foreach(var pair in active)if(pair.Value)Update(pair.Key);
  }catch(Exception ex){Log("Startup retry: "+ex.Message);}
  finally{System.Threading.Interlocked.Exchange(ref ticking,0);}
 }
 public void CreateWidget(WidgetContext c){active[c.Id]=true;Update(c.Id);Log("Created "+c.Id);}
 public void DeleteWidget(string id,string state){active.TryRemove(id,out _);Log("Deleted "+id);}
 public void Activate(WidgetContext c){active[c.Id]=true;Update(c.Id);}
 public void Deactivate(string id){active[id]=false;}
 public void OnWidgetContextChanged(WidgetContextChangedArgs a){Update(a.WidgetContext.Id);}
 readonly Dictionary<string,long> clicks=new();
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr h,int cmd);
 [DllImport("user32.dll")] static extern uint GetDoubleClickTime();
 static void OpenNext(){
  var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Corsair","Corsair iCUE5 Software","iCUE.exe");
  foreach(var proc in System.Diagnostics.Process.GetProcessesByName("iCUE")){try{if(proc.MainModule?.FileName==exe&&proc.MainWindowHandle!=IntPtr.Zero){ShowWindowAsync(proc.MainWindowHandle,9);SetForegroundWindow(proc.MainWindowHandle);Log("Opened official iCUE existing window");return;}}catch{}}
  System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)!});
 }
 static System.Diagnostics.Process? bridge;
 static readonly object bridgeLock=new();
 static long nextBridgeAttempt,bridgeStarted;
 static void EnsureBridge(){lock(bridgeLock){try{
  if(bridge!=null&&!bridge.HasExited){
   // This bridge writes a waiting heartbeat even before iCUE's sensors are ready.
   var stamp=File.GetLastWriteTimeUtc(Path.Combine(DataRoot,"original-sensors.xml"));
   if(Environment.TickCount64-bridgeStarted<30000||DateTime.UtcNow-stamp<TimeSpan.FromSeconds(30))return;
   Log("Bridge heartbeat stalled; restarting bridge only");
   bridge.Kill(true);bridge.WaitForExit(2000);
  }
  if(Environment.TickCount64<nextBridgeAttempt)return;
  nextBridgeAttempt=Environment.TickCount64+2000;
  Directory.CreateDirectory(DataRoot);
  var si=new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"sensor-bridge","CueSensorBridge.exe")){UseShellExecute=false,CreateNoWindow=true};
  si.ArgumentList.Add(Path.Combine(DataRoot,"original-sensors.xml"));si.ArgumentList.Add(Environment.ProcessId.ToString());
  bridge=System.Diagnostics.Process.Start(si);bridgeStarted=Environment.TickCount64;
 }catch(Exception ex){Log("Bridge: "+ex.Message);}}}
 static readonly Dictionary<string,(double Sample,List<double> Values)> history=new();
 static readonly object historyLock=new();
 static XElement Reading(string key,string name,XElement sensor){
  var value=(string?)sensor.Attribute("value")??"";
  var result=new XElement("Sensor",new XAttribute("key",key),new XAttribute("name",name),new XAttribute("unit","°C"),new XAttribute("value",value));
  if(double.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)){
   double.TryParse((string?)sensor.Attribute("sampled"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var sampled);
   lock(historyLock){if(!history.TryGetValue(key,out var h))h=(-1,new List<double>());if(h.Sample!=sampled){h.Values.Add(n);if(h.Values.Count>60)h.Values.RemoveAt(0);h.Sample=sampled;history[key]=h;}result.SetAttributeValue("history",string.Join(",",h.Values.Select(v=>v.ToString(System.Globalization.CultureInfo.InvariantCulture))));}
  }
  return result;
 }
 static XElement ReadOfficial(){
  EnsureBridge();
  using var input=new FileStream(Path.Combine(DataRoot,"original-sensors.xml"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
  var source=XElement.Load(input);
  double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000d;
  var sensors=source.Elements("Sensor").Where(s=>double.TryParse((string?)s.Attribute("sampled"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var t)&&now-t>=-2&&now-t<12).ToArray();
  string Dev(XElement e)=>((string?)e.Attribute("DeviceName")??"").ToLowerInvariant();
  string Name(XElement e)=>((string?)e.Attribute("Name")??"").ToLowerInvariant();
  bool Temp(XElement e)=>((string?)e.Attribute("Units")??"").Contains("C")||((string?)e.Attribute("Type")??"")=="Temperature";
  var temps=sensors.Where(Temp).ToArray();
  var cpu=temps.Where(e=>Dev(e).Contains("ryzen")||Dev(e).Contains("core")).OrderByDescending(e=>Name(e).Contains("package")||Name(e).Contains("封装")).FirstOrDefault();
  var gpu=temps.Where(e=>Dev(e).Contains("nvidia")||Dev(e).Contains("geforce")).OrderByDescending(e=>Name(e).Contains("core")||Name(e).Contains("核心")).FirstOrDefault();
  var coolant=temps.FirstOrDefault(e=>Dev(e).Contains("h100")&&(Name(e).Contains("coolant")||Name(e).Contains("冷却")));
  var result=new XElement("Telemetry",new XAttribute("time",DateTime.UtcNow.ToString("o")));
  foreach(var pair in new[]{("CPU 温度",cpu),("GPU 温度",gpu),("冷却液温度",coolant)})if(pair.Item2!=null)result.Add(Reading(pair.Item1,pair.Item1,pair.Item2));
  foreach(var sensor in temps)result.Add(Reading("id:"+(string?)sensor.Attribute("id"),(string?)sensor.Attribute("DeviceName")+" · "+(string?)sensor.Attribute("Name"),sensor));
  if(!result.HasElements)result.SetAttributeValue("time",DateTime.UnixEpoch.ToString("o"));return result;
 }

 public void OnActionInvoked(WidgetActionInvokedArgs a){try{
  Log("Action: "+a.Verb);if(a.Verb=="open-next"){OpenNext();return;}
  if(a.Verb=="tap-next"){bool open=false;lock(clicks){long now=Environment.TickCount64;if(clicks.TryGetValue(a.WidgetContext.Id,out var last)&&now-last<=GetDoubleClickTime()){clicks.Remove(a.WidgetContext.Id);open=true;}else clicks[a.WidgetContext.Id]=now;}if(open)OpenNext();return;}
  active[a.WidgetContext.Id]=true;Update(a.WidgetContext.Id);
 }catch(Exception ex){Log("Widget action failed: "+ex);}}
  [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(in Guid id,uint flags,IntPtr token,out IntPtr path);
 static string SharedRoot(){var id=new Guid("F1B32785-6FBA-4FCF-9D55-7B8E7F157091");Marshal.ThrowExceptionForHR(SHGetKnownFolderPath(in id,0x00010000,IntPtr.Zero,out var path));try{return Path.Combine(Marshal.PtrToStringUni(path)!,"CueOfficialWidgets");}finally{Marshal.FreeCoTaskMem(path);}}
 public static readonly string DataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"CueOfficialWidgets",System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value);
 static string Option(string key,string fallback){
  try{var config=Path.Combine(Path.GetDirectoryName(SharedRoot())!,"Corsair","iCUEAppearancePatch","widgets.json");using var json=JsonDocument.Parse(File.ReadAllText(config));if(json.RootElement.TryGetProperty(key,out var value))return value.GetString()??fallback;}catch{}
  try{var doc=XDocument.Load(Path.Combine(DataRoot,"settings.xml"));return doc.Descendants("LocalSetting").FirstOrDefault(e=>(string?)e.Element("Key")==key)?.Element("Value")?.Value??fallback;}catch{return fallback;}}
 static string? Chart(string? csv){
  var values=new List<double>();foreach(var item in (csv??"").Split(',')){if(double.TryParse(item,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n))values.Add(n);}if(values.Count<2)return null;
  using var bitmap=new System.Drawing.Bitmap(600,70);using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.Clear(System.Drawing.Color.Transparent);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;double min=values.Min(),span=Math.Max(.5,values.Max()-min);var points=values.Select((v,i)=>new System.Drawing.PointF(3+i*594f/(values.Count-1),(float)(64-(v-min)/span*58))).ToArray();using var pen=new System.Drawing.Pen(System.Drawing.Color.FromArgb(244,155,53),3);g.DrawLines(pen,points);}using var ms=new MemoryStream();bitmap.Save(ms,System.Drawing.Imaging.ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(ms.ToArray());
 }
 public static string Card(XElement? supplied=null,Dictionary<string,string>? options=null){
  string Opt(string k,string f)=>options!=null?(options.TryGetValue(k,out var v)?v:f):Option(k,f);
  bool enabled=Opt("winWidgetsEnabled","1")=="1",charts=Opt("widgetCharts","0")=="1";XElement? snapshot=supplied;if(options==null)try{snapshot=ReadOfficial();}catch(Exception ex){Log("Snapshot read failed: "+ex);}
  DateTime timestamp;bool fresh=DateTime.TryParse((string?)snapshot?.Attribute("time"),out timestamp)&&DateTime.UtcNow-timestamp.ToUniversalTime()<TimeSpan.FromSeconds(12);
  var body=new List<object>{new{type="TextBlock",text="iCUE 原版 · 温度",weight="Bolder",size="Medium"}};
  if(!enabled)body.Add(new{type="TextBlock",text="已暂停更新",isSubtle=true,wrap=true});
  else{int count=0;for(int i=0;i<3;i++){string key=Opt("widgetSensor"+i,new[]{"CPU 温度","GPU 温度","冷却液温度"}[i]);if(key=="none")continue;count++;var sensor=snapshot?.Elements("Sensor").FirstOrDefault(e=>(string?)e.Attribute("key")==key);string name=(string?)sensor?.Attribute("name")??key,unit=(string?)sensor?.Attribute("unit")??"",text="— 未提供";bool valid=fresh&&double.TryParse((string?)sensor?.Attribute("value"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out _);if(valid){double value=double.Parse((string)sensor!.Attribute("value")!,System.Globalization.CultureInfo.InvariantCulture);if(unit=="°C"&&Opt("fahrenheit","0")=="1"){value=value*1.8+32;unit="°F";}text=value.ToString("0.00")+" "+unit;}
  body.Add(new{type="TextBlock",text=name+"   "+text,wrap=true,spacing=i==0?"Small":"Medium"});if(charts&&valid){var url=Chart((string?)sensor?.Attribute("history"));if(url!=null)body.Add(new{type="Image",url,height="35px",size="Stretch",altText=name+"近期实际采样趋势（自适应纵轴）",spacing="Small"});else body.Add(new{type="TextBlock",text="正在积累曲线…",isSubtle=true,size="Small"});}}
  if(count==0)body.Add(new{type="TextBlock",text="暂无温度测点",wrap=true});if(!fresh)body.Add(new{type="TextBlock",text="正在连接原版 iCUE，设备就绪后自动更新",isSubtle=true,wrap=true});else body.Add(new{type="TextBlock",text=timestamp.ToLocalTime().ToString("HH:mm:ss")+" · 原版传感器实时数据",isSubtle=true,size="Small",wrap=true});}
  return JsonSerializer.Serialize(new{type="AdaptiveCard",version="1.5",body,selectAction=new{type="Action.Execute",verb="tap-next",tooltip="双击打开 iCUE 原版"},actions=new[]{new{type="Action.Execute",title="打开 iCUE 原版",verb="open-next"},new{type="Action.Execute",title="刷新",verb="refresh"}}});
 }
 public static void Test(){
  var snap=new XElement("Telemetry",new XAttribute("time",DateTime.UtcNow.ToString("o")),new XElement("Sensor",new XAttribute("key","test"),new XAttribute("name","测试温度"),new XAttribute("unit","°C"),new XAttribute("value","42.25"),new XAttribute("history","40,41,42.25")));
  var opts=new Dictionary<string,string>{{"widgetSensor0","test"},{"widgetSensor1","none"},{"widgetSensor2","none"},{"widgetCharts","1"}};var card=Card(snap,opts);using(var json=JsonDocument.Parse(card)){var items=json.RootElement.GetProperty("body").EnumerateArray().ToArray();if(items.Count(x=>x.GetProperty("type").GetString()=="Image")!=1||!card.Contains("42.25"))throw new Exception("Selection/chart test failed");var uri=items.First(x=>x.GetProperty("type").GetString()=="Image").GetProperty("url").GetString()!;using var ms=new MemoryStream(Convert.FromBase64String(uri.Split(',')[1]));using var image=System.Drawing.Image.FromStream(ms);if(image.Width!=600||image.Height!=70)throw new Exception("Chart PNG invalid");}
  opts["widgetCharts"]="0";if(Card(snap,opts).Contains("data:image"))throw new Exception("Hidden chart test failed");opts["widgetCharts"]="1";snap.SetAttributeValue("time",DateTime.UtcNow.AddMinutes(-1).ToString("o"));if(Card(snap,opts).Contains("data:image")||Card(snap,opts).Contains("42.25"))throw new Exception("Stale data test failed");opts["winWidgetsEnabled"]="1";opts["fahrenheit"]="0";
  string TextOf(string template){using var json=JsonDocument.Parse(template);return string.Join(" ",json.RootElement.GetProperty("body").EnumerateArray().Where(x=>x.TryGetProperty("text",out _)).Select(x=>x.GetProperty("text").GetString()));}
  var fresh=new XElement(snap);fresh.SetAttributeValue("time",DateTime.UtcNow.ToString("o"));
  int attempts=0;
  var first=FirstPresentation(()=>{attempts++;if(attempts==1)return null;var frame=new XElement(fresh);if(attempts==2)frame.Element("Sensor")!.SetAttributeValue("history","42.25");return frame;},()=>opts).GetAwaiter().GetResult();
  if(attempts!=3||TextOf(first).Contains("未提供")||TextOf(first).Contains("正在积累曲线")||!first.Contains("data:image"))throw new Exception("First presentation was not held for readings and chart");
  long began=Environment.TickCount64;
  var absent=FirstPresentation(()=>null,()=>opts,50).GetAwaiter().GetResult();
  if(Environment.TickCount64-began>1000||!TextOf(absent).Contains("连接原版"))throw new Exception("Missing-source wait is not bounded");
  opts["winWidgetsEnabled"]="0";began=Environment.TickCount64;
  var paused=FirstPresentation(()=>null,()=>opts).GetAwaiter().GetResult();
  if(Environment.TickCount64-began>100||!TextOf(paused).Contains("已暂停"))throw new Exception("Paused widgets should not wait for sensors");
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"widget-tests.txt"),"PASS: selections; actual PNG curves; stale suppression; first frame waits for readings + two samples; missing source has bounded grace; paused state immediate.");
 }

 static string DiagnosticCard(){var card=Card();try{File.WriteAllText(Path.Combine(DataRoot,"widget-last-card.json"),card);}catch{}return card;}
 static Dictionary<string,string> PresentationOptions(){
  var result=new Dictionary<string,string>{{"winWidgetsEnabled",Option("winWidgetsEnabled","1")},{"widgetCharts",Option("widgetCharts","0")},{"fahrenheit",Option("fahrenheit","0")}};
  string[] defaults={"CPU 温度","GPU 温度","冷却液温度"};
  for(int i=0;i<3;i++)result["widgetSensor"+i]=Option("widgetSensor"+i,defaults[i]);
  return result;
 }
 static bool PresentationReady(XElement? snapshot,Dictionary<string,string> options){
  if(options["winWidgetsEnabled"]!="1")return true;
  var selected=Enumerable.Range(0,3).Select(i=>options["widgetSensor"+i]).Where(k=>k!="none").ToArray();
  if(selected.Length==0)return true;
  if(snapshot==null||!DateTime.TryParse((string?)snapshot.Attribute("time"),out var time)||DateTime.UtcNow-time.ToUniversalTime()>TimeSpan.FromSeconds(12))return false;
  foreach(var key in selected){
   var sensor=snapshot.Elements("Sensor").FirstOrDefault(s=>(string?)s.Attribute("key")==key);
   if(!double.TryParse((string?)sensor?.Attribute("value"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value))return false;
   if(options["widgetCharts"]=="1"&&((string?)sensor?.Attribute("history")??"").Split(',').Count(x=>double.TryParse(x,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var v)&&double.IsFinite(v))<2)return false;
  }
  return true;
 }
 public static async Task<string> FirstPresentation(Func<XElement?> read,Func<Dictionary<string,string>> options,int graceMs=1800){
  long deadline=Environment.TickCount64+graceMs;
  while(true){
   var preferences=options();XElement? snapshot=null;
   try{snapshot=read();}catch{}
   if(PresentationReady(snapshot,preferences)||Environment.TickCount64>=deadline)return Card(snapshot,preferences);
   await Task.Delay(100).ConfigureAwait(false);
  }
 }
 readonly System.Collections.Concurrent.ConcurrentDictionary<string,byte> updatesInFlight=new();
 void Update(string id){
  if(!updatesInFlight.TryAdd(id,0))return;
  _=Task.Run(async()=>{
   try{
    // Keep the host's previous card until this first presentation is ready.
    // Never send the temporary empty bridge snapshot or an avoidable one-point chart.
    var card=await FirstPresentation(ReadOfficial,PresentationOptions).ConfigureAwait(false);
    if(!active.TryGetValue(id,out var enabled)||!enabled)return;
    WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(id){Template=card,Data="{}",CustomState="v2"});
    try{File.WriteAllText(Path.Combine(DataRoot,"widget-last-card.json"),card);}catch{}
    Log("Updated ready presentation "+id);
   }catch(Exception e){Log(e.ToString());}
   finally{updatesInFlight.TryRemove(id,out _);}
  });
 }
 public static void Log(string s){try{Directory.CreateDirectory(DataRoot);File.AppendAllText(Path.Combine(DataRoot,"winwidgets.log"),DateTime.Now.ToString("O")+" "+s+Environment.NewLine);}catch{}}
}
[ComImport,Guid("00000001-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IClassFactory{[PreserveSig]int CreateInstance(IntPtr outer,ref Guid iid,out IntPtr result);[PreserveSig]int LockServer(bool value);}
[ComVisible(true)]class Factory:IClassFactory{
 static Provider? singleton;
 public int CreateInstance(IntPtr outer,ref Guid iid,out IntPtr result){result=IntPtr.Zero;if(outer!=IntPtr.Zero)return unchecked((int)0x80040110);try{singleton??=new Provider();var p=MarshalInspectable<IWidgetProvider>.FromManaged(singleton);try{return Marshal.QueryInterface(p,ref iid,out result);}finally{Marshal.Release(p);}}catch(Exception e){Provider.Log(e.ToString());return Marshal.GetHRForException(e);}}
 public int LockServer(bool value)=>0;
}
static class Program{
 [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint mode);
 [DllImport("ole32.dll")]static extern int CoRegisterClassObject(in Guid id,[MarshalAs(UnmanagedType.IUnknown)]object factory,uint context,uint flags,out uint cookie);
 [DllImport("ole32.dll")]static extern int CoRevokeClassObject(uint cookie);
 [MTAThread]static void Main(string[] args){try{if(args.Contains("--probe")){for(int i=0;i<20;i++){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"live-card.json"),Provider.Card());Thread.Sleep(1000);}return;}if(args.Contains("--self-test")){Provider.Test();var card=Provider.Card();JsonDocument.Parse(card);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"card-test.json"),card);return;}CoInitializeEx(IntPtr.Zero,0);var factory=new Factory();var id=typeof(Provider).GUID;Marshal.ThrowExceptionForHR(CoRegisterClassObject(in id,factory,4,1,out uint cookie));Provider.Log("COM registered");new ManualResetEvent(false).WaitOne();CoRevokeClassObject(cookie);GC.KeepAlive(factory);}catch(Exception e){Provider.Log(e.ToString());}}
}




