"""Read-only Win32 window evidence. No capture, activation or input injection.
This module does NOT assert whole-host/GPU safety; those require separate probes.
"""
import ctypes
import os
from dataclasses import dataclass
from ctypes import wintypes as w

@dataclass(frozen=True)
class WindowEvidence:
    pid: int
    created_100ns: int
    hwnd: int
    foreground_hwnd: int
    width: int
    height: int
    minimized: bool
    user_input_tick32: int
    image_path: str

class Win32Observer:
    def __init__(self):
        if os.name != 'nt':
            raise RuntimeError('Windows required')
        self.u=ctypes.WinDLL('user32',use_last_error=True)
        self.k=ctypes.WinDLL('kernel32',use_last_error=True)
        self.u.IsWindow.argtypes=[w.HWND]; self.u.IsWindow.restype=w.BOOL
        self.u.IsIconic.argtypes=[w.HWND]; self.u.IsIconic.restype=w.BOOL
        self.u.GetForegroundWindow.argtypes=[];self.u.GetForegroundWindow.restype=w.HWND
        self.u.GetWindowThreadProcessId.argtypes=[w.HWND,ctypes.POINTER(w.DWORD)]
        self.u.GetWindowThreadProcessId.restype=w.DWORD
        self.u.GetClientRect.argtypes=[w.HWND,ctypes.POINTER(w.RECT)]
        self.u.GetClientRect.restype=w.BOOL
        self.k.OpenProcess.argtypes=[w.DWORD,w.BOOL,w.DWORD];self.k.OpenProcess.restype=w.HANDLE
        self.k.CloseHandle.argtypes=[w.HANDLE];self.k.CloseHandle.restype=w.BOOL
        self.k.GetProcessTimes.argtypes=[w.HANDLE]+[ctypes.POINTER(w.FILETIME)]*4
        self.k.GetProcessTimes.restype=w.BOOL
        self.k.QueryFullProcessImageNameW.argtypes=[w.HANDLE,w.DWORD,w.LPWSTR,ctypes.POINTER(w.DWORD)]
        self.k.QueryFullProcessImageNameW.restype=w.BOOL
        class LastInput(ctypes.Structure):
            _fields_=[('cbSize',w.UINT),('dwTime',w.DWORD)]
        self.LastInput=LastInput
        self.u.GetLastInputInfo.argtypes=[ctypes.POINTER(LastInput)]
        self.u.GetLastInputInfo.restype=w.BOOL

    def read(self, hwnd, *, expected_pid=None, expected_created_100ns=None):
        if type(hwnd) is not int or hwnd <= 0:
            raise ValueError('Positive integer HWND required')
        if expected_pid is not None and (type(expected_pid) is not int or expected_pid<=0):
            raise ValueError('Invalid expected PID')
        if expected_created_100ns is not None and (type(expected_created_100ns) is not int or expected_created_100ns<=0):
            raise ValueError('Invalid expected process creation time')
        if not self.u.IsWindow(hwnd):
            raise RuntimeError('Window not available')
        pid=w.DWORD()
        if not self.u.GetWindowThreadProcessId(hwnd,ctypes.byref(pid)) or not pid.value:
            raise ctypes.WinError(ctypes.get_last_error())
        if expected_pid is not None and pid.value!=expected_pid:
            raise RuntimeError('Window owner changed')
        handle=self.k.OpenProcess(0x1000,False,pid.value)
        if not handle:raise ctypes.WinError(ctypes.get_last_error())
        try:
            created,exited,kernel,user=(w.FILETIME() for _ in range(4))
            if not self.k.GetProcessTimes(handle,*map(ctypes.byref,(created,exited,kernel,user))):
                raise ctypes.WinError(ctypes.get_last_error())
            stamp=(created.dwHighDateTime<<32)|created.dwLowDateTime
            if expected_created_100ns is not None and stamp!=expected_created_100ns:
                raise RuntimeError('Process identity changed')
            buf=ctypes.create_unicode_buffer(32768);size=w.DWORD(len(buf))
            if not self.k.QueryFullProcessImageNameW(handle,0,buf,ctypes.byref(size)):
                raise ctypes.WinError(ctypes.get_last_error())
            rect=w.RECT()
            if not self.u.GetClientRect(hwnd,ctypes.byref(rect)):
                raise ctypes.WinError(ctypes.get_last_error())
            width,height=rect.right-rect.left,rect.bottom-rect.top
            if width<=0 or height<=0:raise RuntimeError('Empty client rectangle')
            li=self.LastInput();li.cbSize=ctypes.sizeof(li)
            if not self.u.GetLastInputInfo(ctypes.byref(li)):
                raise ctypes.WinError(ctypes.get_last_error())
            # Recheck HWND ownership after querying process metadata.
            final=w.DWORD()
            if not self.u.IsWindow(hwnd) or not self.u.GetWindowThreadProcessId(hwnd,ctypes.byref(final)) or final.value!=pid.value:
                raise RuntimeError('Window changed during observation')
            return WindowEvidence(pid.value,stamp,hwnd,int(self.u.GetForegroundWindow() or 0),
                                  width,height,bool(self.u.IsIconic(hwnd)),li.dwTime,buf.value)
        finally:
            self.k.CloseHandle(handle)
