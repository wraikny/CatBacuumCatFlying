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

  playerX: float32

  floorHeight: float32
  ceilingHeight: float32

  initSpeeds: Speeds
  diffSpeeds: Speeds

  levelScoreStage: uint32
  levelFrameStage: uint32
} with
  member inline x.PlayerInitPosition =
    let ps = x.playerSize
    Vector2.init (x.playerX - ps.x * 0.5f) (x.floorHeight - ps.y)


type GameObject = {
  key: obj
  size: float32 Vector2
  pos: float32 Vector2
  velocity: float32 Vector2
} with
  member inline o.Area = Rectangle.init o.pos o.size
  member inline o.Foot = { o.pos with x = o.pos.x + o.size.x * 0.5f }

  member inline o.object = o

  static member inline Init (pos, size, velocity) = {
    key = System.Object()
    size = size
    pos = pos
    velocity = velocity
  }

  static member inline Init(pos, size) = GameObject.Init(pos, size, zero)

  static member inline Map(x: GameObject, f) = f x

//type Player = {
//  object: GameObject
//  velocity: float32 Vector2
//} with
//  static member Map(x: Player, f) = { x with object = f x.object }

//  static member Init(size, pos) = {
//    velocity = zero
//    object = GameObject.Init(pos, size)
//  }


type FlyingCatKind =
  | Damage of float32
  | Heal of float32
  | Score of float32


type FlyingCat = {
  object: GameObject
  kind: FlyingCatKind
  point: uint32
} with
  static member inline Map(x: FlyingCat, f) = { x with object = f x.object }
  member inline x.Key = x.object.key


type GameModel = {
  setting: Setting

  count: uint32
  score: uint32
  level: int
  isHold: bool

  player: GameObject
  flyingCats: FlyingCat []
} with
  member inline x.Speeds =
    x.setting.initSpeeds + (float32 x.level) *. x.setting.diffSpeeds

  static member inline Init(setting) = {
    setting = setting
    count = 0u
    score = 0u
    level = 1
    isHold = false
    player = GameObject.Init(setting.PlayerInitPosition, setting.playerSize)
    flyingCats = Array.empty
  }

type Mode = Game

type Model = {
  mode: Mode
  game: GameModel
} with
  static member inline Init(setting) = {
    mode = Game
    game = GameModel.Init(setting)
  }

  static member inline Set(model, x) =
    { model with game = x }


type Msg =
  | Tick
  | Push
  | Release
