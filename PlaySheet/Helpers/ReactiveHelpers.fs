namespace PlaySheet.Helpers

open System
open System.Runtime.CompilerServices

[<Extension>]
type ReactiveExtensions =

    [<Extension>]
    static member inline Once<'T>(this: IObservable<'T>, handler: 'T -> unit) =
        let mutable disposeAfter = false
        let mutable disposable : IDisposable = null

        disposable <- this.Subscribe(fun v ->
            handler(v)

            match disposable with
            | null -> disposeAfter <- true
            | disp -> disp.Dispose()
        )
        
        if disposeAfter then
            disposable.Dispose()
