namespace Cbcf

open Affogato
open Affogato.Helper
open Affogato.Collections
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



type GameSetting = {
  areaSize: int Vector2
  playerSize: float32 Vector2
  flyingCatsSize: float32 Vector2

  playerX: float32
  playerSizeRate: float32

  floorHeight: float32
  ceilingHeight: float32

  initSpeeds: Speeds
  diffSpeeds: Speeds

  generatePerMin: float32 * float32
  generateX: float32

  hp: float32

  scoreDiffPerSec: uint32
  levelScoreStage: uint32
  levelFrameStage: uint32

  medicalImagePath: string
  coinImagePath: string
} with
  member inline x.PlayerInitPosition =
    let ps = x.playerSize
    Vector2.init (x.playerX - ps.x * 0.5f) (x.floorHeight - ps.y)


type GameObject = {
  key: uint16
  size: float32 Vector2
  pos: float32 Vector2
  velocity: float32 Vector2
  imagePath: string
} with
  member inline o.Area = Rectangle.init o.pos o.size
  member inline o.Foot = { o.pos with x = o.pos.x + o.size.x * 0.5f }

  member inline o.object = o

  static member inline Init (key, pos, size, velocity, imagePath) = {
    key = key
    size = size
    pos = pos
    velocity = velocity
    imagePath = imagePath
  }

  static member inline Init(key, pos, size) = GameObject.Init(key, pos, size, zero, "")

  static member inline Map(x: GameObject, f) = f x


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
  setting: GameSetting
  speeds: Speeds
  generatePeriod: uint32
  generateCount: uint32

  scoreForLevelStage: uint32

  count: uint32
  hp: float32
  score: uint32
  level: int
  isHold: bool

  nextId: uint16

  player: GameObject
  flyingCats: FlyingCat []

  imagePaths: Map<int, string []>
  category: int
} with

  static member inline Init(setting) = {
    category = zero
    setting = setting
    speeds = setting.initSpeeds
    generatePeriod = uint32 <| 1800.0f / (fst setting.generatePerMin)
    generateCount = zero

    scoreForLevelStage = zero

    count = zero
    hp = setting.hp
    score = zero
    level = one
    isHold = false

    nextId = zero

    player = GameObject.Init(zero, setting.PlayerInitPosition, setting.playerSize)
    flyingCats = Array.empty

    imagePaths = Map.empty

  }

  static member inline Restart(model) =
    { GameModel.Init(model.setting) with
        imagePaths = model.imagePaths
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

type CreditPage = One | Two

type Mode =
  | CreditMode of CreditPage
  | TitleMode
  | SelectMode
  | WaitingMode
  | GameMode
  | GameOverMode
  | PauseMode
  | ErrorMode of exn
with
  member x.EnabledLongPress = x |> function
    | CreditMode _
    | GameMode
    | WaitingMode
    | ErrorMode _
      -> false
    | _ -> true


type Setting = {
  requestLimit: int
  theCatApiCacheDirectory: string
  gameStartFileCount: int
  title: string
  errorLogPath: string
}

type F = unit -> unit

type SEKind =
  | Medical
  | Coin
  | Enter
  | Click

type Port = {
  addEffect: FlyingCat -> unit
  clear: F

  toggleBacuum: bool -> unit

  pause: F
  resume: F

  playSE: SEKind -> unit
}

type Model = {
  setting: Setting
  prevMode: Mode
  mode: Mode
  game: GameModel

  apiKey: string
  categories: (int * string) []
  categoryIndex: int

  port: Port
} with
  static member inline Init(setting, gameSetting, apiKey, port) = {
    setting = setting
    prevMode = TitleMode
    mode = TitleMode
    game = GameModel.Init(gameSetting)
    apiKey = apiKey
    categories = Array.empty
    categoryIndex = 0

    port = port
  }

  static member inline Set(model, x) =
    { model with game = x }

  static member inline Restart(model) =
    { model with
        categoryIndex = zero
        game = GameModel.Restart(model.game)
    }


type GameMsg =
  | AddFlyingCat of FlyingCat
  | Tick
  | SetPlayerImage of string


type Msg =
  | SetMode of Mode
  | GameMsg of GameMsg

  | SetCategories of (int * string) []
  | AddImagePaths of category:int * filepath:string []

  | DirectoryNotFound

  | Push | Release | LongPress
with
  static member inline Tick = GameMsg Tick
  static member inline AddFlyingCat x = GameMsg <| AddFlyingCat x