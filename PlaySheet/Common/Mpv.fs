namespace PlaySheet

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open System.Text
open System.Threading

open Microsoft.FSharp.NativeInterop


[<Struct>]
type MpvFormat = None      = 0
               | String    = 1
               | OsdString = 2
               | Flag      = 3
               | Int64     = 4
               | Double    = 5
               | Node      = 6
               | NodeArray = 7
               | NodeMap   = 8
               | ByteArray = 9
[<Struct>]
type MpvLogLevel = None    =  0
                 | Fatal   = 10
                 | Error   = 20
                 | Warn    = 30
                 | Info    = 40
                 | Verbose = 50
                 | Debug   = 60
                 | Trace   = 70


[<AutoOpen>]
module private Native = 

    [<Struct>]
    type MpvEventId = None = 0
                    | Shutdown = 1
                    | LogMsg = 2
                    | GetPropReply = 3
                    | SetPropReply = 4
                    | CmdReply = 5
                    | StartFile = 6
                    | EndFile = 7
                    | FileLoaded = 8
                    | Idle = 11
                    | Tick = 14
                    | ClientMsg = 16
                    | VideoReconfig = 17
                    | AudioReconfig = 18
                    | Seek = 20
                    | PlaybackRestart = 21
                    | PropChange = 22
                    | QueueOverflow = 23
                    | EventHook = 24

    [<Struct>]
    type RenderParamType = Invalid = 0
                         | ApiType = 1
                         | FlipY = 4
                         | Depth = 5
                         | IccProfile = 6
                         | AmbientLight = 7
                         | AdvancedControl = 10
                         | NextFrameInfo = 11
                         | BlockForTargetTime = 12
                         | SkipRendering = 13

    [<Struct>]
    type Event =
        val ID       : MpvEventId
        val Error    : int
        val UserData : uint64
        val Data     : nativeint

        member this.GetData<'T when 'T : unmanaged>() =
            this.Data
            |> NativeInterop.NativePtr.ofNativeInt<'T>
            |> NativeInterop.NativePtr.read
    
    [<Struct>]
    type EndFile =
        val Reason : int
        val Error : int
    
    [<Struct>]
    type LogMessage =
        val Prefix : char nativeptr
        val LevelString : char nativeptr
        val Text : char nativeptr
        val Level : MpvLogLevel
    
    [<Struct>]
    type ClientMessage =
        val ArgsLength : int
        val Args : char nativeptr nativeptr

    [<Struct>]
    type PropEvent =
        val Name : char nativeptr
        val Format : MpvFormat
        val Data : nativeint

    [<Struct>]
    type RenderParam =
        val Type : RenderParamType
        val Data : nativeint
    
    [<Struct>]
    type ByteArray =
        val Data : nativeint
        val Size : nativeint

    [<Struct; RequireQualifiedAccess; StructLayout(LayoutKind.Explicit)>]
    type NodeData =
        [<DefaultValue(false); FieldOffset 0>]
        val mutable String : char nativeptr
        [<DefaultValue(false); FieldOffset 0>]
        val mutable Flag : int
        [<DefaultValue(false); FieldOffset 0>]
        val mutable Int64 : int64
        [<DefaultValue(false); FieldOffset 0>]
        val mutable Double : float
        [<DefaultValue(false); FieldOffset 0>]
        val mutable List : NodeList nativeptr
        [<DefaultValue(false); FieldOffset 0>]
        val mutable BA : ByteArray nativeptr

    and [<Struct>] Node(u: NodeData, f: MpvFormat) =
        member __.F = f
        member __.U = u

    and [<Struct>] NodeList(num: int, values: Node nativeptr, keys: char nativeptr nativeptr) =
        member __.Num = num
        member __.Values = values
        member __.Keys = keys


    [<DllImport("kernel32.dll")>]
    extern nativeint private LoadLibrary(string)

    [<DllImport("kernel32.dll")>]
    extern nativeint private GetProcAddress(nativeint, string)

    let private lib =
        let filename = if Environment.Is64BitProcess then
                           "mpv-64.dll"
                       else
                           "mpv-32.dll"
        
        let path = Assembly.GetExecutingAssembly().Location
                   |> Path.GetDirectoryName

        LoadLibrary <| Path.Combine(path, filename)

    let inline private getfn n =
        if lib = nativeint 0 then
            null
        else
            let addr = GetProcAddress(lib, n)

            if addr = nativeint 0 then
                null
            else
                Marshal.GetDelegateForFunctionPointer(addr)

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type WakeupCallback = delegate of nativeint -> unit
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type UpdateCallback = delegate of nativeint -> unit

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type CreateDelegate  = delegate of unit -> nativeint
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type InitDelegate    = delegate of nativeint -> int
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type DestroyDelegate = delegate of nativeint -> int
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type FreeDelegate    = delegate of nativeint -> unit
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type CommandDelegate = delegate of nativeint * nativeint -> int
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type CommandNodeDelegate = delegate of nativeint * Node byref * Node byref -> int
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type WaitEventDelegate       = delegate of nativeint * double -> Event nativeptr
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type SetOptionDelegate       = delegate of nativeint * byte[] * int * nativeint -> int
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type SetOptionStringDelegate = delegate of nativeint * byte[] * byte[] -> int
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type SetPropertyDelegate     = delegate of nativeint * byte[] * int * byte[] byref -> int
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type GetPropertyDelegate     = delegate of nativeint * byte[] * int * nativeint byref -> int
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type SetWakeupCallbackDelegate = delegate of nativeint * WakeupCallback * nativeint -> unit
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type FreeNodeContentsDelegate  = delegate of Node byref -> unit

    let create : CreateDelegate = getfn "mpv_create"

    let initialize : InitDelegate = getfn "mpv_initialize"

    let terminateDestroy : DestroyDelegate = getfn "mpv_terminate_destroy"

    let free : FreeDelegate = getfn "mpv_free"

    let command : CommandDelegate = getfn "mpv_command"

    let commandNode : CommandNodeDelegate = getfn "mpv_command_node"
    
    let freeNode : FreeNodeContentsDelegate = getfn "mpv_free_node_contents"

    let waitEvent : WaitEventDelegate = getfn "mpv_wait_event"

    let setOption : SetOptionDelegate = getfn "mpv_set_option"

    let setOptionString : SetOptionStringDelegate = getfn "mpv_set_option_string"

    let getProperty : GetPropertyDelegate = getfn "mpv_get_property"

    let setProperty : SetPropertyDelegate = getfn "mpv_set_property"
    


    let private bytesCache = Dictionary()
    let private utf8withoutBom = UTF8Encoding(false)

    let getBytes(s: string) =
        match bytesCache.TryGetValue(s) with
        | true, bytes -> bytes
        | false, _ ->
            use ms = new MemoryStream(s.Length + 5)
            use writer = new StreamWriter(ms, utf8withoutBom, 4096, true)

            writer.Write(s)
            writer.Flush()
            ms.WriteByte(byte 0)

            let buf = ms.ToArray()

            bytesCache.[s] <- buf

            buf

    let allocStrings(s: string array) =
        let numberOfStrings = s.Length + 1
        let pointers    = Array.zeroCreate numberOfStrings
        let rootPointer = Marshal.AllocCoTaskMem(IntPtr.Size)

        for i, str in Array.indexed s do
            let bytes = getBytes str
            let pointer = Marshal.AllocHGlobal(bytes.Length)

            Marshal.Copy(bytes, 0, pointer, bytes.Length)

            pointers.[i] <- pointer
        
        Marshal.Copy(pointers, 0, rootPointer, numberOfStrings)
        pointers, rootPointer
    
    let freeStrings(pointers: nativeint array, root: nativeint) =
        Array.iter Marshal.FreeHGlobal pointers
        Marshal.FreeCoTaskMem root
    
    let readString(p: char nativeptr) =
        NativeInterop.NativePtr.toNativeInt p
        |> Marshal.PtrToStringAuto
    
    let readStringIn(p: char nativeptr nativeptr) i =
        NativeInterop.NativePtr.get p i
        |> NativeInterop.NativePtr.toNativeInt
        |> Marshal.PtrToStringAuto


