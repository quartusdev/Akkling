namespace Akkling

open System
open Akka.Actor

/// <summary>
/// Exposes an Akka.NET actor API accessible from inside of F# continuations
/// </summary>
[<Interface>] 
type Actor<'Message> = 
    inherit IActorRefFactory
    inherit ICanWatch
    
    /// <summary>
    /// Gets <see cref="IActorRef" /> for the current actor.
    /// </summary>
    abstract Self : IActorRef<'Message>
    
    /// <summary>
    /// Gets <see cref="ActorSystem" /> for the current actor.
    /// </summary>
    abstract System : ActorSystem
    
    /// <summary>
    /// Returns a sender of current message or <see cref="ActorRefs.NoSender" />, if none could be determined.
    /// </summary>
    abstract Sender<'Response> : unit -> IActorRef<'Response>
    
    /// <summary>
    /// Returns a parrent of current actor.
    /// </summary>
    abstract Parent<'Other> : unit -> IActorRef<'Other>

    /// <summary>
    /// Lazy logging adapter. It won't be initialized until logging function will be called. 
    /// </summary>
    abstract Log : Lazy<Akka.Event.ILoggingAdapter>
    
    /// <summary>
    /// Stashes the current message (the message that the actor received last)
    /// </summary>
    abstract Stash : unit -> unit
    
    /// <summary>
    /// Unstash the oldest message in the stash and prepends it to the actor's mailbox.
    /// The message is removed from the stash.
    /// </summary>
    abstract Unstash : unit -> unit
    
    /// <summary>
    /// Unstashes all messages by prepending them to the actor's mailbox.
    /// The stash is guaranteed to be empty afterwards.
    /// </summary>
    abstract UnstashAll : unit -> unit
    
    /// <summary>
    /// Sets or clears a timeout before <see="ReceiveTimeout"/> message will be send to an actor.
    /// </summary>
    abstract SetReceiveTimeout : TimeSpan option -> unit
    
    /// <summary>
    /// Schedules a message to be transmited in specified delay.
    /// </summary>
    abstract Schedule<'Scheduled> : TimeSpan -> IActorRef<'Scheduled> -> 'Scheduled -> ICancelable
    
    /// <summary>
    /// Schedules a message to be repeatedly transmited, starting at specified delay with provided intervals.
    /// </summary>
    abstract ScheduleRepeatedly<'Scheduled> : TimeSpan -> TimeSpan -> IActorRef<'Scheduled> -> 'Scheduled -> ICancelable

    /// <summary>
    /// A raw Actor context in it's untyped form.
    /// </summary>
    abstract UntypedContext : IActorContext 

[<Interface>]
type ExtContext =
    /// <summary>
    /// Returns current actor incarnation
    /// </summary>
    abstract Incarnation : unit -> ActorBase
    
    /// <summary>
    /// Stops execution of provided actor.
    /// </summary>
    abstract Stop : IActorRef<'T> -> unit

/// <summary>
/// Exposes an Akka.NET extended actor API accessible from inside of F# continuations 
/// </summary>
[<Interface>]
type ExtActor<'Message> = 
    inherit Actor<'Message>
    inherit ExtContext

