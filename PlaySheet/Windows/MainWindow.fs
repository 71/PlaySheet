namespace PlaySheet.Windows

open PlaySheet
open PlaySheet.Controls
open PlaySheet.Extensibility
open PlaySheet.Helpers

open System.Windows
open System.Windows.Controls
open System.Windows.Input
open System.Windows.Media
open System.Windows.Shell
open System.Timers
open System.Windows.Threading


type internal MainWindow(mpv: Mpv, settings: Settings, playerRef: Player byref) as this =
    inherit Window()

    let playerHost = new MpvHost()
    
    let canvas  = Canvas()
    let overlay = UIHost(canvas, this)
    let player  = Player(this, mpv, canvas, settings)

    do
        playerRef <- player

        // Style window
        this.WindowStartupLocation <- WindowStartupLocation.CenterScreen
        this.WindowStyle        <- WindowStyle.None
        this.AllowsTransparency <- true
        this.Background         <- SolidColorBrush(Colors.Black)

        let chrome = WindowChrome(CaptionHeight = 0.0,
                                  ResizeBorderThickness = Thickness 5.0)

        WindowChrome.SetWindowChrome(this, chrome)


        // Set up content... Order matters /!\
        overlay.WindowStyle        <- WindowStyle.None
        overlay.AllowsTransparency <- true
        overlay.Background         <- SolidColorBrush(Colors.Transparent)

        overlay.HorizontalContentAlignment <- HorizontalAlignment.Stretch
        overlay.VerticalContentAlignment   <- VerticalAlignment.Stretch

        this.Loaded.Add <| fun _ ->
            overlay.Show()
            overlay.Owner <- this

        this.Content <- playerHost
        this.HorizontalContentAlignment <- HorizontalAlignment.Stretch
        this.VerticalContentAlignment   <- VerticalAlignment.Stretch


        // Initalize player correctly
        this.Loaded.Once <| fun _ ->
            let mutable wid = playerHost.Handle

            mpv.Option("wid", MpvFormat.Int64, &&wid)


        // Resize items on movement
        this.SizeChanged.Add <| fun _ ->
            playerHost.Width  <- this.ActualWidth
            playerHost.Height <- this.ActualHeight
            overlay.Width  <- this.ActualWidth
            overlay.Height <- this.ActualHeight
        
        this.LocationChanged.Add <| fun _ ->
            overlay.Left <- this.Left
            overlay.Top <- this.Top


        // Drag & drop
        this.AllowDrop <- true

        this.DragEnter.Add <| fun e ->
            if e.Data.GetDataPresent(DataFormats.FileDrop) then
                let files = e.Data.GetData(DataFormats.FileDrop) :?> string[]

                if files.Length = 1 then
                    e.Effects <- DragDropEffects.Move

        this.Drop.Add <| fun e ->
            let files = e.Data.GetData(DataFormats.FileDrop) :?> string[]
            let file = files.[0]

            player.Load(file)


        // Move window when mouse down
        this.MouseDown.Add <| fun e ->
            if e.LeftButton = MouseButtonState.Pressed then
                this.DragMove()


        // Enter / quit fullscreen on double click
        this.MouseDoubleClick.Add <| fun e ->
            this.WindowState <- 
                match this.WindowState with
                | WindowState.Normal -> WindowState.Maximized
                | _                  -> WindowState.Normal

        this.StateChanged.Add <| fun _ ->
            overlay.WindowState <- this.WindowState

            if this.WindowState <> WindowState.Minimized then
                this.Focus() |> ignore


    do
        // Hide cursor and UI after a few seconds
        let mutable isUiHidden = false
        let timer = new Timer(AutoReset = false)

        let hideUi() =
            Mouse.OverrideCursor <- Cursors.None
            player.HideControls.Trigger()

            isUiHidden <- true
        
        let showUi() =
            Mouse.OverrideCursor <- null
            player.ShowControls.Trigger()

            isUiHidden <- false
        
        timer.Elapsed.Add <| fun _ ->
            this.Dispatcher.InvokeSafe hideUi
        
        this.MouseMove.Add <| fun _ ->
            if isUiHidden then
                showUi()
            else
                timer.Stop()
                timer.Start()

                player.ShowControls.Trigger()

        this.MouseLeave.Add <| fun e ->
            // Sometimes it says the mouse leaves, even though it's only over
            // another UI element
            if not isUiHidden && isNull e.MouseDevice.DirectlyOver then
                hideUi()
        
        timer.Interval <- settings.UIHideTimeout * 1000.0

        settings.ValueChanged(<@ fun x -> x.UIHideTimeout @>) <| fun timeout ->
            timer.Interval <- timeout * 1000.0
        |> ignore
