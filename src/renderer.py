import os,sys,time,re,json,datetime,xml.etree.ElementTree as E
from pathlib import Path
os.environ['QT_SCALE_FACTOR']='1';os.environ['QT_ENABLE_HIGHDPI_SCALING']='0'
if not getattr(sys,'frozen',False):sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'telemetry/qt'))
from PySide6.QtCore import QUrl,QTimer,Qt,QPointF,qInstallMessageHandler
from PySide6.QtGui import QGuiApplication,QColor,QFontDatabase
from PySide6.QtQml import QQmlPropertyMap
from PySide6.QtQuick import QQuickView
base=Path(sys.executable).parent if getattr(sys,'frozen',False) else Path(__file__).parent
resources=base/'screen-runtime' if getattr(sys,'frozen',False) else base
job=Path(sys.argv[1]);job.mkdir(parents=True,exist_ok=True)
log=open(job/'renderer.log','w',encoding='utf-8');qInstallMessageHandler(lambda t,c,m:(log.write(m+'\n'),log.flush()))
app=QGuiApplication([])
for f in (resources/'fonts').glob('*'):QFontDatabase.addApplicationFont(str(f))
params=QQmlPropertyMap();clock=QQmlPropertyMap();clock.insert('tick',0)
view=QQuickView();view.setFlags(Qt.Tool|Qt.FramelessWindowHint|Qt.WindowDoesNotAcceptFocus);view.setSource(QUrl())
view.rootContext().setContextProperty('params',params);view.rootContext().setContextProperty('clock',clock)
view.setSource(QUrl.fromLocalFile(str(resources/'qml/Main.qml')));view.setPosition(-10000,-10000);view.show()
started=time.monotonic();last_input=None;last_effect='';last_read=0;last_frame=0;fps=30
names=['ConcentricFill','SingleBar','FadeFill','Monitor','Image','ApertureFill','DynamicBar','PatternFill','DualBar','Turbo','Blank','ConstructorTwoSensors']
def color(s,default='#FFFFFF'):
 if not s:return default
 if s.startswith('rgb('):
  parts=[float(x) for x in s[4:-1].split()];return '#'+''.join('%02x'%round(max(0,min(1,v))*255) for v in parts[:3])
 return s
def sensor(sid):
 try:
  readings=E.parse(job.parent/'original-sensors.xml').getroot();found=None
  if '430a201134895aaab9f09f061091005f' in sid:
   found=next((a for a in readings if a.get('DeviceName')=='H100i ELITE LCD' and a.get('Type')=='temperature'),None)
  elif 'TemperaturePackage' in sid:
   found=next((a for a in readings if a.get('Kind')=='package' and a.get('Type')=='temperature'),None)
  if found is not None and time.time()-float(found.get('sampled','0'))<10:return found
 except:pass
 return None
