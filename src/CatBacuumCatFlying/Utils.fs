[<AutoOpen>]
module Cbcf.Utils

let inline outOf a b x = x < a || b < x

let inline lift (x: ^a): ^b = ( (^a or ^b) : (static member Lift:_->_) x)

let inline set y (x: ^a): ^a = (^a: (static member Set:_*_->_) (x, y))

let inline ifThen t f = if t then f else id


open System.Threading
open System.Collections.Generic
open Elmish

type Messenger<'arg, 'model, 'msg, 'view>(program: Program<'arg, 'model, 'msg, 'view>) =
  let viewEvent = Event<'view>()

  let mutable lastModel = Unchecked.defaultof<_>
  let mutable dispatch = None

  let ctx = SynchronizationContext.Current
  do
    if isNull ctx then invalidOp "Call from UI thread"

  let queue = Queue<'msg>()

  let program =
    let view = Program.view program
    program
    |> Program.withSetState(fun model dispatch ->
      lastModel <- model
      viewEvent.Trigger(view model dispatch)
    )
    |> Program.withSubscription(fun model ->
      [ fun f ->
        lastModel <- model
        dispatch <- Some f

        while queue.Count > 0 do f(queue.Dequeue())
      ]
    )

  member __.View with get() = viewEvent.Publish

  member __.Program with get() = program

  member __.LastModel with get() = lastModel

  member __.Enqueue(msg) =
    dispatch |> function
    | Some f -> f msg
    | _ -> queue.Enqueue(msg)

  member __.Start(arg) =
    program
    |> Program.runWith arg
      

module Messenger =
  let inline create program = new Messenger<_, _, _, _>(program)