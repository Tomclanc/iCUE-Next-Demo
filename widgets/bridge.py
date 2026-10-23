from pathlib import Path
import argparse,ctypes,os,sys,time,xml.etree.ElementTree as ET
from PySide6.QtCore import QCoreApplication,QUrl,QMetaObject
from PySide6.QtRemoteObjects import QRemoteObjectNode


def run():
    parser=argparse.ArgumentParser()
    parser.add_argument('output',type=Path)
    parser.add_argument('parent',type=int)
    parser.add_argument('--endpoint',default='local:Corsair_WB_Plugin_Sensors_e180fbb8')
    args=parser.parse_args()
    app=QCoreApplication([])
    kernel=ctypes.windll.kernel32
    kernel.OpenProcess.restype=ctypes.c_void_p
    kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong]
    kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,args.parent)
    if not handle:return 1
    output=args.output;output.parent.mkdir(parents=True,exist_ok=True)
    metadata={};values={};methods={}
    node=None;replica=None
    last_metadata=0;last_poll=0;last_flush=0;last_valid=False;subscribed=False
    failures=0;events=0;dirty=True;state='waiting';last_state=None

    def value_changed(sid,value):
        nonlocal dirty,events
        values[str(sid)]=(str(value),time.time());dirty=True;events+=1

    def invalidate(*unused):
        nonlocal last_metadata
        last_metadata=0

    def reset():
        nonlocal node,replica,methods,last_metadata,last_poll,last_valid,dirty,subscribed
        if replica is not None:replica.deleteLater()
        if node is not None:node.deleteLater()
        node=QRemoteObjectNode()
        node.connectToNode(QUrl(args.endpoint))
        replica=node.acquireDynamic('SensorProvider')
        methods={};metadata.clear();values.clear()
        last_metadata=last_poll=0;last_valid=False;subscribed=False;dirty=True

    def batch(requests,timeout_ms=1000):
        # Queue independent calls together instead of blocking once per field/sensor.
        pending=[replica.sendWithReply(QMetaObject.Call.InvokeMetaMethod,methods[name],argv)
                 for name,argv in requests]
        deadline=time.monotonic()+timeout_ms/1000
        while not all(p.isFinished() for p in pending):
            if time.monotonic()>=deadline:raise TimeoutError('SensorProvider batch timed out')
            app.processEvents();time.sleep(.002)
        return [p.returnValue() for p in pending]

    def flush():
        nonlocal last_flush,dirty,last_state
        now=time.time()
        root=ET.Element('OriginalSensors',time=str(now),events=str(events),state=state)
        for sid,meta in metadata.items():
            reading=values.get(sid)
            if reading is not None and 0<=now-reading[1]<10:
                ET.SubElement(root,'Sensor',id=sid,value=reading[0],sampled=str(reading[1]),**meta)
        tmp=output.with_suffix('.tmp')
        ET.ElementTree(root).write(tmp,encoding='utf-8',xml_declaration=True)
        os.replace(tmp,output)
        if state!=last_state:
            with output.with_suffix('.bridge.log').open('a',encoding='utf-8') as f:
                f.write(time.strftime('%Y-%m-%dT%H:%M:%S')+' '+state+' sensors='+str(len(root))+'\n')
            last_state=state
        last_flush=time.monotonic();dirty=False

    reset()
    try:
        while kernel.WaitForSingleObject(handle,0)==258:
            try:
                app.processEvents()
                if not replica.isInitialized():
                    if last_valid:
                        metadata.clear();values.clear();last_metadata=0;last_valid=False;dirty=True
                    state='waiting'
                    replica.waitForSource(100)
                else:
                    if not last_valid:
                        meta=replica.metaObject()
                        methods={bytes(meta.method(i).name()).decode():i for i in range(meta.methodOffset(),meta.methodCount())}
                        # Signals stay attached across reconnections of this same replica.
                        if not subscribed:
                            replica.sensorValueChanged.connect(value_changed)
                            replica.sensorAdded.connect(invalidate)
                            replica.sensorRemoved.connect(invalidate)
                            replica.sensorDataChanged.connect(invalidate)
                            subscribed=True
                        last_valid=True;last_metadata=0
                    now=time.monotonic()
                    if now-last_metadata>=(30 if metadata else .5):
                        ids=[str(x) for x in batch([('getAllSensorIds',[])])[0]]
                        fields=['Name','DeviceName','Type','Kind','Units']
                        answers=batch([('getSensor'+field,[sid]) for sid in ids for field in fields])
                        updated={sid:{field:str(answers[i*len(fields)+j]) for j,field in enumerate(fields)} for i,sid in enumerate(ids)}
                        metadata.clear();metadata.update({sid:meta for sid,meta in updated.items() if meta['DeviceName']})
                        last_metadata=time.monotonic();last_poll=0
                    if metadata and time.monotonic()-last_poll>=1:
                        ids=list(metadata)
                        answers=batch([(name,[sid]) for sid in ids for name in ('sensorIsConnected','getSensorValue')])
                        sampled=time.time()
                        for i,sid in enumerate(ids):
                            if answers[i*2]:values[sid]=(str(answers[i*2+1]),sampled)
                            else:values.pop(sid,None)
                        last_poll=time.monotonic();dirty=True
                    state='ready' if values else 'waiting-sensors'
                    failures=0
                if time.monotonic()-last_flush>=1 or (dirty and time.monotonic()-last_flush>=.1):flush()
            except Exception:
                failures+=1;state='reconnecting';last_metadata=0;dirty=True
                # Recreate the local replica after repeated failures; never restart official iCUE.
                if failures>=3:reset();failures=0
                try:flush()
                except OSError:pass
                time.sleep(.1)
            time.sleep(.02)
    finally:
        kernel.CloseHandle(handle)
    return 0

if __name__=='__main__':raise SystemExit(run())
