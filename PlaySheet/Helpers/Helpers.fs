namespace PlaySheet.Helpers

open System.Collections.Generic
open System.Runtime.CompilerServices

open Microsoft.FSharp.Quotations.Patterns

open Nett

[<AutoOpen>]
module Helpers =
    let internal getModuleType = function
    | PropertyGet (_, prop, _) -> prop.DeclaringType
    | _ -> invalidArg "arg" "Expression must be a property getter."

[<Extension>]
type Extensions =

    [<Extension>]
    static member Unwrap<'K, 'V>(this: IEnumerable<KeyValuePair<'K, 'V>>) =
        this
        |> Seq.map (fun pair -> pair.Key, pair.Value)

    [<Extension>]
    static member Unwrap<'K, 'V>(this: KeyValuePair<'K, 'V>) =
        this.Key, this.Value
    
    [<Extension>]
    static member inline WithConversions(this: TomlSettings.ITypeSettingsBuilder< 'a >,
                                         serialize: 'a -> string,
                                         deserialize: string -> 'a ) =
        this.WithConversionFor<TomlString>(fun convert ->
            convert.FromToml(fun x -> deserialize x.Value)
                   .ToToml(serialize)
            |> ignore)

    [<Extension>]
    static member inline WithConversions(this: TomlSettings.ITypeSettingsBuilder< 'a >,
                                         serialize: 'a -> float,
                                         deserialize: float -> 'a ) =
        this.WithConversionFor<TomlFloat>(fun convert ->
            convert.FromToml(fun x -> deserialize x.Value)
                   .ToToml(serialize)
            |> ignore)
