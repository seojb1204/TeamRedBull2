"""Windows-only hidden host for our spawned Unity process. No user-window operations."""
import os

class HiddenUnityHost:
    def __init__(self):
        if os.name!='nt': raise RuntimeError('Hidden Unity host requires Windows')
        import ctypes
        from ctypes import wintypes as w
        self.c,self.w=ctypes,w
        u=self.u=ctypes.WinDLL('user32',use_last_error=True)
        self.callback=ctypes.WINFUNCTYPE(w.BOOL,w.HWND,w.LPARAM)
        u.CreateWindowExW.argtypes=[w.DWORD,w.LPCWSTR,w.LPCWSTR,w.DWORD,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int,w.HWND,w.HMENU,w.HINSTANCE,ctypes.c_void_p]
        u.CreateWindowExW.restype=w.HWND
        u.GetWindowThreadProcessId.argtypes=[w.HWND,ctypes.POINTER(w.DWORD)]
        u.IsWindowVisible.argtypes=[w.HWND];u.IsWindowVisible.restype=w.BOOL
        u.EnumWindows.argtypes=[self.callback,w.LPARAM]
        u.EnumChildWindows.argtypes=[w.HWND,self.callback,w.LPARAM]
        u.SetParent.argtypes=[w.HWND,w.HWND];u.SetParent.restype=w.HWND
        u.DestroyWindow.argtypes=[w.HWND]
        u.PeekMessageW.argtypes=[ctypes.POINTER(w.MSG),w.HWND,w.UINT,w.UINT,w.UINT]
        u.TranslateMessage.argtypes=[ctypes.POINTER(w.MSG)]
        u.DispatchMessageW.argtypes=[ctypes.POINTER(w.MSG)];u.DispatchMessageW.restype=ctypes.c_ssize_t
        self.hwnd=u.CreateWindowExW(0,'STATIC','TowerOwnedHiddenParent',0x80000000,0,0,960,540,None,None,None,None)
        if not self.hwnd: raise ctypes.WinError(ctypes.get_last_error())
        if u.IsWindowVisible(self.hwnd): self.close();raise RuntimeError('Hidden parent became visible')

    def pump_and_guard(self,pid):
        c,w,u=self.c,self.w,self.u
        message=w.MSG()
        while u.PeekMessageW(c.byref(message),None,0,0,1):
            u.TranslateMessage(c.byref(message));u.DispatchMessageW(c.byref(message))
        owned=[];visible=[]
        def inspect(hwnd,unused):
            process=w.DWORD();u.GetWindowThreadProcessId(hwnd,c.byref(process))
            if process.value==pid:
                owned.append(hwnd)
                if u.IsWindowVisible(hwnd):visible.append(hwnd)
            return True
        callback=self.callback(inspect)
        u.EnumWindows(callback,0);top_level=list(owned)
        u.EnumChildWindows(self.hwnd,callback,0)
        if visible or u.IsWindowVisible(self.hwnd):raise RuntimeError('Hidden game window became visible; stopping owned experiment')
        for hwnd in set(top_level):
            c.set_last_error(0)
            previous=u.SetParent(hwnd,self.hwnd)
            if not previous and c.get_last_error():raise c.WinError(c.get_last_error())

    def close(self):
        if self.hwnd:self.u.DestroyWindow(self.hwnd);self.hwnd=None
