namespace PlaySheet.DefaultUI.Controls

open PlaySheet.Extensibility
open PlaySheet.Extensibility.Styles
open PlaySheet.Helpers

open System
open System.Timers
open System.Windows
open System.Windows.Controls
open System.Windows.Media


type SeekBar(player: Player) as this =
    inherit ExtensionControl()

    static let sliderStyle =
        Style.forType<Slider>
        |> Style.setter <@ fun x -> x.Template @> (
            // See: https://docs.microsoft.com/en-us/dotnet/framework/wpf/controls/slider-styles-and-templates
            let template = ControlTemplate()
            let grid = Grid()

            grid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))
            grid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))
            grid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))

            Binding.ofName "MinHeight"
            |> Binding.withSelfRelativeSource
            |> Binding.set grid.RowDefinitions.[1] <@ fun x -> x.MinHeight @>

            template
        )
        |> Style.build

    let timer = new Timer(500.0, AutoReset = true)
    let bar = Slider()

    do
        this.Content <- bar

        bar.Height <- 20.0
        bar.Background <- Colors.Transparent.AsBrush()

        bar.ValueChanged.Add <| fun e ->
            if e.NewValue <> player.Position then
                player.Position <- e.NewValue

        timer.Elapsed.Add <| fun _ ->
            bar.Dispatcher.InvokeSafe <| fun () ->
                if (not << Double.IsNaN) player.Duration then
                    bar.Maximum <- player.Duration
                    bar.Value <- player.Position

        timer.Start()
        
        let settings = player.Settings

        if settings.AccentColor.IsTransparent then
            bar.Foreground <- Colors.Blue.AsBrush()
        else
            bar.Foreground <- settings.AccentColor.AsBrush()
        
        DockPanel.SetDock(this, Dock.Bottom ||| Dock.Left ||| Dock.Right)

    override __.ShouldDisplay(_, _) =
        // Always display seek bar
        true
    
    override this.OnDisplaying() =
        this.Visibility <- Visibility.Visible

    override this.OnHiding() =
        this.Visibility <- Visibility.Collapsed
    