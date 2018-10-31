namespace PlaySheet.Extensibility.Styles

open System.Windows
open System.Windows.Controls
open System.Windows.Media

module ButtonStyles =
    let transparentButtonStyle =
        Style.forType<Button>
        |> Style.setter <@ fun x -> x.Background      @> (Brushes.Transparent :> _)
        |> Style.setter <@ fun x -> x.Foreground      @> (Brushes.White :> _)
        |> Style.setter <@ fun x -> x.Padding         @> (Thickness 0.0)
        |> Style.setter <@ fun x -> x.BorderThickness @> (Thickness 0.0)
        |> Style.build
