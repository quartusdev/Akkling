namespace Akkling

[<RequireQualifiedAccess; ReferenceEquality>]
type Behavior<'Message> =
     | Extensible of OnMessage : (Actor<'Message> -> 'Message -> Behavior<'Message>) * OnSignal : (Actor<'Message> -> obj-> Behavior<'Message>)
     | ExtensibleAsync of OnMessage : (Actor<'Message> -> 'Message -> Async<Behavior<'Message>>) * OnSignal : (Actor<'Message> -> obj-> Async<Behavior<'Message>>)
     | Deffered of Factory : (Actor<'Message> -> Behavior<'Message>)
     //| DefferedAsync of OnMessage : (Actor<'Message> -> Async<Behavior<'Message>>)
     | Unhandled
     | Same
     | Stopped

module Behaviors =
    let rec canonicalize ctx current behavior =
        match behavior with
        | Behavior.Same -> current
        | Behavior.Unhandled -> current
        | Behavior.Deffered factory -> factory ctx |> canonicalize ctx current
        | _ -> behavior

    let rec undefer ctx behavior =
        match behavior with
        | Behavior.Deffered factory -> factory ctx |> undefer ctx
        | _ -> behavior

    let validateInitial behavior =
        match behavior with
        | Behavior.Same | Behavior.Unhandled -> false
        | _ -> true

    let isUnhandled behavior = 
        behavior = Behavior.Unhandled

    let echo =
        Behavior.Extensible (
            (fun ctx msg -> 
                ctx.Sender() <! msg
                Behavior.Same), 
            (fun ctx signal -> 
                ctx.Sender() <! signal
                Behavior.Same))

module Actor =
    let private emptyHandler _ _ = Behavior.Unhandled
    let private emptyHandlerAsync _ _  = async { return Behavior.Unhandled }
    let private ignoreHandler _ _ = Behavior.Same

    let immutable<'Message> onMessage = 
        Behavior<'Message>.Extensible (onMessage, emptyHandler)

    let immutableAsync<'Message> onMessage =
        Behavior<'Message>.ExtensibleAsync (onMessage, emptyHandlerAsync)

    let deffered<'Message> factory =
        Behavior<'Message>.Deffered factory

    let onSignal onSignal behavior =
        match behavior with
        | Behavior.Extensible (onMessage, _) ->
            Behavior.Extensible (onMessage, onSignal)
        | _ -> invalidArg "behavior" (sprintf "cannot override signal handler for behvaior %A" behavior)

    let same = Behavior.Same

    let unhandled = Behavior.Unhandled

    let stopped = Behavior.Stopped

    let empty<'Message> = immutable<'Message> emptyHandler

    let ignore<'Message> = immutable<'Message> ignoreHandler

            


