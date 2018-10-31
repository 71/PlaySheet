namespace PlaySheet.Helpers

open System.Windows
open System.Windows.Data
open System.Windows.Interop
open System.Windows.Media
open System.Windows.Threading

open Microsoft.FSharp.Quotations


module Binding =
    let empty = Binding()
    let ofName name = Binding(name)

    let update f (binding: Binding) = f binding ; binding

    let withRelativeSource relativeSource =
        update (fun x -> x.RelativeSource <- relativeSource)
    
    let withSelfRelativeSource binding =
        withRelativeSource (RelativeSource RelativeSourceMode.Self) binding
    let withTemplatedParentRelativeSource binding =
        withRelativeSource (RelativeSource RelativeSourceMode.TemplatedParent) binding

    let set (element: 'a) (propGetter: Expr<'a -> 'b>) (binding: Binding) =
        let prop = DependencyObjectHelpers.getPropertyFromGetter propGetter

        BindingOperations.SetBinding(element, prop, binding)
        |> ignore


[<AutoOpen>]
module ControlsHelpers =

    type Window with
        member this.Handle = WindowInteropHelper(this).Handle
    
    type Color with
        member this.IsTransparent = this.A = 0uy

        member this.AsBrush() = SolidColorBrush(this)
    
    type Dispatcher with
        member this.InvokeSafe(f: unit -> unit) =
            this.BeginInvoke(System.Action<unit> f :> System.Delegate, ())
            |> ignore // Operation may have failed, but we don't really care
