module Elmish.Reactive

open System
open System.Threading
open System.Collections.Generic
open Elmish

type Messenger<'arg, 'model, 'msg, 'view>(program: Program<'arg, 'model, 'msg, 'view>) =

  let ctx = SynchronizationContext.Current
  do
    if isNull ctx then invalidOp "Call from UI thread"

  let queue = Queue<'msg>()
  let onErrorEvent = Event<string * exn>()
  let viewEvent = Event<'view>()

  let mutable lastModel = Unchecked.defaultof<_>
  let mutable dispatch = None

  let program =
    let view = Program.view program

    program
    |> Program.withSetState (fun model dispatch ->
      (Program.setState program model dispatch)

      lastModel <- model
      viewEvent.Trigger(view model dispatch)
    )
    |> Program.withSubscription (fun _ ->
      [ fun f ->
        dispatch <- Some f

        while queue.Count > 0 do
          f(queue.Dequeue())
      ]
    )
    |> Program.mapErrorHandler(fun handler x ->
      handler x
      onErrorEvent.Trigger(x)
    )

  member private __.View with get() = viewEvent.Publish
  member __.OnError with get() = onErrorEvent.Publish

  member __.LastModel with get() = lastModel

  member __.Dispatch(msg) =
    let inline exec() =
      dispatch |> function
      | Some f -> f msg
      | _ -> queue.Enqueue(msg)

    if isNull SynchronizationContext.Current then
      ctx.Post((fun _ -> exec()), null)
    else
      exec()

  member __.Run arg =
    program
    |> Program.runWith arg

  interface IObservable<'view> with
    member this.Subscribe(observer) =
      this.View.Subscribe(observer)


[<RequireQualifiedAccess>]
module Messenger =
  let inline create program: Messenger<'arg, 'model, 'msg, 'view> =
    new Messenger<_, _, _, _>(program)
  
  let inline mkProgram (init: 'arg -> 'model * Cmd<'msg>) update =
    Program.mkProgram init update (fun m _ -> m)

  let inline mkSimple (init: 'arg -> 'model) update =
    Program.mkSimple init update (fun m _ -> m)

  let inline mkProgram' (init: 'model * Cmd<'msg>) update =
    mkProgram (fun() -> init) update