def tick():
 global last_input,last_effect,last_read,last_frame,fps
 now=time.monotonic()
 if now-last_read>.25:
  last_read=now
  try:
   p=job/'input.xml'
   if time.time()-p.stat().st_mtime>15:app.quit();return
   x=E.parse(p).getroot();idx=int(x.get('index','8'));effect=names[idx];fps=max(1,min(30,int(float(x.get('fps','30')))))
   vals={k:v for k,v in x.attrib.items()};v0=float(vals.get('value0','0'));v1=float(vals.get('value1','0'))
   data={'sensorValue0':vals.get('text0','—'),'sensorValue1':vals.get('text1','—'),'sensorUnits0':'°C','sensorUnits1':'°C','sensorName0':vals.get('sensor0',''),'sensorName1':vals.get('sensor1',''),'normalizedValue0':max(0,min(1,(v0-float(vals['min0']))/max(1,float(vals['max0'])-float(vals['min0'])))),'normalizedValue1':max(0,min(1,(v1-float(vals['min1']))/max(1,float(vals['max1'])-float(vals['min1'])))),'backgroundColor':vals['background'],'mainColor0':vals['color0'],'mainColor1':vals['color1'],'labelColor':vals['label'],'dataColor':vals['color0'],'mainColor':vals['color0'],'color0':vals['color0'],'color1':'#785AFF','color2':vals['background'],'normalizedValue2':0.,'nameColor0':vals['label'],'nameColor1':vals['label'],'valueColor0':vals['color0'],'valueColor1':vals['color1'],'scaling':1.,'position':QPointF(0,0),'rotationAngle':0.,'blurActive':False,'backgroundSource':QUrl()}
   # Read original effect colours/settings; never write the original configuration.
   files=list((Path(os.environ['APPDATA'])/'Corsair/CUE5/profiles').glob('*.cueprofiledata'))
   if len(files)==1:
    profile=E.parse(files[0]).getroot();node=next((n for n in profile.iter() if n.findtext('key')=='LightingNode'),None)
    if node is not None:
     active=next((n for n in node.iter('effect') if (n.findtext('polymorphic_name') or '').endswith('ScreenEffectSettings')),None)
     if active is not None:
      d=active.find('ptr_wrapper/data');kind=active.findtext('polymorphic_name').replace('ScreenEffectSettings','')
      if kind==effect or kind=='Constructor':
       fps=max(1,min(30,int(float(d.findtext('.//fps') or fps))))
       if kind=='DualBar':
        for i in range(2):
         suffix=str(i);source=d.findtext('sensorId'+str(i+1)) or '';r=sensor(source);lo=float(d.findtext('minValue'+str(i+1)) or 20);hi=float(d.findtext('maxValue'+str(i+1)) or 60)
         data['mainColor'+suffix]=color(d.findtext('mainColors/value'+suffix),data['mainColor'+suffix])
         if r is not None:
          value=float(r.get('value'));data['sensorValue'+suffix]=str(round(value));data['normalizedValue'+suffix]=max(0,min(1,(value-lo)/max(1,hi-lo)))
       for k in ['backgroundColor','labelColor','dataColor','mainColor']:data[k]=color(d.findtext(k),data.get(k,'#FFFFFF'))
       for i in range(3):
        c=d.findtext('lowColors/value'+str(i)) or d.findtext('colors/value'+str(i))
        if c:data['color'+str(i)]=color(c)
       if kind!='DualBar':
        sid=d.findtext('sensorId') or '';reading=None
        try:
         sensors=E.parse(job.parent/'original-sensors.xml').getroot()
         if '430a201134895aaab9f09f061091005f' in sid:
          reading=next((a for a in sensors if a.get('DeviceName')=='H100i ELITE LCD' and a.get('Type')=='temperature'),None)
         elif 'TemperaturePackage' in sid:
          reading=next((a for a in sensors if a.get('Kind')=='package' and a.get('Type')=='temperature'),None)
        except:pass
        if reading is not None and time.time()-float(reading.get('sampled','0'))<10:
         rv=float(reading.get('value'));lo=float(d.findtext('minValue') or 20);hi=float(d.findtext('maxValue') or 60)
         data.update(sensorValue0=str(round(rv)),sensorName0=reading.get('Name',''),sensorUnits0=reading.get('Units','°C'),normalizedValue0=max(0,min(1,(rv-lo)/max(1,hi-lo))))
        else:data.update(sensorValue0='—',sensorName0=d.findtext('sensorName') or '',normalizedValue0=0)
        for i in range(3):
         low=d.findtext('lowColors/value'+str(i));high=d.findtext('highColors/value'+str(i))
         if low and high:
          a=QColor(color(low));b=QColor(color(high));r=data['normalizedValue0'];data['color'+str(i)]=QColor.fromRgbF(a.redF()+(b.redF()-a.redF())*r,a.greenF()+(b.greenF()-a.greenF())*r,a.blueF()+(b.blueF()-a.blueF())*r).name()
       if kind=='Constructor':
        entries=list(d.find('sensorsData')) if d.find('sensorsData') is not None else []
        effect={1:'ConstructorOneSensor',2:'ConstructorTwoSensors',3:'ConstructorThreeSensors'}.get(len(entries),'ConstructorOneSensor')
        for i,entry in enumerate(entries[:3]):
         reading=sensor(entry.findtext('sensorId') or '');suffix=str(i)
         data['sensorValue'+suffix]=str(round(float(reading.get('value')))) if reading is not None else '—'
         data['sensorName'+suffix]=entry.findtext('name') or ''
         data['sensorUnits'+suffix]=reading.get('Units','') if reading is not None else ''
         data['nameColor'+suffix]=color(entry.findtext('nameColor'))
         data['valueColor'+suffix]=color(entry.findtext('valueColor'))
         data['labelOrientation'+suffix]=int(entry.findtext('orientation') or 0)
        if int(d.findtext('dataType') or 0)==3 and entries:
         entry=entries[0];dt=entry.find('dateTimeSettings');nowTime=datetime.datetime.now();twelve=dt is not None and dt.findtext('timeFormat')=='0'
         data.update(nameText=entry.findtext('name') or '',time=nowTime.strftime('%I:%M' if twelve else '%H:%M').lstrip('0'),timeLabel=nowTime.strftime('%p') if twelve and dt.findtext('displayAMPM')=='true' else '',date=nowTime.strftime('%Y-%m-%d'))
         effect='ConstructorTime'
       if kind in ['Image','Constructor']:
        imageData=d if kind=='Image' else d.find('base')
        path=imageData.findtext('backgroundSource') or ''
        if path.startswith('qrc:/'):url=QUrl.fromLocalFile(str(resources/'resources'/path[5:].lstrip('/')))
        elif path.startswith('file:'):url=QUrl(path)
        else:url=QUrl.fromLocalFile(path) if path else QUrl()
        data.update(backgroundSource=url,scaling=float(imageData.findtext('scaling') or 1),position=QPointF(float(imageData.findtext('position/x') or 0),float(imageData.findtext('position/y') or 0)),rotationAngle=float(imageData.findtext('rotationAngle') or 0),blurActive=imageData.findtext('blurActive')=='true')
   for k,v in data.items():params.insert(k,QColor(v) if 'color' in k.lower() else v)
   if effect!=last_effect:
    view.rootObject().loadEffect(QUrl.fromLocalFile(str(resources/'qml'/(effect+'Widget.qml'))));last_effect=effect;started_effect=time.monotonic()
  except Exception as ex:
   log.write(str(ex)+'\n');log.flush()
 clock.insert('tick',int((now-started)*1000))
 if now-last_frame>=1/fps:
  last_frame=now
  view.engine().incubationController().incubateFor(3)
  frame=view.grabWindow()
  if not frame.isNull():
   frame.save(str(job/'frame-next.png'));os.replace(job/'frame-next.png',job/'frame.png')
timer=QTimer();timer.timeout.connect(tick);timer.start(16);app.exec()
