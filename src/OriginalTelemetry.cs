using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
namespace CueStudio {
 public static partial class OriginalTelemetry {
  static int polling;static Process bridge;static DateTime loaded;static readonly object gate=new object();static Dictionary<string,Reading> data=new Dictionary<string,Reading>();
  static string FilePath {get{return Path.Combine(Path.GetDirectoryName(MainWindow.Store),"original-sensors.xml");}}
  public static string Status {get{lock(gate){return data.Values.Any(r=>r.Value.HasValue&&DateTime.UtcNow-r.Time<TimeSpan.FromSeconds(10))?"":"原版传感器未连接 · 请保持原版 iCUE 后台运行";}}}
  public static void EnsureOriginalRunning(){
   try{if(Process.GetProcessesByName("iCUE").Length>0)return;
    string launcher=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Corsair","Corsair iCUE5 Software","iCUE Launcher.exe");
    if(File.Exists(launcher))Process.Start(new ProcessStartInfo(launcher,"--autorun"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(launcher)});
   }catch(Exception ex){try{File.AppendAllText(Path.Combine(Path.GetDirectoryName(MainWindow.Store),"telemetry-startup.log"),DateTime.Now.ToString("o")+" "+ex.Message+Environment.NewLine);}catch{}}
  }
  public static void Start(){try{if(bridge!=null&&!bridge.HasExited)return;string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sensor-bridge","CueSensorBridge.exe");if(File.Exists(exe))bridge=Process.Start(new ProcessStartInfo(exe,"\""+FilePath+"\" "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true});}catch{}}
  static string Device(string d){d=d.ToUpperInvariant();return d.Contains("NVIDIA")?"NVIDIA":d.Contains("RYZEN")?"CPU":d.Contains("RADE")?"AMD GPU":d.Contains("MICRO-STAR")?"MAINBOARD":d.Contains("DOMINATOR")?"DDR5":d.Contains("H100")?"H100":d;}
  static string Name(string n){if(n.StartsWith("RAS")&&n.Contains("CAS"))return "RAS到CAS延迟时间";return n.Replace("（","(").Replace("）",")").Replace(" ","").Replace("#","").Replace("CAS延迟时间(CL)","CAS延迟时间(CL)").Replace("RAS到CAS延迟时间","RAS到CAS延迟时间(tRCD)");}
  public static void Poll(){if(System.Threading.Interlocked.Exchange(ref polling,1)!=0)return;Start();try{DateTime write=File.GetLastWriteTimeUtc(FilePath);if(write==loaded)return;var root=XElement.Load(FilePath);double epoch=Double.Parse((string)root.Attribute("time"),CultureInfo.InvariantCulture);DateTime time=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(epoch);if(DateTime.UtcNow-time>TimeSpan.FromSeconds(10))return;var next=new Dictionary<string,Reading>();lock(gate){foreach(var e in root.Elements("Sensor")){string dev=(string)e.Attribute("DeviceName"),raw=(string)e.Attribute("Name"),kind=(string)e.Attribute("Kind"),type=(string)e.Attribute("Type"),key=Device(dev)+"|"+Name(raw);DateTime sampled=e.Attribute("sampled")==null?time:new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Double.Parse((string)e.Attribute("sampled"),CultureInfo.InvariantCulture));double v;bool valid=Double.TryParse((string)e.Attribute("value"),NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&v>=0&&!Double.IsNaN(v)&&!Double.IsInfinity(v);Reading old;data.TryGetValue(key,out old);string label=kind=="package"&&type=="temperature"?"CPU 封装温度":raw=="温度"&&Device(dev)=="DDR5"?"DIMM 温度":raw.StartsWith("温度 #")?raw+"（原版测点）":raw;next[key]=new Reading{Name=label,Unit=(string)e.Attribute("Units"),Value=valid?(double?)v:null,Time=sampled,Source="原版 iCUE SensorProvider / "+dev+" / "+raw+" / type="+type+" / kind="+kind+" / id="+(string)e.Attribute("id")+(raw.StartsWith("温度 #")?"；该接口没有公开测点物理名称，暂不猜测核心、显存或热点。":""),History=old!=null&&old.Time==sampled?old.History:valid?(old!=null&&old.Value.HasValue&&time-old.Time<TimeSpan.FromSeconds(10)?old.History:new double[0]).Concat(new[]{v}).Reverse().Take(60).Reverse().ToArray():new double[0]};}data=next;loaded=write;}}catch{}finally{System.Threading.Interlocked.Exchange(ref polling,0);}}
  public static Dictionary<string,Reading> Temperatures(){lock(gate){return data.Where(p=>p.Value.Unit=="°C").ToDictionary(p=>"original:"+p.Key,p=>DateTime.UtcNow-p.Value.Time<TimeSpan.FromSeconds(10)?p.Value:new Reading{Name=p.Value.Name,Unit=p.Value.Unit,Source=p.Value.Source+"；数据过期"});}}
  public static Reading Get(string dev,string name){string key=Device(dev)+"|"+Name(name);lock(gate){Reading r;if(!data.TryGetValue(key,out r))return null;if(DateTime.UtcNow-r.Time>TimeSpan.FromSeconds(10))return new Reading{Name=r.Name,Unit=r.Unit,Source=r.Source+"；原版通道断开或数据过期"};return r;}}
 }
}
