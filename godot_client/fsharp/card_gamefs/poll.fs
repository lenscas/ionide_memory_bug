namespace CardGame

open FSharp.Json
open Godot
open System
open System.Collections.Generic
open System.Collections
open System.Diagnostics
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks
open FSharp.Control.Tasks.NonAffine

type PollResult<'a> =
    | Got of 'a
    | NotYet

module PollHelper =
    let isDone res =
        match res with
        | Got (_) -> true
        | NotYet -> false

    let isNotDone res = isDone res |> not

    let toOption res =
        match res with
        | Got (x) -> Some(x)
        | NotYet -> None

    let fromOption res =
        match res with
        | Some x -> Got x
        | None -> NotYet

type Poll<'T>(func: unit -> PollResult<'T>) =
    let mutable funcResult: PollResult<'T> = NotYet

    member this.Peek() = funcResult

    member this.Poll() =
        match funcResult with
        | Got (x) -> Got(x)
        | NotYet ->
            let res = func ()
            funcResult <- res
            funcResult

    member this.Force(waiter: unit -> SignalAwaiter2) =
        task {
            let mutable value: option<'T> = None
            let mutable breakOut = false

            while not breakOut do
                let shouldWait =
                    match this.Poll() with
                    | Got (x) ->
                        value <- Some(x)
                        breakOut <- true
                        false
                    | NotYet -> true

                if shouldWait then
                    let! _ = waiter ()
                    ()
                else
                    ()

            return value |> Option.get
        }

    static member FromResult<'T, 'B>(res: Result<Poll<'T>, 'B>) : (Poll<Result<'T, 'B>>) =
        new Poll<Result<'T, 'B>>(fun () ->
            match res with
            | Ok (x) ->
                match x.Poll() with
                | Got (z) -> Got(Ok(z))
                | NotYet -> NotYet
            | Result.Error (x) -> Got(Result.Error(x)))

    static member Ready<'T> res =
        new Poll<'T>(fun () -> PollResult.Got(res))

module Poll =
    let AndThen<'A, 'T> (func: 'A -> Poll<'T>) (poll1: Poll<'A>) =
        let mutable otherPoll: option<Poll<'T>> = None

        new Poll<'T>(fun () ->
            match otherPoll with
            | Some (x) -> x.Poll()
            | None ->
                match poll1.Poll() with
                | Got (x) ->
                    otherPoll <- Some(func (x))
                    NotYet
                | NotYet -> NotYet)

    let AndThenOk<'A, 'ERR, 'B> (func: 'A -> Poll<Result<'B, 'ERR>>) (poll1: Poll<Result<'A, 'ERR>>) =
        let mutable otherPoll: option<Poll<Result<'B, 'ERR>>> = None

        new Poll<Result<'B, 'ERR>>(fun () ->
            match otherPoll with
            | Some (x) -> x.Poll()
            | None ->
                match poll1.Poll() with
                | Got (x) ->
                    match x with
                    | Ok (x) -> otherPoll <- Some(func (x))
                    | Result.Error (x) -> otherPoll <- Some(Poll(fun () -> Got(Result.Error(x))))

                    NotYet
                | NotYet -> NotYet)

    let Map<'A, 'T> (func: 'A -> 'T) (poll1: Poll<'A>) =
        poll1
        |> AndThen(fun x -> Poll(fun () -> x |> func |> Got))

    let MapOk<'A, 'T, 'Err> (func: 'A -> 'T) (poll: Poll<Result<'A, 'Err>>) =
        poll
        |> Map
            (fun x ->
                match x with
                | Ok (x) -> Ok(func (x))
                | Result.Error (x) -> Result.Error(x))

    let After func poll =
        poll
        |> Map
            (fun x ->
                func (x)
                x)

    let AfterOk func poll =
        poll
        |> After
            (fun x ->
                match x with
                | Result.Error (_) -> ()
                | Ok (x) -> func (x))

    let Flatten<'T, 'ERR> (poll: Poll<Result<Result<'T, 'ERR>, 'ERR>>) =
        poll
        |> Map
            (fun x ->
                match x with
                | Ok (x) -> x
                | Result.Error (x) -> Result.Error x)

    let IgnoreResult poll = poll |> Map ignore

    let TryPoll<'T, 'A> (func: 'T -> 'A) (poll: Option<Poll<'T>>) =
        match poll with
        | Some (x) ->
            match x.Poll() with
            | Got (x) -> Some(func (x))
            | NotYet -> None
        | None -> None


    let Peek<'a, 't> (func: 'a -> 't) (poll: Poll<'a>) =
        match poll.Peek() with
        | Got x -> x |> func |> Some
        | NotYet -> None

    let TryPeek<'a, 't> (func: 'a -> 't) (poll: Poll<'a> option) =
        match poll with
        | Some poll -> Peek func poll
        | None -> None

    let TryIgnorePeek<'a, 't> (func: 'a -> 't) (poll: Poll<'a> option) = (TryPeek func poll) |> ignore

    let ignorePoll<'T> (poll: Poll<'T>) = poll.Poll() |> ignore
    let TryIgnorePoll<'T> (poll: Option<Poll<'T>>) = poll |> TryPoll ignore |> ignore

    let All<'T, 'L when 'L :> seq<Poll<'T>>> (polls: 'L) : Poll<'T list> =
        Poll<'T list>
            (fun () ->
                polls
                |> Seq.fold
                    (fun state current ->
                        match (state, current.Poll()) with
                        | Got x, Got y -> (y :: x) |> Got
                        | _, NotYet
                        | NotYet, _ -> NotYet)
                    (Got [])
                |> PollHelper.toOption
                |> Option.map List.rev
                |> PollHelper.fromOption)

    let MergeResults<'T, 'E, 'L when 'L :> seq<Result<'T, 'E>>> (pol: Poll<'L>) : Poll<Result<list<'T>, 'E>> =
        pol
        |> Map(
            (fun x ->
                x
                |> Seq.fold
                    (fun y z ->
                        match y with
                        | Ok ls ->
                            match z with
                            | Ok n -> ls |> List.append [ n ] |> Ok
                            | Error z -> Result.Error z

                        | Error x -> Result.Error x)
                    (Ok []))
        )

    type PollBuilder() =
        member this.Bind(value, func) = AndThen func value
        member this.Return(x) = Poll.Ready x
        member this.ReturnFrom x = x
        member this.Zero() = Poll.Ready()

        member this.Combine<'b>(a: Poll<unit>, b: Poll<'b>) =

            a |> AndThen(fun _ -> b)

        member this.Delay f = f ()

    let poll = PollBuilder()
