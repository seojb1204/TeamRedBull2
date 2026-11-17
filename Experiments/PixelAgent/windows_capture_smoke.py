"""Native target-only capture on our own unshown test window, never the game."""
import ctypes,json,threading,time
from ctypes import wintypes as w
from windows_observer import Win32Observer
from windows_capture import capture
def main():
    o=Win32Observer();u=o.u
    u.CreateWindowExW.argtypes=[w.DWORD,w.LPCWSTR,w.LPCWSTR,w.DWORD,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int,w.HWND,w.HMENU,w.HINSTANCE,ctypes.c_void_p]
    u.CreateWindowExW.restype=w.HWND
    u.DestroyWindow.argtypes=[w.HWND];u.DestroyWindow.restype=w.BOOL
    before=u.GetForegroundWindow()
    h=u.CreateWindowExW(0,'STATIC','Owned capture test 123',0x80000000,0,0,320,240,None,None,None,None)
    assert h
    try:
        e=o.read(int(h))
        result={}
        def run():
            try:result['frame']=capture(int(h),e.pid,e.created_100ns,timeout=3)
            except Exception as exc:result['error']=exc
        worker=threading.Thread(target=run);worker.start()
        u.PeekMessageW.argtypes=[ctypes.POINTER(w.MSG),w.HWND,w.UINT,w.UINT,w.UINT]
        u.TranslateMessage.argtypes=[ctypes.POINTER(w.MSG)]
        u.DispatchMessageW.argtypes=[ctypes.POINTER(w.MSG)];u.DispatchMessageW.restype=ctypes.c_ssize_t
        msg=w.MSG();deadline=time.monotonic()+5
        while worker.is_alive() and time.monotonic()<deadline:
            while u.PeekMessageW(ctypes.byref(msg),None,0,0,1):
                u.TranslateMessage(ctypes.byref(msg));u.DispatchMessageW(ctypes.byref(msg))
            time.sleep(.005)
        worker.join(timeout=.1)
        assert not worker.is_alive(),'capture thread did not terminate'
        if 'error' in result:raise result['error']
        f=result['frame']
        assert (f.width,f.height,len(f.rgb))==(320,240,320*240*3)
        assert len(set(f.rgb))>1
        assert u.GetForegroundWindow()==before
        print(json.dumps({'actual_win32_capture':'PASS','size':[f.width,f.height],
                          'foreground_unchanged':True,'game_pixels':False,'input_sent':False}))
    finally:assert u.DestroyWindow(h)
if __name__=='__main__':main()
