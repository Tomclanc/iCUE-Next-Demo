import sys,os,time,ctypes,xml.etree.ElementTree as ET
from pathlib import Path
from PySide6.QtCore import QCoreApplication,QUrl,QMetaObject
from PySide6.QtRemoteObjects import QRemoteObjectNode
app=QCoreApplication([]);output=Path(sys.argv[1]);parent=int(sys.argv[2])
kernel=ctypes.windll.kernel32;kernel.OpenProcess.restype=ctypes.c_void_p
kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong]
handle=kernel.OpenProcess(0x100000,False,parent)
if not handle:sys.exit(1)
node=QRemoteObjectNode();node.connectToNode(QUrl('local:Corsair_WB_Plugin_Sensors_e180fbb8'))
r=node.acquireDynamic('SensorProvider');metadata={};values={};last_meta=0;last_poll=0;last_flush=0;dirty=False;subscribed=False;events=0

def call(name,args=[]):
    result=r.sendWithReply(QMetaObject.Call.InvokeMetaMethod,methods[name],args)
    if not result.waitForFinished(1000):raise RuntimeError('timeout')
    return result.returnValue()
def changed(sid,value):
    global dirty,events
    values[sid]=(str(value),time.time());dirty=True;events+=1
def invalidate(*args):
    global last_meta
    last_meta=0
while kernel.WaitForSingleObject(handle,0)==258:
    try:
        app.processEvents()
        if not r.isInitialized():
            if not r.waitForSource(1000):raise RuntimeError('disconnected')
        m=r.metaObject();methods={bytes(m.method(i).name()).decode():i for i in range(m.methodOffset(),m.methodCount())}
        if not subscribed:
            r.sensorValueChanged.connect(changed)
            r.sensorAdded.connect(invalidate);r.sensorRemoved.connect(invalidate);r.sensorDataChanged.connect(invalidate)
            subscribed=True
        now=time.monotonic()
        if now-last_meta>30:
            new={}
            for sid in call('getAllSensorIds'):
                meta={k:str(call('getSensor'+k,[sid])) for k in ['Name','DeviceName','Type','Kind','Units']}
                if meta['DeviceName']:new[sid]=meta
            metadata=new;last_meta=now
        if now-last_poll>=1:
            for sid in metadata:
                if call('sensorIsConnected',[sid]):values[sid]=(str(call('getSensorValue',[sid])),time.time())
                else:values.pop(sid,None)
            last_poll=now;dirty=True
        if dirty and time.monotonic()-last_flush>=.1:
            root=ET.Element('OriginalSensors',time=str(time.time()),events=str(events))
            for sid,meta in metadata.items():
                if sid in values:
                    value,stamp=values[sid]
                    ET.SubElement(root,'Sensor',id=sid,value=value,sampled=str(stamp),**meta)
            output.parent.mkdir(parents=True,exist_ok=True)
            tmp=output.with_suffix('.tmp');ET.ElementTree(root).write(tmp,encoding='utf-8',xml_declaration=True);os.replace(tmp,output)
            dirty=False;last_flush=time.monotonic()
    except Exception:
        last_meta=0
        time.sleep(.2)
    time.sleep(.02)
