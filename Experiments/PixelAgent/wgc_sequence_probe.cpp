// Owned-window-only Windows Graphics Capture probe. No input, no desktop capture.
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <iostream>
#include <atomic>
#include <fstream>
#include <string_view>
using namespace winrt;
using namespace winrt::Windows::Graphics::Capture;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::Graphics::DirectX::Direct3D11;

static unsigned owned_downs=0,owned_ups=0;
LRESULT CALLBACK ProbeProc(HWND h, UINT message, WPARAM wp, LPARAM lp) {
    if(message==WM_KEYDOWN && wp=='J'){++owned_downs;InvalidateRect(h,nullptr,TRUE);return 0;}
    if(message==WM_KEYUP && wp=='J'){++owned_ups;return 0;}
    if(message==WM_PAINT) {
        PAINTSTRUCT ps{};HDC dc=BeginPaint(h,&ps);
        RECT r{};GetClientRect(h,&r);
        RECT left=r;left.right=(r.right-r.left)/2;
        HBRUSH red=CreateSolidBrush(RGB(50,50,50)),blue=CreateSolidBrush(RGB(100,100,100));
        FillRect(dc,&r,blue);FillRect(dc,&left,red);
        DeleteObject(red);DeleteObject(blue);EndPaint(h,&ps);return 0;
    }
    if(message==WM_MOUSEACTIVATE)return MA_NOACTIVATE;
    return DefWindowProcW(h,message,wp,lp);
}
struct OwnedProcess {
 PROCESS_INFORMATION pi{};
 ~OwnedProcess(){if(pi.hProcess){if(WaitForSingleObject(pi.hProcess,32000)==WAIT_TIMEOUT)TerminateProcess(pi.hProcess,9);CloseHandle(pi.hProcess);CloseHandle(pi.hThread);}}
};
struct OwnedWindow {
    HWND hwnd{};
    ~OwnedWindow(){if(hwnd)DestroyWindow(hwnd);}
};
int main(int argc, char** argv) {
    if(argc!=4||std::string_view(argv[1])!="--unity-covered")return 6;
    const bool covered=true;OwnedProcess child;HWND target{};
    LASTINPUTINFO input{sizeof(LASTINPUTINFO)};if(!GetLastInputInfo(&input))return 5;
    HWND previous=GetForegroundWindow();
    OwnedWindow owned;
    const char* phase="init";
    try {
        init_apartment(apartment_type::multi_threaded);
        if(!GraphicsCaptureSession::IsSupported()) {
            std::cout<<"UNSUPPORTED\n";return 2;
        }
        WNDCLASSW cls{};cls.lpfnWndProc=ProbeProc;cls.hInstance=GetModuleHandleW(nullptr);cls.lpszClassName=L"OwnedWgcPattern";
        if(!RegisterClassW(&cls))throw hresult_error(HRESULT_FROM_WIN32(GetLastError()));
        owned.hwnd=CreateWindowExW(WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW,cls.lpszClassName,
            L"Owned WGC capture probe",WS_POPUP,-32000,-32000,640,480,
            nullptr,nullptr,GetModuleHandleW(nullptr),nullptr);
        if(!owned.hwnd)throw hresult_error(HRESULT_FROM_WIN32(GetLastError()));
        DWORD pid{};GetWindowThreadProcessId(owned.hwnd,&pid);
        if(pid!=GetCurrentProcessId())throw hresult_error(E_ACCESSDENIED);
        // Optional covered fixture: under the opaque foreground window, never on top.
        RECT vr{GetSystemMetrics(SM_XVIRTUALSCREEN),GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_XVIRTUALSCREEN)+GetSystemMetrics(SM_CXVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN)+GetSystemMetrics(SM_CYVIRTUALSCREEN)};
        if(covered) {
            RECT fg{};auto style=GetWindowLongPtrW(previous,GWL_EXSTYLE);
            if(!previous||IsIconic(previous)||!GetWindowRect(previous,&fg)||
               (style&(WS_EX_LAYERED|WS_EX_TRANSPARENT))||
               fg.right-fg.left<720||fg.bottom-fg.top<560||
               DWORD(GetTickCount()-input.dwTime)<10000) {
                std::cout<<"BLOCKED foreground_or_recent_user_activity\n";return 5;
            }
            RECT candidate{fg.left+40,fg.top+40,fg.left+680,fg.top+520},clip{};
            if(!IntersectRect(&clip,&vr,&candidate)||!EqualRect(&clip,&candidate)){
                std::cout<<"BLOCKED target_outside_desktop\n";return 5;
            }
            if(!SetWindowPos(owned.hwnd,HWND_BOTTOM,candidate.left,candidate.top,640,480,
                            SWP_NOACTIVATE|SWP_SHOWWINDOW))throw hresult_error(HRESULT_FROM_WIN32(GetLastError()));
        } else {
            RECT r{};GetWindowRect(owned.hwnd,&r);RECT overlap{};
            if(IntersectRect(&overlap,&vr,&r))throw hresult_error(E_ACCESSDENIED);
            ShowWindow(owned.hwnd,SW_SHOWNOACTIVATE);
        }
        InvalidateRect(owned.hwnd,nullptr,TRUE);UpdateWindow(owned.hwnd);
        if(GetForegroundWindow()!=previous)throw hresult_error(E_ACCESSDENIED);
        phase="launch_owned_unity";
        std::string path(argv[2]);std::wstring exe(path.begin(),path.end());
        std::wstring command=L"\""+exe+L"\" -parentHWND "+std::to_wstring(reinterpret_cast<uintptr_t>(owned.hwnd))+
            L" delayed -screen-fullscreen 0 -screen-width 640 -screen-height 480 -force-d3d11 -logFile external-player.log -towerExternalInput -towerAutoSeconds 20 -towerFrameRate 30 -towerDataRoot \""+std::wstring(argv[3],argv[3]+strlen(argv[3]))+L"\" -towerReport \""+std::wstring(argv[3],argv[3]+strlen(argv[3]))+L"\\report.json\"";
        STARTUPINFOW si{sizeof(si)};si.dwFlags=STARTF_USESHOWWINDOW;si.wShowWindow=SW_HIDE;
        if(!CreateProcessW(exe.c_str(),command.data(),nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,nullptr,&si,&child.pi))
            throw hresult_error(HRESULT_FROM_WIN32(GetLastError()));
        auto startupDeadline=GetTickCount64()+5000;
        while(GetTickCount64()<startupDeadline){
            MSG m{};while(PeekMessageW(&m,nullptr,0,0,PM_REMOVE)){TranslateMessage(&m);DispatchMessageW(&m);}
            LASTINPUTINFO li{sizeof(li)};
            if(!GetLastInputInfo(&li)||li.dwTime!=input.dwTime||GetForegroundWindow()!=previous)throw hresult_error(E_ABORT);
            Sleep(10);
        }
        struct Find {DWORD pid;HWND found;};Find find{child.pi.dwProcessId,nullptr};
        EnumChildWindows(owned.hwnd,[](HWND h,LPARAM data)->BOOL{
            auto f=reinterpret_cast<Find*>(data);DWORD p{};GetWindowThreadProcessId(h,&p);
            wchar_t kind[128]{};GetClassNameW(h,kind,128);
            if(p==f->pid && std::wstring_view(kind)==L"UnityWndClass"){f->found=h;return FALSE;}return TRUE;
        },reinterpret_cast<LPARAM>(&find));
        target=find.found;
        if(!target)throw hresult_error(E_FAIL);
        // Only our spawned Unity window becomes a covered, nonactivating top-level.
        SetWindowLongPtrW(target,GWL_EXSTYLE,GetWindowLongPtrW(target,GWL_EXSTYLE)|WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW);
        SetWindowLongPtrW(target,GWL_STYLE,WS_POPUP|WS_VISIBLE);
        SetParent(target,nullptr);
        RECT hostRect{};GetWindowRect(owned.hwnd,&hostRect);
        if(!SetWindowPos(target,HWND_BOTTOM,hostRect.left,hostRect.top,640,480,SWP_NOACTIVATE|SWP_SHOWWINDOW|SWP_FRAMECHANGED))
            throw hresult_error(HRESULT_FROM_WIN32(GetLastError()));
        auto renderDeadline=GetTickCount64()+1500;
        while(GetTickCount64()<renderDeadline){
            MSG m{};while(PeekMessageW(&m,nullptr,0,0,PM_REMOVE)){TranslateMessage(&m);DispatchMessageW(&m);}Sleep(10);
            if(GetForegroundWindow()!=previous)throw hresult_error(E_ABORT);
        }
        phase="CreateForWindow";
        auto interop=get_activation_factory<GraphicsCaptureItem,IGraphicsCaptureItemInterop>();
        GraphicsCaptureItem item{nullptr};
        check_hresult(interop->CreateForWindow(target,guid_of<GraphicsCaptureItem>(),put_abi(item)));
        auto size=item.Size();
        if(size.Width<=0||size.Height<=0||size.Width>1920||size.Height>1080)
            throw hresult_error(E_INVALIDARG);
        phase="D3D_WARP";
        com_ptr<ID3D11Device> d3d;com_ptr<ID3D11DeviceContext> context;
        check_hresult(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,d3d.put(),nullptr,context.put()));
        auto dxgi=d3d.as<IDXGIDevice>();com_ptr<::IInspectable> inspectable;
        check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.get(),inspectable.put()));
        auto device=inspectable.as<IDirect3DDevice>();
        phase="framepool";
        auto pool=Direct3D11CaptureFramePool::CreateFreeThreaded(device,
            DirectXPixelFormat::B8G8R8A8UIntNormalized,1,size);
        auto session=pool.CreateCaptureSession(item);
        phase="StartCapture";
        DWORD owner{};GetWindowThreadProcessId(target,&owner);
        if(owner!=child.pi.dwProcessId)throw hresult_error(E_ACCESSDENIED);
        auto safeWait=[&](ULONGLONG ms){
            auto until=GetTickCount64()+ms;
            while(GetTickCount64()<until){
                LASTINPUTINFO li{sizeof(li)};DWORD currentPid{};GetWindowThreadProcessId(target,&currentPid);
                if(currentPid!=child.pi.dwProcessId||GetForegroundWindow()!=previous||!GetLastInputInfo(&li)||li.dwTime!=input.dwTime)throw hresult_error(E_ABORT);
                MSG m{};while(PeekMessageW(&m,nullptr,0,0,PM_REMOVE)){TranslateMessage(&m);DispatchMessageW(&m);}Sleep(5);
            }
        };
        for(auto key:{VK_RETURN,VK_RETURN,VK_ESCAPE,int('J')}){
            safeWait(50);
            if(!PostMessageW(target,WM_KEYDOWN,key,1))throw hresult_error(E_FAIL);
            safeWait(100);
            if(!PostMessageW(target,WM_KEYUP,key,0xC0000001))throw hresult_error(E_FAIL);
            safeWait(800);
        }
        // Scripted transport smoke, not a screen-driven policy or a combat win.
        session.StartCapture();
        long long lastStamp=-1;
        for(int seq=0;seq<12;++seq){
        Direct3D11CaptureFrame frame{nullptr};
        auto deadline=GetTickCount64()+3000;
        while(GetTickCount64()<deadline&&!frame){
            MSG msg{};
            while(PeekMessageW(&msg,nullptr,0,0,PM_REMOVE)){TranslateMessage(&msg);DispatchMessageW(&msg);}
            LASTINPUTINFO latest{sizeof(LASTINPUTINFO)};
            if(!GetLastInputInfo(&latest)||latest.dwTime!=input.dwTime||GetForegroundWindow()!=previous)
                throw hresult_error(E_ABORT);
            frame=pool.TryGetNextFrame();if(!frame)Sleep(10);
        }
        if(!frame){session.Close();pool.Close();std::cout<<"NO_FRAME timeout_ms=3000 foreground_unchanged="<<(GetForegroundWindow()==previous)<<"\n";return 3;}
        auto stamp=frame.SystemRelativeTime().count();
        LARGE_INTEGER qpc{},freq{};QueryPerformanceCounter(&qpc);QueryPerformanceFrequency(&freq);
        double ageMs=(double(qpc.QuadPart)/freq.QuadPart-double(stamp)/10000000.0)*1000.0;
        if(stamp<=lastStamp||ageMs<0||ageMs>500)throw hresult_error(E_ABORT);lastStamp=stamp;
        phase="readback";
        auto access=frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
        com_ptr<ID3D11Texture2D> source;
        check_hresult(access->GetInterface(__uuidof(ID3D11Texture2D),source.put_void()));
        D3D11_TEXTURE2D_DESC desc{};source->GetDesc(&desc);
        if(desc.Width>1920||desc.Height>1080)throw hresult_error(E_INVALIDARG);
        desc.Usage=D3D11_USAGE_STAGING;desc.BindFlags=0;desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;desc.MiscFlags=0;
        com_ptr<ID3D11Texture2D> staging;check_hresult(d3d->CreateTexture2D(&desc,nullptr,staging.put()));
        context->CopyResource(staging.get(),source.get());
        D3D11_MAPPED_SUBRESOURCE map{};check_hresult(context->Map(staging.get(),0,D3D11_MAP_READ,0,&map));
        bool varied=false;size_t reds=0,blues=0;
        std::ofstream ppm("sequence-"+std::to_string(seq)+".ppm",std::ios::binary);ppm<<"P6\n"<<desc.Width<<" "<<desc.Height<<"\n255\n";
        auto bytes=static_cast<unsigned char*>(map.pData);
        for(UINT y=0;y<desc.Height;y++)for(UINT x=0;x<desc.Width;x++){
            auto p=bytes+y*map.RowPitch+x*4;
            char rgb[3]={char(p[2]),char(p[1]),char(p[0])};ppm.write(rgb,3);
            if(p[0]!=bytes[0]||p[1]!=bytes[1]||p[2]!=bytes[2])varied=true;
            if(p[2]>150&&p[1]<100&&p[0]<100)++reds;
            if(p[0]>150&&p[1]<120&&p[2]<100)++blues;
        }
        context->Unmap(staging.get(),0);frame.Close();
        LASTINPUTINFO finalInput{sizeof(LASTINPUTINFO)};
        bool stable=GetForegroundWindow()==previous&&GetLastInputInfo(&finalInput)&&finalInput.dwTime==input.dwTime;
        std::cout<<"FRAME seq="<<seq<<" stamp100ns="<<stamp<<" age_ms="<<ageMs<<" width="<<desc.Width<<" height="<<desc.Height
            <<" nonflat="<<varied<<" red_pixels="<<reds<<" blue_pixels="<<blues
            <<" unity_pid="<<child.pi.dwProcessId
            <<" foreground_unchanged="<<stable<<" external_game_smoke=true global_input=false\n";
        if(!varied||!stable)return 4;
        safeWait(100);
        }
        session.Close();pool.Close();return 0;
    } catch(hresult_error const& e) {
        std::cout<<"FAILED phase="<<phase<<" HRESULT=0x"<<std::hex<<static_cast<unsigned long>(e.code())
            <<" foreground_unchanged="<<(GetForegroundWindow()==previous)<<"\n";
        return 1;
    }
}