type MpvProperty = { Name   : string
                   ; Format : MpvFormat
                   ; Data   : nativeint }

type MpvEvent =
    | Shutdown

    | LogMessage       of message: string * level: MpvLogLevel
    | ClientMessage    of args: string[]

    | GetPropertyReply of error: int * userdata: uint64 * data: MpvProperty
    | SetPropertyReply of error: int * userdata: uint64
    | CommandReply     of error: int * userdata: uint64
    | PropertyChange   of userdata: uint64 * data: MpvProperty

    | FileLoaded
    | EndOfFile        of reason: int * error: int

    | Seek
    | Restart

type MpvNode =
    | NodeString    of string: string
    | NodeFlag      of flag: int
    | NodeInt64     of int64: int64
    | NodeDouble    of double: float
    | NodeList      of list: MpvNode[]
    | NodeDict      of pairs: (string * MpvNode)[]
    | NodeByteArray of ba: byte[]

    member __.Format = function
        | NodeString _ -> MpvFormat.String
        | NodeFlag   _ -> MpvFormat.Flag
        | NodeInt64  _ -> MpvFormat.Int64
        | NodeDouble _ -> MpvFormat.Double
        | NodeList   _ -> MpvFormat.NodeArray
        | NodeDict   _ -> MpvFormat.NodeArray
        | NodeByteArray _ -> MpvFormat.ByteArray
    


