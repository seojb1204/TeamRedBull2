"""Bounded target-HWND PrintWindow capture. No activation or input injection.
Only pixels explicitly rendered by target window are requested; no desktop blit.
Native capture success is NOT evidence of correct/current Unity rendering.
"""
import ctypes
import math
import multiprocessing as mp
import queue
import time
from ctypes import wintypes as w
from dataclasses import dataclass
from windows_observer import Win32Observer

MAX_PIXELS=1920*1080

@dataclass(frozen=True)
class CapturedFrame:
    width:int
    height:int
    rgb:bytes
    started_at:float
    ended_at:float

def validate_payload(payload, identity, now, max_age=1.0):
    if not isinstance(payload,dict) or payload.get('error'):
        raise RuntimeError('Capture worker failed: '+str(payload.get('error','invalid payload')) if isinstance(payload,dict) else 'Invalid capture payload')
    if type(payload.get('identity')) is not tuple or any(type(v) is not int for v in payload['identity']) or payload['identity']!=identity:raise RuntimeError('Capture identity changed')
    width,height=payload.get('width'),payload.get('height')
    if type(width) is not int or type(height) is not int or width<=0 or height<=0 or width*height>MAX_PIXELS:
        raise RuntimeError('Invalid capture dimensions')
    rgb=payload.get('rgb')
    if type(rgb) is not bytes or len(rgb)!=width*height*3:raise RuntimeError('Invalid RGB size')
    start,end=payload.get('started_at'),payload.get('ended_at')
    if any(type(v) not in (int,float) or not math.isfinite(v) for v in (start,end,now)):
        raise RuntimeError('Invalid capture time')
    if not start<=end<=now or now-start>max_age:raise RuntimeError('Stale capture')
    # Fail closed on flat output, including common black/white PrintWindow failures.
    first=rgb[:3]
    if all(rgb[i:i+3]==first for i in range(0,len(rgb),3)):
        raise RuntimeError('Blank capture')
    return CapturedFrame(width,height,rgb,start,end)

def _native_worker(out,identity):
    hwnd,pid,created=identity
    try:
        start=time.monotonic()
        observer=Win32Observer()
        before=observer.read(hwnd,expected_pid=pid,expected_created_100ns=created)
        if before.minimized:raise RuntimeError('Minimized window')
        width,height=before.width,before.height
        if width*height>MAX_PIXELS:raise RuntimeError('Capture too large')
        g=ctypes.WinDLL('gdi32',use_last_error=True);u=observer.u
        class Header(ctypes.Structure):
            _fields_=[('size',w.DWORD),('width',w.LONG),('height',w.LONG),
                      ('planes',w.WORD),('bits',w.WORD),('compression',w.DWORD),
                      ('image_size',w.DWORD),('xppm',w.LONG),('yppm',w.LONG),
                      ('colors',w.DWORD),('important',w.DWORD)]
        class Info(ctypes.Structure):
            _fields_=[('header',Header),('color',w.DWORD*3)]
        g.CreateCompatibleDC.argtypes=[w.HDC];g.CreateCompatibleDC.restype=w.HDC
        g.CreateDIBSection.argtypes=[w.HDC,ctypes.POINTER(Info),w.UINT,ctypes.POINTER(ctypes.c_void_p),w.HANDLE,w.DWORD]
        g.CreateDIBSection.restype=w.HANDLE
        g.SelectObject.argtypes=[w.HDC,w.HANDLE];g.SelectObject.restype=w.HANDLE
        g.DeleteObject.argtypes=[w.HANDLE];g.DeleteDC.argtypes=[w.HDC]
        u.PrintWindow.argtypes=[w.HWND,w.HDC,w.UINT];u.PrintWindow.restype=w.BOOL
        dc=g.CreateCompatibleDC(None)
        if not dc:raise RuntimeError('DC creation failed')
        bitmap=None;old=None
        try:
            info=Info();info.header=Header(ctypes.sizeof(Header),width,-height,1,32,0,width*height*4,0,0,0,0)
            bits=ctypes.c_void_p()
            bitmap=g.CreateDIBSection(dc,ctypes.byref(info),0,ctypes.byref(bits),None,0)
            if not bitmap or not bits.value:raise RuntimeError('Bitmap creation failed')
            old=g.SelectObject(dc,bitmap)
            if not old:raise RuntimeError('Bitmap selection failed')
            if not u.PrintWindow(hwnd,dc,1):raise RuntimeError('PrintWindow failed')
            raw=ctypes.string_at(bits,width*height*4)
            rgb=bytes(channel for i in range(0,len(raw),4) for channel in (raw[i+2],raw[i+1],raw[i]))
        finally:
            if old:g.SelectObject(dc,old)
            if bitmap:g.DeleteObject(bitmap)
            g.DeleteDC(dc)
        after=observer.read(hwnd,expected_pid=pid,expected_created_100ns=created)
        if (after.width,after.height,after.minimized)!=(width,height,False):
            raise RuntimeError('Window changed during capture')
        out.put(dict(identity=identity,width=width,height=height,rgb=rgb,
                     started_at=start,ended_at=time.monotonic()))
    except Exception as exc:
        out.put({'error':type(exc).__name__+': '+str(exc)})

def capture(hwnd,pid,created_100ns,*,timeout=1.0,_worker=_native_worker):
    identity=(hwnd,pid,created_100ns)
    if any(type(v) is not int or v<=0 for v in identity):
        raise ValueError('Exact positive window/process identity required')
    if type(timeout) not in (int,float) or not math.isfinite(timeout) or not .05<=timeout<=3:
        raise ValueError('Timeout must be 0.05 to 3 seconds')
    ctx=mp.get_context('spawn');out=ctx.Queue(maxsize=1)
    proc=ctx.Process(target=_worker,args=(out,identity),daemon=True)
    try:
        proc.start()
        try:payload=out.get(timeout=timeout)
        except queue.Empty:raise TimeoutError('Capture worker deadline exceeded')
        return validate_payload(payload,identity,time.monotonic(),timeout)
    finally:
        # Only this function's own worker may be terminated; never target process.
        if proc.pid:
            if proc.is_alive():proc.terminate()
            proc.join(timeout=.2)
        out.close()
