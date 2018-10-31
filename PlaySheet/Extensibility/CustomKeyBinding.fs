namespace PlaySheet.Extensibility

open System
open System.Windows.Input

type ICustomKeyBinding =
    inherit ICommand

    abstract ID   : string
    abstract Name : string
    abstract Description : string

type CustomKeyBinding = { ID          : string
                        ; Name        : string
                        ; Description : string
                        ; Execute     : unit -> unit
                        }
with
    static member private UntriggeredEvent = new Event<EventHandler, EventArgs>()

    interface ICommand with
        member __.CanExecute(_) = true
        [<CLIEvent>]
        member __.CanExecuteChanged = CustomKeyBinding.UntriggeredEvent.Publish

        member this.Execute(_) = this.Execute()
    
    interface ICustomKeyBinding with
        member this.ID = this.ID
        member this.Name = this.Name
        member this.Description = this.Description
