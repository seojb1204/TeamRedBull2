"""Actual Win32 read-only checks using our own unshown STATIC test window."""
import ctypes, json
from ctypes import wintypes as w
from windows_observer import Win32Observer
o=Win32Observer();u=o.u;k=o.k
u.CreateWindowExW.argtypes=[w.DWORD,w.LPCWSTR,w.LPCWSTR,w.DWORD,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int,w.HWND,w.HMENU,w.HINSTANCE,ctypes.c_void_p]
u.CreateWindowExW.restype=w.HWND
u.DestroyWindow.argtypes=[w.HWND];u.DestroyWindow.restype=w.BOOL
k.GetCurrentProcessId.restype=w.DWORD
before=u.GetForegroundWindow()
h=u.CreateWindowExW(0,'STATIC','OwnedBotObserverTest',0x80000000,0,0,320,240,None,None,None,None)
assert h,'test window failed'
try:
    e=o.read(int(h),expected_pid=k.GetCurrentProcessId())
    assert e.width==320 and e.height==240
    assert e.created_100ns>0 and e.image_path.lower().endswith('python.exe')
    assert e.foreground_hwnd==int(before or 0)
    again=o.read(int(h),expected_pid=e.pid,expected_created_100ns=e.created_100ns)
    assert again.created_100ns==e.created_100ns
    rejected=0
    for kwargs in ({'expected_pid':e.pid+1},{'expected_created_100ns':e.created_100ns+1}):
        try:o.read(int(h),**kwargs)
        except RuntimeError:rejected+=1
    for bad in (0,-1,True,'1',None,1.0):
        try:o.read(bad)
        except ValueError:rejected+=1
    assert rejected==8
finally:
    assert u.DestroyWindow(h)
try:o.read(int(h))
except RuntimeError:rejected+=1
assert rejected==9
assert u.GetForegroundWindow()==before
print(json.dumps({'result':'PASS','actual_win32':True,'identity_and_dimensions':True,'rejections':rejected,'foreground_unchanged':True,'native_input_sent':False,'game_launched':False}))
