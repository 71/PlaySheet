namespace PlaySheet.Extensibility

open System.Windows.Controls.Primitives


/// Represents a popup that can be shown by an extension.
[<AbstractClass>]
type ExtensionPopup() =
    inherit Popup()
