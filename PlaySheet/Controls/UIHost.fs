namespace PlaySheet.Controls

open System
open System.Runtime.InteropServices
open System.Windows
open System.Windows.Controls.Primitives
open System.Windows.Interop

[<AutoOpen>]
module private UiHost =
    let HWND_TOPMOST   = nativeint -1
    let HWND_NOTOPMOST = nativeint -2
    let HWND_TOP       = nativeint 0
    let HWND_BOTTOM    = nativeint 1

    let SWP_NOSIZE = 0x0001u
    let SWP_NOMOVE = 0x0002u
    let SWP_NOZORDER = 0x0004u
    let SWP_NOREDRAW = 0x0008u
    let SWP_NOACTIVATE = 0x0010u

    let SWP_FRAMECHANGED = 0x0020u
    let SWP_SHOWWINDOW = 0x0040u
    let SWP_HIDEWINDOW = 0x0080u
    let SWP_NOCOPYBITS = 0x0100u
    let SWP_NOOWNERZORDER = 0x0200u
    let SWP_NOSENDCHANGING = 0x0400u

    let TOPMOST_FLAGS = SWP_NOACTIVATE ||| SWP_NOOWNERZORDER ||| SWP_NOSIZE
                    ||| SWP_NOMOVE ||| SWP_NOREDRAW ||| SWP_NOSENDCHANGING

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type RECT =
        val Left   : int
        val Top    : int
        val Right  : int
        val Bottom : int
    
    [<DllImport("user32.dll")>]
    extern bool GetWindowRect(nativeint hWnd, RECT* lpRect)

    [<DllImport("user32.dll")>]
    extern bool SetWindowPos(nativeint hWnd, nativeint hWndInsertAfter,
                             int X, int Y, int cx, int cy, uint32 uFlags)

type internal UIHost(child: FrameworkElement, parentWindow: Window) as this =
    inherit Window()

    let mutable isTopmost = false


    let setTopmostState(isTop) =
        // https://stackoverflow.com/a/6452940/5117446
        if isTop <> isTopmost then
            match PresentationSource.FromVisual(child) with
            | :? HwndSource as hwndSource ->
                let hwnd = hwndSource.Handle
                let mutable rect = RECT()

                if GetWindowRect(hwnd, &&rect) then
                    let left, top = rect.Left, rect.Top
                    let width, height = this.ActualWidth, this.ActualHeight


                    let inline setWindowPos(after) =
                        SetWindowPos(hwnd, after, left, top, int width, int height, TOPMOST_FLAGS)
                        |> ignore

                    if isTop then
                        setWindowPos HWND_TOPMOST
                    else
                        setWindowPos HWND_BOTTOM
                        setWindowPos HWND_TOP
                        setWindowPos HWND_NOTOPMOST
                
                isTopmost <- isTop

            | _ -> ()


    do
        this.Content <- child
        this.ShowInTaskbar <- false

        let onParentActivated   = EventHandler(fun _ _ -> setTopmostState(true))
        let onParentDeactivated = EventHandler(fun _ _ -> setTopmostState(false))

        this.Loaded.Add(fun _ ->
            parentWindow.Activated.AddHandler(onParentActivated)
            parentWindow.Deactivated.AddHandler(onParentDeactivated)
        )

        this.Unloaded.Add(fun _ ->
            parentWindow.Activated.RemoveHandler(onParentActivated)
            parentWindow.Deactivated.RemoveHandler(onParentDeactivated)
        )

    override __.OnActivated(e) =
        setTopmostState(isTopmost)

        base.OnActivated(e)
