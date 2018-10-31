namespace PlaySheet

open Serilog

[<AutoOpen>]
module Logging = 

    let inline debugf format   = Printf.ksprintf Log.Debug format
    let inline warningf format = Printf.ksprintf Log.Warning format
    let inline errorf format   = Printf.ksprintf Log.Error format
    let inline verbosef format = Printf.ksprintf Log.Verbose format
    let inline infof format    = Printf.ksprintf Log.Information format
