using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace CueStudio {public partial class MainWindow {
 void StorageGroups(Canvas canvas){
  try{
   string file=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Store),"original-sensors.xml");
   var root=System.Xml.Linq.XElement.Load(file);double y=458;
   var disks=root.Elements("Sensor").Where(e=>((string)e.Attribute("Type"))=="temperature"&&(((string)e.Attribute("DeviceName")??"").ToUpperInvariant().Contains("SSD")||((string)e.Attribute("DeviceName")??"").ToUpperInvariant().Contains("NVME"))).GroupBy(e=>(string)e.Attribute("DeviceName")).OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase);
   foreach(var disk in disks){var sensors=disk.OrderBy(e=>(string)e.Attribute("Name"),StringComparer.OrdinalIgnoreCase).ToArray();SensorGroup(canvas,16,y,disk.Key.ToUpperInvariant(),"硬盘驱动器 (HDD) / 固态硬盘 (SSD)",sensors.Select(e=>(string)e.Attribute("Name")).ToArray(),new double[sensors.Length],sensors.Select(e=>(string)e.Attribute("Units")).ToArray());y+=80+Math.Ceiling(sensors.Length/2d)*96;}
  }catch(System.IO.IOException){}catch(System.Xml.XmlException){}
 }
 bool sensorDragCompleted;
 Action cardDragCancel;
 double CardOption(string key,double fallback){double n;return double.TryParse(Option(key),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?n:fallback;}
 void SaveCardPosition(Border card){string key=(string)card.Tag;SetOption("layoutX:"+key,Canvas.GetLeft(card).ToString(System.Globalization.CultureInfo.InvariantCulture));SetOption("layoutY:"+key,Canvas.GetTop(card).ToString(System.Globalization.CultureInfo.InvariantCulture));}
 bool CardButtonHit(DependencyObject node,Border card){while(node!=null&&node!=card){if(node is System.Windows.Controls.Primitives.ButtonBase||node is System.Windows.Controls.Primitives.Selector||node is TextBox)return true;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}return false;}
 void EnableCardDrag(Border card,Canvas parent,string key,bool group){
  card.Tag=key;Point start=new Point();bool armed=false,dragging=false;int oldZ=0;Transform originalTransform=null;
  Action cancel=()=>{if(!armed)return;armed=false;dragging=false;card.RenderTransform=originalTransform;Panel.SetZIndex(card,oldZ);card.Opacity=1;cardDragCancel=null;if(Mouse.Captured==card)Mouse.Capture(null);};
  card.PreviewMouseLeftButtonDown+=(s,e)=>{if(CardButtonHit(e.OriginalSource as DependencyObject,card)||group&&e.GetPosition(card).Y>=60)return;sensorDragCompleted=false;start=e.GetPosition(parent);oldZ=Panel.GetZIndex(card);originalTransform=card.RenderTransform;armed=true;cardDragCancel=cancel;Mouse.Capture(card,CaptureMode.SubTree);};
  card.MouseMove+=(s,e)=>{if(!armed)return;if(e.LeftButton!=MouseButtonState.Pressed){cancel();return;}var pt=e.GetPosition(parent);double dx=pt.X-start.X,dy=pt.Y-start.Y;if(!dragging&&Math.Abs(dx)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(dy)<SystemParameters.MinimumVerticalDragDistance)return;dragging=true;sensorDragCompleted=true;Panel.SetZIndex(card,1000);card.Opacity=.9;card.RenderTransform=new TranslateTransform(dx,dy);e.Handled=true;};
  card.PreviewMouseLeftButtonUp+=(s,e)=>{if(!armed)return;bool moved=dragging;var point=e.GetPosition(parent);cancel();if(!moved)return;e.Handled=true;
   if(group){int col=Math.Max(0,Math.Min(2,(int)Math.Floor((point.X-16)/416)));var siblings=parent.Children.OfType<Border>().Where(b=>b!=card&&b.Tag is string&&Math.Max(0,Math.Min(2,(int)Math.Round((Canvas.GetLeft(b)-16)/416)))==col).OrderBy(b=>Canvas.GetTop(b)).ToList();int index=siblings.FindIndex(b=>point.Y<Canvas.GetTop(b)+b.Height/2);if(index<0)index=siblings.Count;siblings.Insert(index,card);double y=10;foreach(var item in siblings){Canvas.SetLeft(item,16+col*416);Canvas.SetTop(item,y);y+=item.Height+8;}ReflowGroups(parent);
   }else{var target=parent.Children.OfType<Border>().FirstOrDefault(b=>b!=card&&b.Tag is string&&new Rect(Canvas.GetLeft(b),Canvas.GetTop(b),b.Width,b.Height).Contains(point));if(target==null)return;double x=Canvas.GetLeft(card),y=Canvas.GetTop(card);Canvas.SetLeft(card,Canvas.GetLeft(target));Canvas.SetTop(card,Canvas.GetTop(target));Canvas.SetLeft(target,x);Canvas.SetTop(target,y);}
   foreach(var item in parent.Children.OfType<Border>().Where(b=>b.Tag is string))SaveCardPosition(item);
  };
  card.LostMouseCapture+=(s,e)=>{if(armed&&Mouse.Captured!=card)cancel();};
  Canvas.SetLeft(card,CardOption("layoutX:"+key,Canvas.GetLeft(card)));Canvas.SetTop(card,CardOption("layoutY:"+key,Canvas.GetTop(card)));
 }
 void RestoreSensorOrder(Canvas parent){var cards=parent.Children.OfType<Border>().Where(b=>b.Tag is string).OrderBy(b=>Canvas.GetTop(b)).ThenBy(b=>Canvas.GetLeft(b)).ToList();for(int i=0;i<cards.Count;i++){Canvas.SetLeft(cards[i],16+i%2*192);Canvas.SetTop(cards[i],64+i/2*96);}}
 void ReflowGroups(Canvas parent){double bottom=0;for(int col=0;col<3;col++){double x=16+col*416,y=10;var cards=parent.Children.OfType<Border>().Where(b=>b.Tag is string&&Math.Max(0,Math.Min(2,(int)Math.Round((Canvas.GetLeft(b)-16)/416)))==col).OrderBy(b=>Canvas.GetTop(b)).ToList();foreach(var card in cards){Canvas.SetLeft(card,x);Canvas.SetTop(card,y);y+=card.Height+8;}bottom=Math.Max(bottom,y);}parent.Height=Math.Max(790,bottom);}
}}

