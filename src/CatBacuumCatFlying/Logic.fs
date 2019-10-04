namespace Cbcf

open Affogato.Helper
open Affogato


[<AutoOpen>]
module Utils =
  let inline outOf a b x = x < a || b < x


module GameObject =
  let inline get (x: ^a): GameObject =
    (^a: (member object:_) x)

  let inline area x = (get x).Area
  let inline pos x = (get x).pos

  let inline inCollision (x: ^a) (y: ^b): bool =
    Rectangle.isCollided2 (area x) (area y)

  let inline mapPos f x =
    x |>> fun (o: GameObject) ->
      { o with pos = f o.pos }

module Player =
  let inline mapVelocity f x =
    { x with velocity = f x.velocity }
      
  let inline clampY (setting: Setting) (isReflectable) x =
    let yMin, yMax = setting.floorHeight, setting.ceilingHeight
    if (GameObject.pos x).y |> outOf yMin yMax then
      x |> GameObject.mapPos (fun p -> { p with y = setting.floorHeight })
        |> mapVelocity (fun v ->
          if isReflectable then
            { v with y = -v.y }
          else zero
        )
    else
      x

  let inline bacuum (speeds: Speeds) x =
    x
    |> mapVelocity ((+) <| Vector2.init 0.0f speeds.bacuumSpeed)


  let inline update setting x =
    x
    |> GameObject.mapPos ((+) x.velocity)
    |> clampY setting false

module FlyingCat =
  let inline update setting x =
    x


module Update =
  open wraikny.Tart.Core
  let mapPlayer f (model: Model): Model =
    { model with player = f model.player }

  let mapFlyingCat f (model: Model): Model =
    { model with flyingCats = Array.map f model.flyingCats }

  let updateModel (model: Model): Model =
    let count = model.count + one
    { model with
        count = count
        level =
          if count % model.setting.levelFrameStage = 0u then
            model.level + one
          else model.level
    }

  let update (msg: Msg) (model: Model): Model * Cmd<Msg, _> =
    msg |> function
    | Tick ->
      let stg = model.setting
      model
      |> updateModel
      |> mapPlayer (Player.update stg)
      |> mapFlyingCat (FlyingCat.update stg)
      , Cmd.none

    | Hold t ->
      { model with isHold = t }, Cmd.none
