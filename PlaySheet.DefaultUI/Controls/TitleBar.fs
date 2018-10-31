namespace PlaySheet.DefaultUI.Controls

open PlaySheet.Extensibility
open PlaySheet.Extensibility.Styles.ButtonStyles
open PlaySheet.Helpers

open System.Windows
open System.Windows.Controls
open System.Windows.Media
open System.Windows.Media.Effects

type TitleBar(player: Player) as this =
    inherit ExtensionControl()

    do
        let window = player.Window

        // Create buttons (shown on the right)
        let buttonsLayout = StackPanel()
        let height = 30.0

        buttonsLayout.FlowDirection <- FlowDirection.RightToLeft
        buttonsLayout.Orientation <- Orientation.Horizontal

        buttonsLayout.Height <- height

        let inline button content =
            Button(Content = content,
                   Style = transparentButtonStyle,
                   Width = height,
                   FontFamily = FontFamily("Segoe UI Symbol"))

        let close    = button ""
        let maximize = button ""
        let pinToTop = button ""
        let minimize = button ""

        buttonsLayout.Children.Add(close)    |> ignore
        buttonsLayout.Children.Add(maximize) |> ignore
        buttonsLayout.Children.Add(pinToTop) |> ignore
        buttonsLayout.Children.Add(minimize) |> ignore

        close.Click.Add <| fun _ ->
            Application.Current.Shutdown()
        
        maximize.Click.Add <| fun _ ->
            window.WindowState <- match window.WindowState with
                                  | WindowState.Maximized -> WindowState.Normal
                                  | _                     -> WindowState.Maximized
        
        minimize.Click.Add <| fun _ ->
            window.WindowState <- WindowState.Minimized
        
        pinToTop.Click.Add <| fun _ ->
            window.Topmost <- not window.Topmost
        
        window.ValueChanged(<@ fun x -> x.Topmost @>) <| fun topmost ->
            pinToTop.Content <- if topmost then "" else ""
        |> ignore

        window.ValueChanged(<@ fun x -> x.WindowState @>) <| fun windowState ->
            maximize.Content <- match windowState with
                                | WindowState.Maximized -> ""
                                | _                     -> ""
        |> ignore

        // Create title (shown on the left)
        let title = TextBlock(Text = player.Title,
                              Foreground = Brushes.White,
                              VerticalAlignment = VerticalAlignment.Center,
                              FontSize = 14.0,
                              Padding = Thickness(10.0, 0.0, 0.0, 0.0))
        
        title.Effect <- DropShadowEffect(ShadowDepth = 4.0,
                                         BlurRadius  = 4.0,
                                         Direction   = 320.0,
                                         Color       = Colors.Black,
                                         Opacity     = 0.3)

        player.Mpv.Add <| function
            | PlaySheet.MpvEvent.FileLoaded ->
                title.Dispatcher.InvokeSafe <| fun () ->
                    title.Text <- player.Title
            | _ -> ()
        
        // Create whole layout
        let layout = Grid()

        layout.ColumnDefinitions.Add(ColumnDefinition())
        layout.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))

        layout.Children.Add(title) |> ignore
        layout.Children.Add(buttonsLayout) |> ignore

        Grid.SetColumn(title, 0)
        Grid.SetColumn(buttonsLayout, 1)

        this.Content <- layout

        DockPanel.SetDock(this, Dock.Top)


    override __.ShouldDisplay(cursorPos, windowSize) =
        // Only display title bar if the cursor is in the top quarter of the window
        cursorPos.Y < (windowSize.Height / 4.0)
    
    override this.OnDisplaying() =
        this.Visibility <- Visibility.Visible

    override this.OnHiding() =
        this.Visibility <- Visibility.Collapsed
