namespace PlaySheet

open PlaySheet.Helpers

open System
open System.IO
open System.Reflection
open System.Windows
open System.Windows.Media

open Nett
open Serilog.Events


/// An observable dictionary.
type private Dict<'K, 'V when 'K : equality> = ObservableDictionary<'K, 'V>

/// Configuration of the logger.
type LoggerConfig = { mutable File    : string
                    ; mutable Console : bool
                    ; mutable Level   : LogEventLevel
                    }


/// Defines all the settings for the application.
type Settings private(path: string, ?dir: string) =
    inherit DependencyObject()

    static do
        DependencyObjectHelpersCache.settingsType <- typeof<Settings>

    let dir = defaultArg dir (Path.GetDirectoryName path)
    let mutable lastSaveTime = DateTime.Now

    static let getTomlConfig(path: string) =
        TomlSettings.Create(fun cfg ->
            let inline configType f =
                cfg.ConfigureType<'a>(fun ct ->
                    ct.CreateInstance(Func<'a> f) |> ignore
                )
                |> ignore

            let inline configDictType mapValue =
                cfg.ConfigureType<Dict<string, 'a>>(fun ct ->
                    ct.CreateInstance(fun () -> Dict()) |> ignore

                    ct.WithConversionFor<TomlTable>(fun conv ->
                        conv.FromToml(fun table ->
                                table
                                |> Seq.map (fun row -> row.Key, row.Value.Get<'a>())
                                |> ObservableDictionary.Of
                            )
                            .ToToml(fun dict table ->
                                for k, v in dict.Unwrap() do
                                    mapValue table k v |> ignore
                            )
                        |> ignore
                    )
                    |> ignore
                )
                |> ignore
 
            configDictType <| fun table k (v: string) -> table.Add(k, v)
            configDictType <| fun table k (v: float) -> table.Add(k, v)

            configType <| fun () -> { File    = null
                                    ; Console = false
                                    ; Level   = LogEventLevel.Fatal
                                    }

            cfg.ConfigureType<Color>(fun ct ->
                ct.CreateInstance(fun () -> Colors.Transparent)
                  .WithConversions(
                    string,
                    (fun x -> ColorConverter.ConvertFromString(x) :?> Color)
                  )
                |> ignore
            )
            |> ignore

            // Ignore what we inherited from DependencyObject
            cfg.ConfigureType<Settings>(fun ct ->
                ct.CreateInstance(fun () -> Settings(path))
                  .IgnoreProperty(fun x -> x.Dispatcher)
                  .IgnoreProperty(fun x -> x.DependencyObjectType)
                  .IgnoreProperty(fun x -> x.IsSealed)
                |> ignore
            )
            |> ignore
        )

    /// Gets the default settings.
    static member internal Default(?path) =
        let path = defaultArg path Settings.DefaultPath
        let dir = Path.GetDirectoryName(path)

        Settings(path, dir,
                 ExtensionsDirectory = Path.Combine(dir, "Extensions")
                )

    /// Gets the default path where settings should be saved.
    static member internal DefaultPath =
        let docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        let storageDir = Path.Combine(docsDir, "PlaySheet")

#if RELEASE
        Path.Combine(storageDir, "Config.toml")
#else
        Path.Combine(storageDir, "DevConfig.toml")
