namespace PlaySheet.Helpers

open System
open System.Collections.Generic
open System.Runtime.CompilerServices

type DisposableCollection(disposables: IDisposable seq) =
    let disposables = List(disposables)

    new() = new DisposableCollection(Seq.empty)
    new([<ParamArray>] disposables: IDisposable[]) = new DisposableCollection(disposables)


    static member Of(disposables: IDisposable seq) = new DisposableCollection(disposables)

    member private __.TakeOwnership() =
        let result = disposables.ToArray()
        disposables.Clear()
        result


    member __.Append(disposable: IDisposable) =
        match disposable with
        | :? DisposableCollection as collection ->
            disposables.AddRange(collection.TakeOwnership())
        | _ ->
            disposables.Add(disposable)

    member this.Append([<ParamArray>] disposables: IDisposable[]) =
        disposables |> Array.iter this.Append

    member this.Append(disposables: IDisposable seq) =
        disposables |> Seq.iter this.Append
    
    member this.Append'(disposable: IDisposable) =
        this.Append(disposable)
        this

    member this.Append'([<ParamArray>] disposables: IDisposable[]) =
        this.Append(disposables)
        this

    member this.Append'(disposables: IDisposable seq) =
        this.Append(disposables)
        this


    interface IDisposable with
        member __.Dispose() =
            disposables |> Seq.iter (fun x -> x.Dispose())
            disposables.Clear()

[<Extension>]
type DisposableCollectionExtensions =

    [<Extension>]
    static member ToDisposable<'T when 'T :> IDisposable>(this: IDisposable seq) =
        new DisposableCollection(this)
