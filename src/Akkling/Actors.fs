//-----------------------------------------------------------------------
// <copyright file="Actors.fs" company="Akka.NET Project">
//     Copyright (C) 2009-2015 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2015 Akka.NET project <https://github.com/akkadotnet/akka.net>
//     Copyright (C) 2015 Bartosz Sypytkowski <gttps://github.com/Horusiath>
// </copyright>
//-----------------------------------------------------------------------
[<AutoOpen>]
module Akkling.Actors

open System
open Akka.Actor
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq.QuotationEvaluation



type LifecycleEvent = 
    | PreStart
    | PostStop
    | PreRestart of cause : exn * message : obj
    | PostRestart of cause : exn

type TypedContext<'Message, 'Actor when 'Actor :> ActorBase and 'Actor :> IWithUnboundedStash>(context : IActorContext, actor : 'Actor) as this = 
    let self = context.Self
    interface ExtActor<'Message> with
        member __.UntypedContext = context
        member __.Self = typed self
        member __.Sender<'Response>() = typed (context.Sender) :> IActorRef<'Response>
        member __.Parent<'Other>() = typed (context.Parent) :> IActorRef<'Other>
        member __.System = context.System
        member __.ActorOf(props, name) = context.ActorOf(props, name)
        member __.ActorSelection(path : string) = context.ActorSelection(path)
        member __.ActorSelection(path : ActorPath) = context.ActorSelection(path)
        member __.Watch(aref : IActorRef) = context.Watch aref
        member __.Unwatch(aref : IActorRef) = context.Unwatch aref
        member __.Log = lazy (Akka.Event.Logging.GetLogger(context))
        member __.Stash() = actor.Stash.Stash()
        member __.Unstash() = actor.Stash.Unstash()
        member __.UnstashAll() = actor.Stash.UnstashAll()
        member __.SetReceiveTimeout timeout = context.SetReceiveTimeout(Option.toNullable timeout)
        member __.Schedule (delay : TimeSpan) target message = 
            context.System.Scheduler.ScheduleTellOnceCancelable(delay, untyped target, message, self)
        member __.ScheduleRepeatedly (delay : TimeSpan) (interval : TimeSpan) target message = 
            context.System.Scheduler.ScheduleTellRepeatedlyCancelable(delay, interval, untyped target, message, self)
        member __.Incarnation() = actor :> ActorBase
        member __.Stop(ref : IActorRef<'T>) = context.Stop(untyped(ref))
            
type [<AbstractClass>]Actor() = 
    inherit UntypedActor()
    interface IWithUnboundedStash with
        member val Stash = null with get, set

type FunActor<'Message>(initialBehavior : Behavior<'Message>) as this =
    inherit Actor() 

    let untypedContext = UntypedActor.Context :> IActorContext
    let ctx = TypedContext<'Message, FunActor<'Message>>(untypedContext, this)

    /// The behavior used to handle the next message or signal
    let mutable behavior = initialBehavior

    do
        if not (Behaviors.validateInitial initialBehavior) then
            failwith "initial behavior is invalid"

    let isSignal (msg: obj) =
        match msg with
        | :? LifecycleEvent
        | :? Terminated -> true
        | _ -> false

//    let runAsnyc a =
//        Akka.Dispatch.ActorTaskScheduler.RunTask(System.Func<System.Threading.Tasks.Task>(fun () -> 
//            upcast (a |> Async.StartAsTask))
    
    member __.Next current next (message : obj) = 
        match next with
        | Behavior.Unhandled when not (message :? LifecycleEvent) ->
            this.Unhandled message
        | Behavior.Stopped ->
            untypedContext.Stop(untypedContext.Self)
        | _ -> ()

        Behaviors.canonicalize ctx current next

    member __.Handle (msg: obj) = 
        let runAsnyc a =
            Akka.Dispatch.ActorTaskScheduler.RunTask(System.Func<System.Threading.Tasks.Task>(fun () -> 
                upcast (a |> Async.StartAsTask)))

        let next =
            match behavior with
            | Behavior.Extensible (onMessage, onSignal) ->
                match msg with
                | signal when isSignal msg ->
                    onSignal ctx signal
                | :? 'Message as message ->
                    onMessage ctx message
                | _ ->
                    // Message is not a signal or a message that can be handled
                    Behavior.Unhandled
            | Behavior.ExtensibleAsync (onMessage, onSignal) ->
                match msg with
                | signal when isSignal msg ->
                    async {
                        let! b = onSignal ctx signal 
                        behavior <- this.Next behavior b msg
                    } 
                    |> runAsnyc

                    // Just return same here, since the async block will set the behavior later
                    Behavior.Same 
                | :? 'Message as message ->
                    async {
                        let! b = onMessage ctx message
                        behavior <- this.Next behavior b msg
                    }
                    |> runAsnyc

                    // Just return same here, since the async block will set the behavior later
                    Behavior.Same
                | _ ->
                    // Message is not a signal or a message that can be handled
                    Behavior.Unhandled
            | Behavior.Stopped -> 
                Behavior.Stopped
            | _ -> invalidOp (sprintf "cannot handle behavior %A" behavior)

        behavior <- this.Next behavior next msg
    
    member __.Sender() : IActorRef = base.Sender

    member this.InternalUnhandled(message: obj) : unit = this.Unhandled message

    override this.OnReceive msg = this.Handle msg
    
    override this.PostStop() = 
        base.PostStop()
        this.Handle PostStop
    
    override this.PreStart() = 
        base.PreStart()
        // Undefer possible deffered behavior before receiving messages
        behavior <- Behaviors.undefer ctx behavior
        this.Handle PreStart
    
    override this.PreRestart(cause, msg) = 
        base.PreRestart(cause, msg)
        this.Handle(PreRestart(cause, msg))
    
    override this.PostRestart(cause) = 
        base.PostRestart cause
        this.Handle(PostRestart cause)