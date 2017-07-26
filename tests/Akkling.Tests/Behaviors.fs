module Akkling.Tests.Behavior

open Xunit

open Akkling
open Akkling.TestKit

type Event =
    | Pong
    | Started

type Command =
    | Ping

[<Fact>]
let ``must stop when exception from deffered factory`` () = testDefault <| fun tck ->
    let p = probe tck

    let behavior = 
        Actor.deffered<Command>
        <| fun ctx ->
            let childBehavior =
                Actor.deffered<Command> 
                <| fun _ ->
                typed p.Ref <! Started
                failwith "stop this actor with an exception"
            let child = spawnAnonymous ctx <| props childBehavior
        
            ctx.Watch(untyped child) |> ignore

            Actor.immutable<Command> (fun ctx msg -> Actor.same)
            |> Actor.onSignal (fun ctx signal ->
                match signal with
                | Terminated(ref, _, _) when ref = child ->
                    typed p.Ref <! Pong
                    Actor.stopped
                | _ -> Actor.unhandled) 
    
    let actor = spawnAnonymous tck <| props(behavior)

    p.ExpectMsg(Started) |> ignore
    p.ExpectMsg(Pong) |> ignore

[<Fact>]
let ``must stop when result of deffered is Behavior.Stopped`` () = testDefault <| fun tck ->
    let p = probe tck

    let behavior = 
        Actor.deffered<Command>
        <| fun ctx ->
            let childBehavior =
                Actor.deffered<Command> 
                <| fun _ -> Actor.stopped
            let child = spawnAnonymous ctx <| props childBehavior
        
            ctx.Watch(untyped child) |> ignore

            Actor.immutable<Command> (fun ctx msg -> Actor.same)
            |> Actor.onSignal (fun ctx signal ->
                match signal with
                | Terminated(ref, _, _) when ref = child ->
                    typed p.Ref <! Pong
                    Actor.stopped
                | _ -> Actor.unhandled)          

    let actor = spawnAnonymous tck <| props(behavior)

    p.ExpectMsg(Pong) |> ignore

[<Fact>]
let ``must create underlying when nested in deffered`` () = testDefault <| fun tck ->
    let p = probe tck

    let behavior = 
        Actor.deffered<Command>
        <| fun ctx ->
            (typed p) <! Started

            Actor.immutable<Command> 
            <| fun ctx msg ->
                (typed p) <! Pong
                Actor.same

    let actor = spawnAnonymous tck <| props(behavior)

    p.ExpectMsg(Started) |> ignore

[<Fact>]
let ``must execute async behavior`` () = testDefault <| fun tck ->
    let p = probe tck

    let behavior = 
        Actor.immutableAsync<Command>
        <| fun ctx msg ->
            async {
                let next =
                    match msg with
                    | Ping -> 
                        (typed p) <! Started
                        
                        Actor.deffered
                        <| fun ctx ->
                            (typed p) <! Pong
                            Actor.ignore

                return next
            }

    let actor = spawnAnonymous tck <| props(behavior)
    let wait = System.TimeSpan.FromDays(1.0) |> Some |> Option.toNullable
    actor <! Ping
    p.ExpectMsg(Started, wait) |> ignore
    p.ExpectMsg(Pong, wait) |> ignore