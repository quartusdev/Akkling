//-----------------------------------------------------------------------
// <copyright file="Spawning.fs" company="Akka.NET Project">
//     Copyright (C) 2009-2015 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2015 Akka.NET project <https://github.com/akkadotnet/akka.net>
//     Copyright (C) 2015 Bartosz Sypytkowski <gttps://github.com/Horusiath>
// </copyright>
//-----------------------------------------------------------------------

namespace Akkling

open Akka.Actor
open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq.QuotationEvaluation

[<RequireQualifiedAccess>]
module Configuration = 
    let internal extendedConfig = (Akka.Configuration.ConfigurationFactory.ParseString """
            akka.actor {
                serializers {
                    hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
                }
                serialization-bindings {
                  "System.Object" = hyperion
                }
            }
        """)

    /// Parses provided HOCON string into a valid Akka configuration object.
    let parse = Akka.Configuration.ConfigurationFactory.ParseString
    
    /// Returns default Akka for F# configuration.
    let defaultConfig () = extendedConfig.WithFallback(Akka.Configuration.ConfigurationFactory.Default())
    
    /// Loads Akka configuration from the project's .config file.
    let load = Akka.Configuration.ConfigurationFactory.Load

module System = 
    /// Creates an actor system with remote deployment serialization enabled.
    let create (name : string) (config : Akka.Configuration.Config) : ActorSystem = 
        let _ = Akka.Serialization.HyperionSerializer           // I don't know why, but without this system cannot instantiate serializer
        let system = ActorSystem.Create(name, config.WithFallback Configuration.extendedConfig)
        let exprSerializer = Akkling.Serialization.ExprSerializer(system :?> ExtendedActorSystem)
        system.Serialization.AddSerializer(exprSerializer)
        system.Serialization.AddSerializationMap(typeof<Expr>, exprSerializer)
        system

[<AutoOpen>]
module Spawn = 

    /// <summary>
    /// Spawns an actor using specified actor <see cref="Props{Message}"/>.
    /// </summary>
    /// <param name="actorFactory">Either actor system or parent actor</param>
    /// <param name="name">Name of spawned child actor</param>
    /// <param name="f">Used by actor for handling response for incoming request</param>
    let spawn (actorFactory : IActorRefFactory) (name : string) (p: Props<'Message>) : IActorRef<'Message> = 
        typed (actorFactory.ActorOf(p.ToProps(), name)) :> IActorRef<'Message>

    /// <summary>
    /// Spawns an anonymous actor with automatically generated name using specified actor <see cref="Props{Message}"/>.
    /// </summary>
    /// <param name="actorFactory">Either actor system or parent actor</param>
    /// <param name="name">Name of spawned child actor</param>
    /// <param name="f">Used by actor for handling response for incoming request</param>
    let inline spawnAnonymous (actorFactory : IActorRefFactory) (p: Props<'Message>) : IActorRef<'Message> = 
        spawn actorFactory null p