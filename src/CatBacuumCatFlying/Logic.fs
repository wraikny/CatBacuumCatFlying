namespace Cbcf.Logic

open Cbcf
open Affogato.Helper
open Affogato
open Affogato.Collections

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

  let inline mapVelocity f x =
    x |>> fun (o: GameObject) ->
      { o with velocity = f o.velocity }
      

  let inline move x =
    x |>> fun (o: GameObject) ->
      { o with pos = o.pos + o.velocity }


module Player =
  let inline clampY (setting: Setting) (isReflectable) x =
    let floorHeight, ceilingHeight = setting.floorHeight, setting.ceilingHeight
    let area = GameObject.area x
    let up, down = Rectangle2.up area, Rectangle2.down area

    let inline setPosY y o =
      o |> GameObject.mapPos (fun p -> { p with y = y })
        //|> GameObject.mapVelocity (fun v ->
        //  if isReflectable then
        //    { v with y = -v.y }
        //  else zero
        //)

    if down >= floorHeight then
      x |> setPosY (floorHeight - area.size.y)
    elif up <= ceilingHeight then
      x |> setPosY (ceilingHeight)
    else x


  let inline update setting x =
    x
    |> GameObject.move
    |> clampY setting false


module FlyingCat =
  let inline outOfArea (x: FlyingCat) =
    if Rectangle2.right x.object.Area < 0.0f then Some x else None

  let inline update x =
    x
    |> GameObject.move
    |> outOfArea


open wraikny.Tart.Core

module GameModel =
  let inline mapPlayer f (model: GameModel): GameModel =
    { model with player = f model.player }

  let inline mapFlyingCat f (model: GameModel): GameModel =
    { model with flyingCats = Array.choose f model.flyingCats }

  let inline calculate (model: GameModel) =
    let collidedMap =
      model.flyingCats
      |> Seq.map (fun x ->
        if GameObject.inCollision model.player x then
          x.Key, false
        else
          x.Key, true
      )
      |> HashMap.ofSeq


    { model with
        flyingCats = model.flyingCats |> Array.filter (fun x -> HashMap.find x.Key collidedMap)
    }


  let updateModel (model: GameModel): GameModel =
    let count = model.count + one
    { model with
        count = count
        level =
          if count % model.setting.levelFrameStage = 0u then
            model.level + one
          else model.level
    }

  let tick model =
    let stg = model.setting

    model
    |> updateModel
    |> mapPlayer (Player.update stg)
    |> mapFlyingCat (FlyingCat.update)
    , Cmd.none

  let push model =
    { model with
        isHold = true
        player =
          { model.player with
              velocity = Vector2.init zero -model.Speeds.bacuumSpeed
          }
    }, Cmd.none

  let release model =
    { model with
        isHold = false
        player =
          { model.player with
              velocity = Vector2.init zero model.Speeds.fallingSpeed
          }
    }, Cmd.none


module Model =
  let inline chain f m =
    let g, c = f m.game
    set g m, c

  let update (msg: Msg) (model: Model) =
    (model.mode, msg) |> function
    | Game, Tick ->
      model |> chain GameModel.tick
    | Game, Push ->
      model |> chain GameModel.push
    | Game, Release ->
      model |> chain GameModel.release
