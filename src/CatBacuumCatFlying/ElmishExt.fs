[<AutoOpen>]
module Elmish.Extension

open System.Threading
open System.Collections.Generic
open Elmish

type Messenger<'arg, 'model, 'msg, 'view>(program: Program<'arg, 'model, 'msg, 'view>) =
  let viewEvent = Event<'view>()

  do
    let ctx = SynchronizationContext.Current
    if isNull ctx then invalidOp "Call from UI thread"

  let mutable lastModel = Unchecked.defaultof<_>
  let mutable dispatch = None

  let queue = Queue<'msg>()

  let program =
    let view = Program.view program
    program
    |> Program.withSetState (fun model dispatch ->
      lastModel <- model
      viewEvent.Trigger(view model dispatch)
    )
    |> Program.withSubscription (fun _ ->
      [ fun f ->
        dispatch <- Some f

        while queue.Count > 0 do f(queue.Dequeue())
      ]
    )

  member __.View with get() = viewEvent.Publish

  member __.LastModel with get() = lastModel

  member __.Enqueue(msg) =
    dispatch |> function
    | Some f -> f msg
    | _ -> queue.Enqueue(msg)

  member __.Start(arg) =
    program
    |> Program.runWith arg


[<RequireQualifiedAccess>]
module Messenger =
  let inline create program = new Messenger<_, _, _, _>(program)