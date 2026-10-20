using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Linq;
namespace CueStudio {
 public sealed class Reading {
  public string Name, Source, Unit; public double? Value; public DateTime Time; public double[] History=new double[0];
 }
 public static class Telemetry {
  static readonly object gate=new object(); static Dictionary<string,Reading> readings=new Dictionary<string,Reading>();
  static Timer timer,originalTimer; static int busy; static StateHandler callback; public static string Connection="尚未连接"; public static string Devices="";
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void StateHandler(IntPtr context,IntPtr state);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Ansi)] struct Device {public int Type;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Serial;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Model;public int Leds,Channels;}
  [DllImport("iCUESDK.x64_2019.dll",CallingConvention=CallingConvention.Cdecl)] static extern int CorsairConnect(StateHandler handler,IntPtr context);
  [DllImport("iCUESDK.x64_2019.dll",CallingConvention=CallingConvention.Cdecl)] static extern int CorsairGetDevices(ref int mask,int max,[Out] Device[] devices,out int count);
  public static void Start(){if(timer!=null)return;OriginalTelemetry.EnsureOriginalRunning();try{callback=delegate(IntPtr c,IntPtr s){int state=Marshal.ReadInt32(s);Connection=state==6?"iCUE SDK 已连接（共享模式）":"iCUE SDK："+new[]{"无效","已关闭","连接中","连接超时","连接被拒绝","连接断开","已连接"}[Math.Max(0,Math.Min(6,state))];};int result=CorsairConnect(callback,IntPtr.Zero);if(result!=0)Connection="iCUE SDK 连接错误："+result;}catch(Exception e){Connection="iCUE SDK 不可用："+e.GetType().Name;}originalTimer=new Timer(delegate{OriginalTelemetry.Poll();},null,0,100);timer=new Timer(Poll,null,0,3000);}
  static void Poll(object unused){if(Interlocked.Exchange(ref busy,1)!=0)return;try{
   try{int mask=-1,count;var devices=new Device[64];int result=CorsairGetDevices(ref mask,64,devices,out count);Devices=result==0?String.Join("、",devices.Take(count).Select(d=>d.Model).Distinct()):"设备枚举不可用（"+result+"）";}catch{Devices="设备枚举不可用";}
   string output="";using(var p=new Process()){p.StartInfo=new ProcessStartInfo("nvidia-smi.exe","--query-gpu=name,uuid,temperature.gpu,temperature.memory,utilization.gpu,utilization.memory,memory.used,memory.total,fan.speed --format=csv,noheader,nounits"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};try{p.Start();var task=p.StandardOutput.ReadToEndAsync();if(p.WaitForExit(2000)&&p.ExitCode==0)output=task.Result;else if(!p.HasExited)p.Kill();}catch{}}
   var row=output.Split('\n').Select(l=>l.Split(',').Select(v=>v.Trim()).ToArray()).FirstOrDefault(a=>a.Length==9&&a[0].IndexOf("RTX 3090",StringComparison.OrdinalIgnoreCase)>=0);
   if(row!=null){Set("gpu.core","GPU 核心温度","°C",row[2],"NVIDIA NVML / temperature.gpu / "+row[1]);Set("gpu.memory","显存结温","°C",row[3],"NVIDIA NVML / temperature.memory / "+row[1]);Set("gpu.load","GPU 负载","%",row[4],"NVIDIA NVML / utilization.gpu");Set("gpu.memload","显存控制器负载","%",row[5],"NVIDIA NVML / utilization.memory");double used,total;if(Double.TryParse(row[6],NumberStyles.Float,CultureInfo.InvariantCulture,out used)&&Double.TryParse(row[7],NumberStyles.Float,CultureInfo.InvariantCulture,out total)&&total>0)Set("gpu.fb","显存容量占用","%",(used/total*100).ToString(CultureInfo.InvariantCulture),"NVIDIA NVML / memory.used ÷ memory.total");Set("gpu.fan","GPU 风扇占空比","%",row[8],"NVIDIA NVML / fan.speed；不是 RPM，未区分各风扇");}
   Snapshot();
  }catch{}finally{Interlocked.Exchange(ref busy,0);}}
  static void Set(string key,string name,string unit,string raw,string source){double n;double? value=Double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!Double.IsNaN(n)&&!Double.IsInfinity(n)&&n>=0?(double?)n:null;lock(gate){Reading old;readings.TryGetValue(key,out old);var history=value.HasValue?(old==null||!old.Value.HasValue||DateTime.UtcNow-old.Time>TimeSpan.FromSeconds(10)?new double[0]:old.History).Concat(new[]{value.Value}).Reverse().Take(60).Reverse().ToArray():new double[0];readings[key]=new Reading{Name=name,Source=source,Unit=unit,Value=value,Time=DateTime.UtcNow,History=history};}}
  public static Reading Get(string key,string name,string unit,string source){lock(gate){Reading r;if(readings.TryGetValue(key,out r)){if(DateTime.UtcNow-r.Time<TimeSpan.FromSeconds(10))return r;return new Reading{Name=r.Name,Unit=r.Unit,Source=r.Source+"；数据已过期"};}return new Reading{Name=name,Unit=unit,Source=source};}}
  public static Reading Resolve(string device,string name,string unit){var original=OriginalTelemetry.Get(device,name);if(original!=null)return original;string d=device.ToUpperInvariant();if(d.Contains("NVIDIA")){string key=name=="温度 #1"||name=="GPU 温度"?"gpu.core":name=="温度 #2"?"gpu.memory":name=="负载"?"gpu.load":name=="内存加载"?"gpu.memload":name=="帧缓存"?"gpu.fb":name=="GPU 风扇占空比"?"gpu.fan":"";string label=name=="温度 #1"||name=="GPU 温度"?"GPU 核心温度":name=="温度 #2"?"显存结温":name=="温度 #3"?"GPU 热点温度":name;return Get(key,label,unit,"NVIDIA 接口未提供该测点；不根据原版编号推断来源");}
   string title=name=="包"?"CPU 封装温度":name.StartsWith("温度 #")?"未知测点 "+name.Substring(3):name=="温度"&&d.Contains("DOMINATOR")?"DIMM 温度":name;return new Reading{Name=title,Unit=unit,Source="未接入："+device+" / 原名称："+name+"。iCUE 公共 SDK 不提供此遥测；不能确认是核心、外部或其他测点。"};}
  public static Reading Named(string n){if(n=="CPU 温度")return Resolve("AMD Ryzen 9 9900X","包","°C");if(n=="CPU 负载")return Resolve("AMD Ryzen 9 9900X","负载","%");if(n=="GPU 温度")return Resolve("NVIDIA","温度 #1","°C");if(n=="GPU 风扇")return Resolve("NVIDIA","风扇 #1","RPM");return Resolve("H100i ELITE LCD",n=="水泵转速"?"泵":n,n=="水泵转速"?"RPM":"°C");}

  static XElement WidgetSensor(string key,string name,Reading r){return new XElement("Sensor",new XAttribute("key",key),new XAttribute("name",name),new XAttribute("unit",r.Unit??""),new XAttribute("source",r.Source??""),new XAttribute("value",r.Value.HasValue?r.Value.Value.ToString(CultureInfo.InvariantCulture):""),new XAttribute("history",r.Value.HasValue?String.Join(",",r.History.Select(v=>v.ToString(CultureInfo.InvariantCulture))):""));}
  static void Snapshot(){var root=new XElement("Telemetry",new XAttribute("time",DateTime.UtcNow.ToString("o")),new XAttribute("connection",Connection),new XAttribute("devices",Devices));foreach(string n in new[]{"CPU 温度","CPU 负载","GPU 温度","GPU 风扇","冷却液温度","水泵转速"}){var r=Named(n);root.Add(WidgetSensor(n,r.Name,r));}foreach(var pair in OriginalTelemetry.Temperatures()){string dev=pair.Key.Substring(9).Split('|')[0];root.Add(WidgetSensor(pair.Key,dev+" · "+pair.Value.Name,pair.Value));}string dir=Path.GetDirectoryName(MainWindow.Store);Directory.CreateDirectory(dir);string path=Path.Combine(dir,"telemetry.xml"),tmp=path+".tmp";root.Save(tmp);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
 }
}
