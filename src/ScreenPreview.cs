using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace CueStudio {public partial class MainWindow {
 Process screenRenderer;DateTime screenLaunchTime=DateTime.MinValue;DateTime screenInputTime=DateTime.MinValue,screenFrameTime=DateTime.MinValue;
 string ScreenJob {get{return Path.Combine(Path.GetDirectoryName(Store),"screen-preview");}}
 bool OriginalScreenPreview(){
  string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ScreenRenderer","CueScreenRenderer.exe");
  if(Store.EndsWith("test-settings.xml")||!File.Exists(exe))return false;
  Directory.CreateDirectory(ScreenJob);
  var image=new Image{Stretch=Stretch.Fill,Clip=new EllipseGeometry(new Point(75,75),75,75)};Put(surface,image,798,237,150,150);
  var pending=Label(surface,"正在加载原版动画…",801,294,144,40,12,"#FFFFFF");pending.TextAlignment=TextAlignment.Center;
  Action<double> update=t=>{try{
   if((DateTime.UtcNow-screenInputTime).TotalMilliseconds>250){
    var a=LcdReading(DS.Sensor1);var b=LcdReading(DS.Sensor2);var ci=System.Globalization.CultureInfo.InvariantCulture;
    var x=new XElement("Screen",new XAttribute("index",Math.Max(0,Array.IndexOf(screenTypes,DS.ScreenType))),new XAttribute("fps",DS.Fps),new XAttribute("value0",a.Value??0),new XAttribute("value1",b.Value??0),new XAttribute("text0",a.Value.HasValue?a.Value.Value.ToString("0",ci):"—"),new XAttribute("text1",b.Value.HasValue?b.Value.Value.ToString("0",ci):"—"),new XAttribute("sensor0",DS.Sensor1),new XAttribute("sensor1",DS.Sensor2),new XAttribute("min0",DS.MinTemp),new XAttribute("max0",DS.MaxTemp),new XAttribute("min1",DS.MinTemp2),new XAttribute("max1",DS.MaxTemp2),new XAttribute("color0",DS.ScreenColor1),new XAttribute("color1",DS.ScreenColor2),new XAttribute("background",DS.ScreenBackground),new XAttribute("label",DS.ScreenLabel));
    string input=Path.Combine(ScreenJob,"input.xml"),temp=input+".tmp";x.Save(temp);if(File.Exists(input))File.Replace(temp,input,null);else File.Move(temp,input);screenInputTime=DateTime.UtcNow;
   }
   if((screenRenderer==null||screenRenderer.HasExited)&&(DateTime.UtcNow-screenLaunchTime).TotalSeconds>10){screenLaunchTime=DateTime.UtcNow;screenRenderer=Process.Start(new ProcessStartInfo(exe,"\""+ScreenJob+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});}
   string frame=Path.Combine(ScreenJob,"frame.png");var stamp=File.GetLastWriteTimeUtc(frame);
   if(File.Exists(frame)&&stamp!=screenFrameTime&&DateTime.UtcNow-stamp<TimeSpan.FromSeconds(2)){
    using(var stream=new FileStream(frame,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.StreamSource=stream;bmp.EndInit();bmp.Freeze();image.Source=bmp;}screenFrameTime=stamp;pending.Visibility=Visibility.Collapsed;
   }
  }catch(Exception){pending.Text="原版动画暂不可用";pending.Visibility=Visibility.Visible;}};
  update(0);animations.Add(update);return true;
 }
}}
