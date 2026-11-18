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
        HBRUSH red=CreateSolidBrush(RGB(220,30,30)),blue=CreateSolidBrush(RGB(30,60,220));
        FillRect(dc,&r,blue);FillRect(dc,&left,red);
        DeleteObject(red);DeleteObject(blue);EndPaint(h,&ps);return 0;
    }
    if(message==WM_MOUSEACTIVATE)return MA_NOACTIVATE;
    return DefWindowProcW(h,message,wp,lp);
}
struct OwnedWindow {
    HWND hwnd{};
    ~OwnedWindow(){if(hwnd)DestroyWindow(hwnd);}
};
int main(int argc, char** argv) {
    if(argc>2||(argc==2&&std::string_view(argv[1])!="--covered"))return 6;
    const bool covered=argc==2;
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
            L"Owned WGC capture probe",WS_POPUP,-32000,-32000,320,240,
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
               fg.right-fg.left<400||fg.bottom-fg.top<320||
               DWORD(GetTickCount()-input.dwTime)<10000) {
                std::cout<<"BLOCKED foreground_or_recent_user_activity\n";return 5;
            }
            RECT candidate{fg.left+40,fg.top+40,fg.left+360,fg.top+280},clip{};
            if(!IntersectRect(&clip,&vr,&candidate)||!EqualRect(&clip,&candidate)){
                std::cout<<"BLOCKED target_outside_desktop\n";return 5;
            }
            if(!SetWindowPos(owned.hwnd,HWND_BOTTOM,candidate.left,candidate.top,320,240,
                            SWP_NOACTIVATE|SWP_SHOWWINDOW))throw hresult_error(HRESULT_FROM_WIN32(GetLastError()));
        } else {
            RECT r{};GetWindowRect(owned.hwnd,&r);RECT overlap{};
            if(IntersectRect(&overlap,&vr,&r))throw hresult_error(E_ACCESSDENIED);
            ShowWindow(owned.hwnd,SW_SHOWNOACTIVATE);
        }
        InvalidateRect(owned.hwnd,nullptr,TRUE);UpdateWindow(owned.hwnd);
        if(GetForegroundWindow()!=previous)throw hresult_error(E_ACCESSDENIED);
        phase="CreateForWindow";
        auto interop=get_activation_factory<GraphicsCaptureItem,IGraphicsCaptureItemInterop>();
        GraphicsCaptureItem item{nullptr};
        check_hresult(interop->CreateForWindow(owned.hwnd,guid_of<GraphicsCaptureItem>(),put_abi(item)));
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
        // Fixture-only delivery experiment. Not SendInput and not Unity input.
        DWORD owner{};GetWindowThreadProcessId(owned.hwnd,&owner);
        if(owner!=GetCurrentProcessId())throw hresult_error(E_ACCESSDENIED);
        if(!PostMessageW(owned.hwnd,WM_KEYDOWN,'J',1)||!PostMessageW(owned.hwnd,WM_KEYUP,'J',0xC0000001))
            throw hresult_error(HRESULT_FROM_WIN32(GetLastError()));
        session.StartCapture();
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
        auto bytes=static_cast<unsigned char*>(map.pData);
        for(UINT y=0;y<desc.Height;y++)for(UINT x=0;x<desc.Width;x++){
            auto p=bytes+y*map.RowPitch+x*4;
            if(p[0]!=bytes[0]||p[1]!=bytes[1]||p[2]!=bytes[2])varied=true;
            if(p[2]>150&&p[1]<100&&p[0]<100)++reds;
            if(p[0]>150&&p[1]<120&&p[2]<100)++blues;
        }
        context->Unmap(staging.get(),0);frame.Close();session.Close();pool.Close();
        LASTINPUTINFO finalInput{sizeof(LASTINPUTINFO)};
        bool stable=GetForegroundWindow()==previous&&GetLastInputInfo(&finalInput)&&finalInput.dwTime==input.dwTime;
        std::cout<<"FRAME width="<<desc.Width<<" height="<<desc.Height
            <<" nonflat="<<varied<<" red_pixels="<<reds<<" blue_pixels="<<blues
            <<" fixture_keydowns="<<owned_downs<<" fixture_keyups="<<owned_ups
            <<" foreground_unchanged="<<stable<<" game=false global_input=false\n";
        return varied&&stable&&reds>desc.Width*desc.Height/3&&blues>desc.Width*desc.Height/3&&owned_downs==1&&owned_ups==1?0:4;
    } catch(hresult_error const& e) {
        std::cout<<"FAILED phase="<<phase<<" HRESULT=0x"<<std::hex<<static_cast<unsigned long>(e.code())
            <<" foreground_unchanged="<<(GetForegroundWindow()==previous)<<"\n";
        return 1;
    }
}
