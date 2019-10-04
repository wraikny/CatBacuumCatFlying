namespace Cbcf.Logic

open Cbcf
open Affogato
open Affogato.Helper
open Affogato.Collections
open FSharpPlus

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
    if Rectangle2.right x.object.Area < 0.0f then None else Some x

  let inline update (x: FlyingCat) =
    x
    |> GameObject.move
    |> outOfArea


open wraikny.Tart.Core
open wraikny.Tart.Core.Libraries

module GameModel =
  let inline mapPlayer f (model: GameModel): GameModel =
    { model with player = f model.player }

  let inline mapFlyingCat f (model: GameModel): GameModel =
    { model with flyingCats = Array.choose f model.flyingCats }

  let inline calculate (model: GameModel) =
    let collidedMap =
      seq {
        for x in model.flyingCats do
          if GameObject.inCollision model.player x then
            yield (x.Key, ())
      }
      |> HashMap.ofSeq

    let score, hp =
      let mutable score = zero
      let mutable hp = zero
      for x in model.flyingCats do
        if HashMap.containsKey x.Key collidedMap then
          x.kind |> function
          | Score a -> score <- score + a
          | HP a -> hp <- hp + a

      score + model.score, hp + model.hp

    printfn "Score: %d, HP: %f" score hp

    { model with
        flyingCats = model.flyingCats |> Array.filter (fun x -> HashMap.containsKey x.Key collidedMap)
        score = score
        hp = hp
    } |> ifThen(score % model.setting.levelScoreStage = 0u) GameModel.LevelUp


  let countup (model: GameModel): GameModel =
    let count = model.count + one
    { model with
        count = count
        generateCount = model.generateCount + one
    } |> ifThen (count % model.setting.levelFrameStage = 0u) GameModel.LevelUp


  let addFlyingCatCheck (model: GameModel) =
    if model.generateCount >= model.generatePeriod then
      let stg = model.setting
      { model with generateCount = 0u }, (
        (monad {
          let! p = Random.double01
          let! q = Random.double01
          let! flag = Random.int 0 3
          let kind = flag |> function
            | 0 -> HP (0.2f * stg.hp * float32 p)
            | 1 -> HP -(0.2f * stg.hp * float32 p)
            | 2 ->
              let p, _ = Utils.boxMullersMethod (float32 p) (float32 q)
              Score (0.5f * p * (float32 stg.levelScoreStage) |> abs |> uint32)
            | x -> failwithf "Unexpected flag %d" x

          let size = stg.flyingCatsSize
          let! posY = Random.float (float stg.ceilingHeight) (float <| stg.floorHeight - size.y)

          let pos = Vector2.init stg.generateX (float32 posY)

          return {
            kind = kind
            object = GameObject.Init(pos, size, Vector2.init -model.speeds.flyingCatsSpeed 0.0f)
          }
        }: Random.Generator<_>)
        |> SideEffect.performWith Msg.AddFlyingCat
      )
    else
      model, Cmd.none


  let update (msg: GameMsg) (model: GameModel) =
    msg |> function
    | Tick ->
      let stg = model.setting

      model
      |> countup
      |> mapPlayer (Player.update stg)
      |> mapFlyingCat (FlyingCat.update)
      |> addFlyingCatCheck

    | AddFlyingCat x ->
      //printfn "AddFlyingCat %A" x
      { model with
          flyingCats = Array.append model.flyingCats [|x|]
      }, Cmd.none


  let push model =
    { model with
        isHold = true
        player =
          { model.player with
              velocity = Vector2.init zero -model.speeds.bacuumSpeed
          }
    }, Cmd.none

  let release model =
    { model with
        isHold = false
        player =
          { model.player with
              velocity = Vector2.init zero model.speeds.fallingSpeed
          }
    }, Cmd.none


module Model =
  let inline chain f m =
    let g, c = f m.game
    set g m, c

  let update (msg: Msg) (model: Model) =
    (model.mode, msg) |> function
    | Game, GameMsg m ->
      model |> chain (GameModel.update m)
    | Game, Push ->
      model |> chain GameModel.push
    | Game, Release ->
      model |> chain GameModel.release
