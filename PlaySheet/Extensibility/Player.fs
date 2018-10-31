namespace PlaySheet.Extensibility

open PlaySheet
open PlaySheet.Helpers

open System
open System.Collections.Generic
open System.Globalization
open System.Timers
open System.Windows
open System.Windows.Controls
open System.Windows.Input


type Player internal(mainWindow: Window, mpv: Mpv, canvas: Canvas, settings: Settings) as this =
    let overlay = DockPanel(HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            LastChildFill = false)

    let customKeyBindings = List<ICustomKeyBinding>()
    let applyKeyBindings(customKeyBindings: ICustomKeyBinding seq) =
        settings.KeyBindings
        |> Seq.choose (fun pair ->
            customKeyBindings |> Seq.tryFind(fun x -> x.ID = pair.Value)
            |> Option.map (fun x -> pair.Key, x)
           )
        |> Seq.iter (fun (key, customKeyBinding) ->
            match Enum.TryParse<Key>(key, true) with
            | true, key ->
                mainWindow.InputBindings.Add(KeyBinding(Command = customKeyBinding, Key = key))
                |> ignore
            | false, _  ->
                match parseInputGesture key with
                | Some gesture ->
                    mainWindow.InputBindings.Add(InputBinding(customKeyBinding, gesture))
                    |> ignore
                | None -> errorf "Invalid key binding %s." key
           )

    do
        canvas.Children.Add(overlay) |> ignore

        canvas.SizeChanged.Add <| fun e ->
            overlay.Width <- e.NewSize.Width
            overlay.Height <- e.NewSize.Height

        // Automatically reload key bindings when settings change:
        settings.ValueChanged(<@ fun x -> x.KeyBindings @>) <| fun _ ->
            infof "Reloading key bindings..."

            mainWindow.InputBindings.Clear()
            applyKeyBindings(customKeyBindings)
        |> ignore

        (mpv :> IObservable<_>).Subscribe { new IObserver<_> with
            member __.OnCompleted() = () // Should not happen

            member __.OnError(exn) =
                // Log error
                warningf "%A" exn

            member __.OnNext(ev) =
                // Process some events
                match ev with
                | FileLoaded ->
                    settings.Dispatcher.InvokeSafe(fun _ ->
                        match (settings.TimeStamps :> IDictionary<_, _>).TryGetValue(this.Path) with
                        | true, timestamp -> this.Position <- timestamp
                        | false, _ -> ()
                    )
                | _ -> ()
        }
        |> ignore

        // Update timestamp every 5 seconds
        let timer = new Timer(5000.0)

        timer.Elapsed.Add <| fun _ ->
            let pos = this.Position

            if (not << Double.IsNaN) pos then
                settings.Dispatcher.InvokeSafe(fun _ ->
                    (settings.TimeStamps :> IDictionary<_, _>).[this.Path] <- pos
                )

        timer.Start()
        Application.Current.Exit.Add <| fun _ -> timer.Stop()


    /// Gets the underlying `Mpv` instance.
    member __.Mpv = mpv

    /// Gets the settings of the player.
    member __.Settings = settings

    /// Gets the window of the player.
    member __.Window = mainWindow

    /// Registers the given key bindings.
    member __.RegisterKeyBindings(keyBindings: ICustomKeyBinding seq) =
        applyKeyBindings(keyBindings)
        customKeyBindings.AddRange(keyBindings)
        
        { new IDisposable with
            member __.Dispose() =
                for keyBinding in keyBindings do
                    // Remove from list of user-defined bindings
                    customKeyBindings.Remove(keyBinding) |> ignore

                    // Remove all created input bindings
                    let inputBindings = mainWindow.InputBindings
                    let mutable i = 0

                    while i < inputBindings.Count do
                        if LanguagePrimitives.PhysicalEquality (box inputBindings.[i].Command) (box keyBinding) then
                            inputBindings.RemoveAt(i)
                        else
                            i <- i + 1
        }


    /// Shows all controls, depending on the position of the cursor.
    member val internal ShowControls = new Event<unit>()

    /// Hides all controls.
    member val internal HideControls = new Event<unit>()

    /// Adds a control to the overlay.
    member __.AddControl(control: ExtensionControl) =
        let setupControl() =
            if control.ShouldDisplay(Mouse.GetPosition mainWindow, mainWindow.RenderSize) then
                control.Show()
            else
                control.Hide()

        overlay.Children.Add(control) |> ignore

        this.ShowControls.Publish.Add <| fun () ->
            if control.ShouldDisplay(Mouse.GetPosition mainWindow, mainWindow.RenderSize) then
                control.Show()
            else
                control.Hide()
        
        this.HideControls.Publish.Add <| fun () ->
            control.Hide()

        if mainWindow.IsLoaded then
            setupControl()
        else
            mainWindow.Loaded.Once <| fun _ ->
                setupControl()



    /// Plays (or unpauses) the current media.
    member __.Play() = mpv.["pause"] <- Mpv.No

    /// Pauses the current media.
    member __.Pause() = mpv.["pause"] <- Mpv.Yes

    /// Gets the filename of the currently playing media.
    member __.Filename = mpv.["filename"]

    /// Gets the full path of the currently playing media.
    member __.Path = mpv.["path"]
    
    /// Gets the title of the currently playing media.
    member __.Title = mpv.["media-title"]

    /// Gets whether the player is currently seeking.
    member __.IsSeeking = mpv.["seeking"] = Mpv.Yes

    /// Gets whether the player has reached the end of its input.
    member __.IsEndReached = mpv.["eof-reached"] = Mpv.Yes
    
    /// Gets the full duration of the currently playing media.
    member __.Duration = match Double.TryParse mpv.["duration"] with
                         | true, dur -> dur
                         | false, _ -> nan

    /// Gets or sets the current position in the stream, in seconds.
    member __.Position with get() = match Double.TryParse mpv.["time-pos"] with
                                    | true, pos -> pos
                                    | false, _ -> nan
                        and set(v: double) =
                            let time = v.ToString(CultureInfo.InvariantCulture)
                            mpv.["time-pos"] <- time

    /// Seeks to the given time in seconds, relative to the current position.
    member __.Seek(offset: double) =
        mpv.Execute("seek", offset.ToString(CultureInfo.InvariantCulture), "relative")
    
    /// Gets or sets whether the media is playing.
    member __.IsPlaying with get()  = mpv.["pause"] = Mpv.No
                         and set(v) = mpv.["pause"] <- if v then Mpv.No else Mpv.Yes
    
    /// Gets or sets whether the media is paused.
    member __.IsPaused with get()  = mpv.["pause"] = Mpv.Yes
                        and set(v) = mpv.["pause"] <- if v then Mpv.Yes else Mpv.No

    /// Load a file, given its path.
    member __.Load(media: string) =
        mpv.Execute("loadfile", media)
        mainWindow.Title <- media
