namespace PlaySheet.Helpers

open System.Windows.Input

[<AutoOpen>]
module internal InputGestureParser =

    let parseInputGesture(str: string) : InputGesture option =
        try Some (KeyGestureConverter().ConvertFromString(str) :?> InputGesture)
        with _ ->
            try Some (MouseGestureConverter().ConvertFromString(str) :?> InputGesture)
            with _ -> None
