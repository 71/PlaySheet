module Library

open PlaySheet.DefaultUI.Controls
open PlaySheet.Extensibility

open System.Windows

[<InitializeExtension>]
let initialize(player: Player) =
    player.AddControl(SeekBar(player))
    player.AddControl(TitleBar(player))

    player.RegisterKeyBindings [
        { ID          = "core.pause"
        ; Name        = "Pause"
        ; Description = "Pauses the current video."
        ; Execute     = player.Pause
        }

        { ID          = "core.unpause"
        ; Name        = "Play"
        ; Description = "Unpauses the current video."
        ; Execute     = player.Play
        }

        { ID          = "core.togglePlayback"
        ; Name        = "Toggle Playback"
        ; Description = "Toggles media playback."
        ; Execute     = fun () -> player.IsPlaying <- not player.IsPlaying
        }

        { ID          = "core.exit"
        ; Name        = "Exit"
        ; Description = "Exits player."
        ; Execute     = fun () -> Application.Current.Shutdown()
        }
    
        { ID          = "core.enterFullscreen"
        ; Name        = "Enter Fullscreen"
        ; Description = "Enters fullscreen mode."
        ; Execute     = fun () -> player.Window.WindowState <- WindowState.Maximized
        }

        { ID          = "core.leaveFullscreen"
        ; Name        = "Leave Fullscreen"
        ; Description = "Leaves fullscreen mode."
        ; Execute     = fun () -> player.Window.WindowState <- WindowState.Normal
        }

        { ID          = "core.toggleFullscreen"
        ; Name        = "Toggle Fullscreen"
        ; Description = "Toggles fullscreen mode."
        ; Execute     = fun () -> player.Window.WindowState <-
                                    match player.Window.WindowState with
                                    | WindowState.Maximized -> WindowState.Normal
                                    | _                     -> WindowState.Maximized
        }
    ]
