namespace Cbcf

open Affogato
open Affogato.Helper
open FSharpPlus

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

  generatePerMin: float32 * float32
  generateX: float32

  hp: float32

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
  | HP of float32
  | Score of uint32


type FlyingCat = {
  object: GameObject
  kind: FlyingCatKind
} with
  static member inline Map(x: FlyingCat, f) = { x with object = f x.object }
  member inline x.Key = x.object.key


type GameModel = {
  setting: Setting
  speeds: Speeds
  generatePeriod: uint32
  generateCount: uint32

  count: uint32
  hp: float32
  score: uint32
  level: int
  isHold: bool

  //nextId: uint32

  player: GameObject
  flyingCats: FlyingCat []
} with
  //member inline x.Speeds =
  //  x.setting.initSpeeds + (float32 x.level) *. x.setting.diffSpeeds

  static member inline Init(setting) = {
    setting = setting
    speeds = setting.initSpeeds
    generatePeriod = uint32 <| 1800.0f / (fst setting.generatePerMin)
    generateCount = 0u

    count = 0u
    hp = setting.hp
    score = 0u
    level = 1
    isHold = false

    //nextId = 0u

    player = GameObject.Init(setting.PlayerInitPosition, setting.playerSize)
    flyingCats = Array.empty
  }

  static member inline LevelUp(x) =
    let level = x.level + one
    { x with
        level = level
        speeds = x.speeds + x.setting.diffSpeeds
        generatePeriod =
          let pmi, pmd = x.setting.generatePerMin
          uint32 <| 3600.0f / (pmi + (float32 level) * pmd)
    }


type Mode = | Game

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

type GameMsg =
  | AddFlyingCat of FlyingCat
  | Tick

type Msg =
  | GameMsg of GameMsg
  | Push
  | Release
with
  static member inline Tick = GameMsg Tick
  static member inline AddFlyingCat x = GameMsg <| AddFlyingCat x
