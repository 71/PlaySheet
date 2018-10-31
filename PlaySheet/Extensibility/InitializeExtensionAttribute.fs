namespace PlaySheet.Extensibility

open System

/// Indicates that this function should be invoked when the extension
/// is loaded, in order to inilialize it.
/// 
/// The method must have a signature of `Player -> unit`.
[<AttributeUsage(AttributeTargets.Method, AllowMultiple = false)>]
type InitializeExtensionAttribute() =
    inherit Attribute()