#endif

    /// Loads the settings from the given file.
    static member Load(path: string) =
        Toml.ReadFile<Settings>(path, getTomlConfig path)

    /// Returns whether the settings are currently being saved.
    [<TomlIgnore>]
    member __.LastSaveTime = lastSaveTime

    /// Returns the time spent since we last saved the settings.
    [<TomlIgnore>]
    member __.TimeSinceLastSave = DateTime.Now - lastSaveTime

    /// Saves the settings to the given file.
    member this.Save(?path: string) =
        let path = defaultArg path this.SettingsPath

        Toml.WriteFile(this, path, getTomlConfig path)

        lastSaveTime <- DateTime.Now

    /// Reloads settings from the current file.
    member this.Reload(?path: string) =
        let path = defaultArg path this.SettingsPath
        let newSettings = Toml.ReadFile<Settings>(path, getTomlConfig path)

        // Perform a memberwise copy from the new settings to the current ones.
        // If the settings are equal, no 'PropertyChanged' event should be triggered.
        let registeredProperties = DependencyObjectHelpersCache.settingsProperties

        for dp in registeredProperties do
            this.SetValue(dp, newSettings.GetValue dp)

    /// Clones the settings into a different instance that uses the given path.
    member internal this.Clone(path) =
        let settings = Settings(path)
        let blacklist = [| "path"; "dir" |]
        let bindingFlags = BindingFlags.Instance ||| BindingFlags.NonPublic

        for field in typeof<Settings>.GetFields(bindingFlags) do
            if not <| Array.contains field.Name blacklist then
                field.SetValue(settings, field.GetValue(this))

        settings
    
    /// Returns a path that starts at the directory of the settings file.
    member __.RelativeToSelf([<ParamArray>] path: string[]) =
        match path with
        | [| |] -> dir
        | [| path |] -> if Path.IsPathRooted(path) then
                            path
                        else
                            Path.Combine(dir, path)
        | paths -> Path.Combine(dir, Path.Combine paths)


    /// Gets the path at which the save file is located.
    [<TomlIgnore>]
    member __.SettingsPath = path

    /// Gets the directory in which the save file is located.
    [<TomlIgnore>]
    member __.SettingsDirectory = dir


    /// Dependency property for `ExtensionsDirectory`.
    static member val ExtensionsDirectoryProperty =
        registerDependencyProperty'<Settings, string>
        <| <@ fun x -> x.ExtensionsDirectory @>
        <| null

    /// Gets or sets the directory in which extensions are located.
    member this.ExtensionsDirectory
        with get() = this.GetPropertyValue()
         and set(v) = this.SetPropertyValue(v)


    /// Dependency property for `Logging`.
    static member val LoggingProperty =
        registerDependencyProperty'<Settings, LoggerConfig>
        <| <@ fun x -> x.Logging @>
#if RELEASE
        <| { File = null ; Console = false ; Level = LogEventLevel.Warning }
#else
        <| { File = null ; Console = true ; Level = LogEventLevel.Debug }
#endif
    
    /// Gets or sets the logging configuration.
    member this.Logging
        with get() = this.GetPropertyValue()
         and set(v) = this.SetPropertyValue<LoggerConfig>(v)


    /// Dependency property for `KeyBindings`.
    static member val KeyBindingsProperty =
        registerDependencyProperty'<Settings, Dict<string, string>>
        <| <@ fun x -> x.KeyBindings @>
        <| Dict()

    /// Gets the custom key bindings set by the user.
    member this.KeyBindings
        with get() = this.GetPropertyValue()
         and private set(v) = this.SetPropertyValue<Dict<string, string>>(v)


    /// Dependency property for `TimeStamps`.
    static member val TimeStampsProperty =
        registerDependencyProperty'<Settings, Dict<string, float>>
        <| <@ fun x -> x.TimeStamps @>
        <| Dict()
    
    /// Gets the timestamps of the currently playing items.
    member this.TimeStamps
        with get() = this.GetPropertyValue()
         and private set(v) = this.SetPropertyValue<Dict<string, float>>(v)


    /// Dependency property for `MpvOptions`.
    static member val MpvOptionsProperty =
        registerDependencyProperty'<Settings, Dict<string, string>>
        <| <@ fun x -> x.MpvOptions @>
        <| Dict()

    /// Gets a set of options that are to be passed to MPV.
    member this.MpvOptions
        with get() = this.GetPropertyValue()
         and private set(v) = this.SetPropertyValue<Dict<string, string>>(v)


    /// Dependency property for `IsTouchMode`.
    static member val IsTouchModeEnabledProperty =
        registerDependencyProperty'<Settings, bool>
        <| <@ fun x -> x.IsTouchModeEnabled @>
        <| false

    /// Gets a set of options that are to be passed to MPV.
    member this.IsTouchModeEnabled
        with get() = this.GetPropertyValue()
         and set(v) = this.SetPropertyValue<bool>(v)
    

    /// Dependency property for `AccentColor`.
    static member val AccentColorProperty =
        registerDependencyProperty'<Settings, Color>
        <| <@ fun x -> x.AccentColor @>
        <| Colors.Transparent

    /// Gets or sets the accent color of the application.
    /// 
    /// A transparent value will be converted to a colorful theme.
    member this.AccentColor
        with get() = this.GetPropertyValue()
         and set(v) = this.SetPropertyValue<Color>(v)


    /// Dependency property for `UIHideTimeout`.
    static member val UIHideTimeoutProperty =
        registerDependencyProperty'<Settings, float>
        <| <@ fun x -> x.UIHideTimeout @>
        <| 1.0

    /// Gets or sets the timeout after which the UI should be hidden,
    /// once the cursor stops moving.
    member this.UIHideTimeout
        with get() = this.GetPropertyValue()
         and set(v) = this.SetPropertyValue<float>(v)
