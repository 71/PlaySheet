namespace PlaySheet.Extensibility.Styles

open System.Windows

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open System.Reflection
open PlaySheet.Helpers

type StyleBuilder<'T>() =
    member val Style = Style(typeof<'T>)

module Style =

    let inline empty<'a> = StyleBuilder<'a>()
    let inline forType<'a> = StyleBuilder<'a>()

    let inline apply f (style: StyleBuilder<'a>) =
        f style.Style
        style

    let inline build (style: StyleBuilder<'a>) =
        style.Style

    let inline basedOn typ =
        apply(fun s -> s.BasedOn <- typ)
    
    let inline setter (propGetter: Expr<'a -> 'b>) (value: 'b) (style: StyleBuilder<'a>) =
        let prop = match propGetter with
                   | Lambda (_, PropertyGet (_, prop, _)) -> prop
                   | _ -> invalidArg "propGetter" "Not a property getter."
        
        let prop = DependencyObjectHelpers.getPropertyFromName(prop.DeclaringType, prop.Name)

        apply(fun s -> s.Setters.Add(Setter(prop, value))) style
