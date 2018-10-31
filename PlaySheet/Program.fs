module internal Program

open PlaySheet
open PlaySheet.Extensibility
open PlaySheet.Helpers
open PlaySheet.Windows

open System
open System.IO
open System.Reflection
open System.Windows

open Serilog


/// Defines the application.
type App() = inherit Application()


/// Defines the command line parameters.
type Args = { ConfigPath : string option
            ; FileToOpen : string option
            ; Extensions : string list
            ; MpvOptions : (string * string) list
            }
with
    static member Default = { ConfigPath = None
                            ; FileToOpen = None
                            ; Extensions = []
                            ; MpvOptions = []
                            }

let inline (|StartWith|_|) (start: string) (str: string) =
    if str.StartsWith(start) then
        Some(str.Substring start.Length)
    else
        None


let usage = """
Modern and extensible video player based on MPV.

USAGE:
    playsheet.exe [OPTIONS] <file>

OPTIONS:
    -c, --config    <PATH>    Specify path to the configuration file.
    -e, --extension <EXT...>  Specify path to one or more extensions files.
    -V, --version             Print version and exit.
    -h, --help                Print this message and exit.

    --mpv:<key> <value>       Specify option to pass to MPV.

"""

/// Print the usage string.
let printUsage status =
    Console.Write(usage)

    exit status


/// Entry point of the player, which parses arguments, loads settings,
/// loads extensions, and starts up the app.
[<EntryPoint; STAThread>]
let main args =
    // Parse arguments
    let rec parseArgs = function

    // No argument
    | [] -> Args.Default

    // One argument (command line option)
    | ("-V" | "--version")::_ ->
        printfn "PlaySheet v%A" (Assembly.GetExecutingAssembly().GetName().Version)

        exit 0

    | ("-h" | "--help")::_ -> printUsage 0

    // One argument remaining: file to open
    | [file] -> { Args.Default with FileToOpen = Some file }

    // Pairs of two arguments
    | ("-c" | "--config")::config::rest ->
        let args = parseArgs rest

        match args.ConfigPath with
        | Some _ -> invalidArg "args" "Only one configuration file can be provided."
        | None -> { args with ConfigPath = Some config }

    | ("-e" | "--extension")::ext::rest ->
        let args = parseArgs rest in { args with Extensions = ext::args.Extensions }

    | (StartWith "--mpv:" opt)::optValue::rest ->
        let rest, value = if optValue.StartsWith("-") then
                            optValue::rest, Mpv.Yes
                          else
                            rest, optValue

        let args = parseArgs rest in { args with MpvOptions = (opt, value)::args.MpvOptions }

    // Unknown
    | _ -> printUsage 1

    let args = args |> List.ofArray |> parseArgs


    // Load settings
    let settingsPath = defaultArg args.ConfigPath Settings.DefaultPath
    let settings =
        if File.Exists(settingsPath) then
            Settings.Load(settingsPath)
        else
            Settings.Default(settingsPath)

    // Watch for settings changes
    use settingsDirWatcher = new FileSystemWatcher(settings.SettingsDirectory)

    settingsDirWatcher.Changed.Add <| fun e ->
        match e.ChangeType with
        | WatcherChangeTypes.Changed when e.FullPath = settings.SettingsPath
                                       && settings.TimeSinceLastSave.TotalSeconds > 2.0 ->
            infof "Settings changed, reloading..."

            settings.Dispatcher.InvokeSafe(settings.Reload)
        
        | _ -> ()

    settingsDirWatcher.EnableRaisingEvents <- true


    // Initialize mpv
    let mpv = match Mpv.Create() with
              | Some mpv -> mpv
              | None -> eprintfn "Unable to load MPV."
                        exit 1
    
    for name, value in Seq.append (settings.MpvOptions.Unwrap()) args.MpvOptions do
        mpv.Option(name, value)

    if not <| mpv.Initialize() then
        eprintfn "Unable to initialize MPV."
        exit 1
    

    // Initialize logger
    let logger = LoggerConfiguration()
                    .MinimumLevel.Is(settings.Logging.Level)

    if settings.Logging.Console then
        logger.WriteTo.Console() |> ignore
    
    match settings.Logging.File with
    | null -> ()
    | logFile ->
        if File.Exists logFile then
            File.Delete logFile

        logger.WriteTo.File(logFile) |> ignore
    
    Log.Logger <- logger.CreateLogger()


    // Initialize player
    let mutable player = Unchecked.defaultof<Player>

    let app = App()
    let mainWindow = MainWindow(mpv, settings, &player)

    mainWindow.Deactivated
        |> Observable.filter (fun _ -> settings.TimeSinceLastSave.TotalSeconds > 20.0)
        |> Observable.add (fun _ -> settings.Save())


    // Load extensions
    let extensionsDirectory = settings.ExtensionsDirectory

    if not <| Directory.Exists(extensionsDirectory) then
        Directory.CreateDirectory(extensionsDirectory) |> ignore

    let assemblies = seq {
        for assemblyPath in args.Extensions do
            yield Assembly.LoadFile(assemblyPath)

        for assemblyPath in Directory.EnumerateFiles(extensionsDirectory, "*.dll") do
            yield Assembly.LoadFile(assemblyPath)
    }

    let initializationMethods = seq {
        for assembly in assemblies do
        for modl     in assembly.GetExportedTypes() do
        for method   in modl.GetMethods(BindingFlags.Static ||| BindingFlags.Public) do
            let attr = method.GetCustomAttribute<InitializeExtensionAttribute>(false)

            if not <| obj.ReferenceEquals(attr, null) then
                let parameters = method.GetParameters()

                if parameters.Length <> 1 then
                    errorf "Unable to load %A: only one parameter must be provided." method
                elif parameters.[0].ParameterType <> typeof<Player> then
                    errorf "Unable to load %A: the parameter must be of type Player." method
                elif not method.IsStatic then
                    errorf "Unable to load %A: the method must be static." method
                elif typeof<IDisposable>.IsAssignableFrom(method.ReturnType) then
                    yield attr, method
                elif typeof<Void> = method.ReturnType then
                    yield attr, method
                else
                    errorf "Unable to load %A: invalid return type." method
    }

    // Initialize extensions
    infof "Initializing extensions..."

    use disposables = new DisposableCollection()

    let initArgs = Array.singleton <| box player

    for _, method in initializationMethods do
        match method.Invoke(null, initArgs) with
        | :? IDisposable as disposable -> disposables.Append(disposable)
        | _ -> ()


    // Run application
    infof "Running application..."

    app.Run(mainWindow)
