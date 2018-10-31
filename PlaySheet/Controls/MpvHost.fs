namespace PlaySheet.Controls

open System.Runtime.InteropServices
open System.Windows.Interop

[<AutoOpen>]
module private MpvHost =
    let WS_CHILD   = 0x40000000
    let WS_VISIBLE = 0x10000000
    let LBS_NOTIFY = 0x00000001
    let HOST_ID    = 0x00000002
    let LISTBOX_ID = 0x00000001
    let WS_VSCROLL = 0x00200000
    let WS_BORDER  = 0x00800000
    let WS_EX_TRANSPARENT = 0x00000020
    let GWL_EXSTYLE = -20


    [<DllImport("user32.dll")>]
    extern nativeint CreateWindowEx(int dwExStyle,
                                    string lpszClassName,
                                    string lpszWindowName,
                                    int style,
                                    int x, int y,
                                    int width, int height,
                                    nativeint hwndParent,
                                    nativeint hMenu,
                                    nativeint hInst,
                                    [<MarshalAs(UnmanagedType.AsAny)>] obj pvParam)
    
    [<DllImport("user32.dll")>]
    extern bool DestroyWindow(nativeint hwnd)

    [<DllImport("user32.dll")>]
    extern int GetWindowLong(nativeint hWnd, int nIndex)
    [<DllImport("user32.dll")>]
    extern int SetWindowLong(nativeint hWnd, int nIndex, int dwNewLong)

type internal MpvHost() =
    inherit HwndHost()

    override this.BuildWindowCore(hwndParent) =
        let handle = CreateWindowEx(0, "static", "", WS_CHILD ||| WS_VISIBLE,
                                    0, 0, 0, 0,
                                    hwndParent.Handle, nativeint HOST_ID,
                                    nativeint 0, null)

        let exStyle = GetWindowLong(handle, GWL_EXSTYLE)
        let newStyle = exStyle ||| WS_EX_TRANSPARENT

        SetWindowLong(handle, GWL_EXSTYLE, newStyle) |> ignore

        HandleRef(this, handle)

    override __.DestroyWindowCore(hwnd) =
        DestroyWindow(hwnd.Handle) |> ignore

    override __.WndProc(_, _, _, _, handled) =
        handled <- false
        nativeint 0
