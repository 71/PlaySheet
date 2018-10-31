namespace PlaySheet.Extensibility

open PlaySheet.Helpers

open System.Windows
open System.Windows.Controls


/// Represents a control added to the overlay by an extension.
/// 
/// Such controls can be shown and hidden depending on user interactions.
[<AbstractClass>]
type ExtensionControl() =
    inherit UserControl()

    /// Dependency property for `IsDisplayed`.
    static member val IsDisplayedProperty =
        registerDependencyProperty<ExtensionControl, bool>
        <| <@ fun x -> x.IsDisplayed @>
        <| false
        <| fun this value ->
            if value then
                this.OnDisplaying()
            else
                this.OnHiding()

    /// Gets or sets whether the control is displayed on the screen.
    /// 
    /// A control may be displayed without
    member this.IsDisplayed
        with get() = this.GetPropertyValue()
         and set(v) = this.SetPropertyValue<bool>(v)

    /// Shows the control.
    member this.Show() = this.IsDisplayed <- true

    /// Hides the control.
    member this.Hide() = this.IsDisplayed <- false

    /// Indicates that the control is being displayed.
    abstract member OnDisplaying : unit -> unit

    /// Indicated that the control is no longer being displayed.
    abstract member OnHiding : unit -> unit

    override __.OnDisplaying() = ()

    override __.OnHiding() = ()

    /// Returns whether this element should be displayed, given the
    /// event that triggered this query and the size of the window.
    abstract member ShouldDisplay : custorPos: Point * windowSize: Size -> bool
