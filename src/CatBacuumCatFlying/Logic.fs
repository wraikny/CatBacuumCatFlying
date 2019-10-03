namespace Cbcf

open Affogato.Helper
open Affogato

type Speeds = {
  bacuumSpeed: float32
  fallingSpeed: float32
  flyingCatsSpeed: float32
} with
  static member inline Map2 (a, b, f) = {
    bacuumSpeed = f a.bacuumSpeed b.bacuumSpeed
    fallingSpeed = f a.fallingSpeed b.fallingSpeed
    flyingCatsSpeed = f a.flyingCatsSpeed b.flyingCatsSpeed
  }
  static member inline Return(x) = {
    bacuumSpeed = x
    fallingSpeed = x
    flyingCatsSpeed = x
  }

  static member inline (+) (a, b): Speeds = map2' (+) a b
  static member inline (*) (a, b): Speeds = map2' (*) a b
  static member inline ( *. ) (a: float32, b: Speeds) = (pure' a) * b
  static member inline ( .* ) (a: Speeds, b: float32) = b *. a


type Setting = {
  areaSize: int Vector2
  playerSize: float32 Vector2
  flyingCatsSize: float32 Vector2

  floorHeight: float32
  ceilingHeight: float32

  initSpeeds: Speeds
  diffSpeeds: Speeds

  levelScoreStage: uint32
  levelFrameStage: uint32
}


type GameObject = {
  key: obj
  size: float32 Vector2
  pos: float32 Vector2
} with
  member o.Area = Rectangle.init o.pos o.size
  member o.Foot = { o.pos with x = o.pos.x + o.size.x * 0.5f }

  member o.object = o

  static member Init size pos = {
    key = System.Object()
    size = size
    pos = pos
  }
  static member Map(x: GameObject, f) = f x

type Player = {
  object: GameObject
  velocity: float32 Vector2
} with
  static member Map(x: Player, f) = { x with object = f x.object }


type FlyingCatKind =
  | Damage of float32
  | Heal of float32
  | Score of float32


type FlyingCat = {
  object: GameObject
  kind: FlyingCatKind
} with
  static member Map(x: FlyingCat, f) = { x with object = f x.object }


type Model = {
  setting: Setting

  count: uint32
  score: uint32
  level: int
  isHold: bool

  player: Player
  flyingCats: FlyingCat []
} with
  member x.Speeds =
    x.setting.initSpeeds + (float32 x.level) *. x.setting.diffSpeeds

type Msg =
  | Tick
  | Hold of bool


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