type Mpv private(ptr: nativeint) =

    static let rec mkManagedNode(node: Node) =
        match node.F with
        | MpvFormat.String -> NodeString <| String node.U.String
        | MpvFormat.Flag   -> NodeFlag node.U.Flag
        | MpvFormat.Int64  -> NodeInt64 node.U.Int64
        | MpvFormat.Double -> NodeDouble node.U.Double

        | MpvFormat.NodeArray ->
            let nodeList = NativePtr.read node.U.List

            if nodeList.Keys = Unchecked.defaultof<_> then
                let nodes = Array.zeroCreate<MpvNode> nodeList.Num

                for i = 0 to nodes.Length - 1 do
                    nodes.[i] <- mkManagedNode <| NativePtr.get nodeList.Values i
                
                NodeList nodes

            else
                let nodes = Array.zeroCreate<string * MpvNode> nodeList.Num

                for i = 0 to nodes.Length - 1 do
                    let key = String(NativePtr.get nodeList.Keys i)
                    let value = mkManagedNode <| NativePtr.get nodeList.Values i

                    nodes.[i] <- key, value
                
                NodeDict nodes
        
        | MpvFormat.ByteArray ->
            let byteArray = NativePtr.read node.U.BA
            let bytes = Array.zeroCreate<byte> <| byteArray.Size.ToInt32()

            Marshal.Copy(byteArray.Data, bytes, 0, bytes.Length)

            NodeByteArray bytes

        | id -> failwithf "Unsupported node %d." (int id)

    let observers = List<IObserver<MpvEvent>>()
    
    let processingThread = new Thread(fun () ->
        while true do
            let event = waitEvent.Invoke(ptr, -1.0)
            let event = NativeInterop.NativePtr.read event

            let inline sendNext(ev) =
                observers |> Seq.iter (fun obs -> obs.OnNext ev)
            let inline sendException(exn) =
                observers |> Seq.iter (fun obs -> obs.OnError exn)

            match event.ID with
            | MpvEventId.None -> ()
           
            | MpvEventId.Idle      -> ()
            | MpvEventId.Tick      -> ()
            | MpvEventId.StartFile -> ()

            | MpvEventId.VideoReconfig -> ()
            | MpvEventId.AudioReconfig -> ()

            | MpvEventId.Shutdown     -> sendNext Shutdown
            
            | MpvEventId.LogMsg       ->
                let msg = event.GetData<LogMessage>()

                LogMessage( (readString msg.Text), msg.Level )
                |> sendNext

            | MpvEventId.ClientMsg    ->
                let msg = event.GetData<ClientMessage>()
                let args = Array.zeroCreate msg.ArgsLength

                for i = 0 to msg.ArgsLength - 1 do
                    args.[i] <- readStringIn msg.Args i

                ClientMessage args
                |> sendNext
            
            | MpvEventId.GetPropReply ->
                let data = event.GetData<PropEvent>()
                let value = { Name = readString data.Name; Format = data.Format; Data = data.Data }

                GetPropertyReply(event.Error, event.UserData, value)
                |> sendNext
            
            | MpvEventId.SetPropReply ->
                SetPropertyReply(event.Error, event.UserData)
                |> sendNext
            
            | MpvEventId.PropChange ->
                let data = event.GetData<PropEvent>()
                let value = { Name = readString data.Name; Format = data.Format; Data = data.Data }

                PropertyChange(event.UserData, value)
                |> sendNext

            | MpvEventId.EndFile ->
                let data = event.GetData<EndFile>()

                EndOfFile(data.Reason, data.Error)
                |> sendNext
            
            | MpvEventId.CmdReply ->
                CommandReply(event.Error, event.UserData)
                |> sendNext

            | MpvEventId.FileLoaded      -> FileLoaded |> sendNext
            | MpvEventId.Seek            -> Seek |> sendNext
            | MpvEventId.PlaybackRestart -> Restart |> sendNext

            | id when (int id) = 9 || (int id) = 19 -> () // Deprecated

            | MpvEventId.QueueOverflow ->
                Exception("Queue overflow.")
                |> sendException
            | _ ->
                Exception(sprintf "Unknown event with ID %d received." <| int event.ID)
                |> sendException
    )

    do processingThread.IsBackground <- true
       processingThread.Start()

    let mutable screenshotter = None 

    static member Yes = "yes"
    static member No = "no"

    /// Creates a new MPV instance.
    static member internal Create() =
        match create with
        | null   -> None
        | create -> Some <| new Mpv(create.Invoke())
    
    member internal __.Initialize() =
        initialize.Invoke(ptr) >= 0


    /// Executes a command.
    member __.Execute([<ParamArray>] args: string array) =
        let pointers, rootPointer = allocStrings(args)
        
        try
            command.Invoke(ptr, rootPointer)
        finally
            freeStrings(pointers, rootPointer)
        |> ignore
    
    /// Executes a command, using a node.
    member __.ExecuteToNode([<ParamArray>] args: string array) =
        let pointers, rootPointer = allocStrings(args)
        let inputNodes = [|
            for p in pointers do
                let mutable nd = Unchecked.defaultof<NodeData>
                nd.String <- NativePtr.ofNativeInt p

                yield Node(nd, MpvFormat.String)
        |]

        use inputNodes = fixed inputNodes
        let mutable inputList = Native.NodeList(args.Length,
                                                inputNodes,
                                                Unchecked.defaultof<_>)
        let inputListPtr = &&inputList

        let mutable nd = Unchecked.defaultof<NodeData>
        nd.List <- inputListPtr

        let mutable inNode = Node(nd, MpvFormat.NodeArray)
        let mutable outNode = Unchecked.defaultof<Node>

        let result =
            try
                commandNode.Invoke(ptr, &inNode, &outNode)
            finally
                freeStrings(pointers, rootPointer)
        
        let res = 
            if result < 0 then
                None
            else
                Some(mkManagedNode outNode)
        
        freeNode.Invoke(&outNode)

        res


    /// Sets the option that has the given name.
    member __.Option(name: string, value: string) =
        setOptionString.Invoke(ptr, getBytes name, getBytes value) |> ignore

    /// Sets the option that has the given name.
    member __.Option(name: string, format: MpvFormat, value: 'T nativeptr) =
        setOption.Invoke(ptr, getBytes name, int format, value |> NativeInterop.NativePtr.toNativeInt) |> ignore


    /// Gets or sets the property that has the given name.
    member __.Item
        with get(name: string) =
            let mutable buf = nativeint 0
            let result = getProperty.Invoke(ptr, getBytes name, int MpvFormat.String, &buf)
            
            if result < 0 then
                null
            else
                let result = Marshal.PtrToStringAnsi(buf)
                free.Invoke(buf)
                result
        
         and set(name: string) (value: string) =
            let mutable bytes = getBytes value

            setProperty.Invoke(ptr, getBytes name, int MpvFormat.String, &bytes) |> ignore
    

    interface IObservable<MpvEvent> with
        /// Subscribes the given observer to the current stream of events.
        member __.Subscribe(observer) =
            observers.Add(observer)

            { new IDisposable with
                member __.Dispose() = observers.Remove(observer) |> ignore }

    interface IDisposable with
        /// Destroys and terminates the underlying Mpv instance.
        member __.Dispose() =
            processingThread.Abort()

            terminateDestroy.Invoke ptr |> ignore

    member private this.CreateScreenshotClient() =
        match Mpv.Create() with
        | None -> None
        | Some client ->
            client.Execute("loadfile", this.["filename"])

            this.Add <| function
                        | FileLoaded -> client.Execute("loadfile", this.["filename"])
                        | _ -> () 
            
            Some client
    
    member private this.ScreenshotClient =
        match screenshotter with
        | Some s -> s
        | None ->
            screenshotter <- this.CreateScreenshotClient()

            match screenshotter with
            | Some s -> s
            | None ->
                failwith "Unable to get screenshot client."
    
    member this.Screenshot(timestamp: float) =
        let client = this.ScreenshotClient
        
        client.["timepos"] <- timestamp.ToString(CultureInfo.InvariantCulture)

        match client.ExecuteToNode("screenshot-raw", "video") with
        | Some (NodeByteArray bytes) -> bytes
        | _ -> failwith ""
