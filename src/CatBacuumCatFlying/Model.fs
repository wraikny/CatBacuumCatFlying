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
  //member inline x.Speeds =
  //  x.setting.initSpeeds + (float32 x.level) *. x.setting.diffSpeeds

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

type Port = {
  addEffect: FlyingCat -> unit
  clear: F

  bacuumOn: F
  bacuumOff: F
  pause: F
  resume: F
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


//type Port =
//  | LoadCatsCache of (int * string) []
//  | SelectedCategory of (((int * string []) -> unit) -> Async<unit>)
//  | OutputLog of filepath:string * string


module ViewModel =
  type UI =
    | Title of string
    | Header of string
    | Text of string
    | BoldText of string
    | Large of string
    | Line
    | Button of string * (unit -> unit)

  let private urlButton text (url: string) =
    Button(text, fun() ->
      url
      |> System.Diagnostics.Process.Start
      |> ignore
    )
  let view model dispatch =

    let inline msgButton text msg =
      Button(text, fun() -> dispatch msg)

    model.mode |> function
    | CreditMode page ->
      [
        yield Title "クレジット"
        match page with
        | One -> yield! [
            urlButton "作者: wraikny" "https://twitter.com/wraikny"
            urlButton "ゲームエンジン: Altseed" "http://altseed.github.io/"
            urlButton "猫: The Cat API" "https://thecatapi.com"
            urlButton "フォント: M+ FONTS" "https://mplus-fonts.osdn.jp/"
            Line
            msgButton "次へ" <| SetMode (CreditMode Two)
          ]
        | Two -> yield! [
            urlButton "掃除機: いらすとや" "https://www.irasutoya.com/"
            urlButton "効果音素材: ポケットサウンド" "https://pocket-se.info"
            urlButton "BGM: d-elf.com" "https://www.d-elf.com/"
            Line
            msgButton "タイトルに戻る" <| SetMode TitleMode
        ]
      ]

    | TitleMode ->
      [
        Title model.setting.title
        //Text "by wraikny@Amusement Creators"
        //Line
        Text "ゲーム操作: スペース / ポーズ: Esc"
        Text "赤: ダメージ, 緑: 回復, 青: 得点"
        Line
        BoldText "ゲームスタート(スペースボタン長押し)" // <| SetMode SelectMode
        Line
        msgButton "クレジットを開く" <| SetMode (CreditMode One)
      ]

    | SelectMode ->
      [
        yield Header "モードセレクト"
        if model.categories.Length = 0 then
          yield Text "データをダウンロード中..."
          yield Text "しばらくお待ち下さい"
          yield Line
          yield Text "セキュリティソフトによって処理が一時停止する場合があります"
        else
          let len = model.categories.Length
          let inline createItem i =
             model.categories.[(model.categoryIndex + i + len) % len]
             |> snd

          yield Text <| createItem -1
          yield Large <| createItem 0
          yield Text <| createItem 1
          
          yield! [
            Line
            Text "スペースボタンで変更 / 長押しで決定"
          ]
      ]
    | WaitingMode ->
      [
        Header "画像をダウンロード中..."
        Text "しばらくお待ち下さい"
        Line
        Text "セキュリティソフトによって処理が一時停止する場合があります"
      ]
    | GameMode -> []
    | ErrorMode e ->
      [
        Text <| e.GetType().ToString()
        Text <| e.Message
        Line
        Text "スタッフ/製作者に教えてもらえると嬉しいです"
        Text (sprintf "ログファイルは'%s'に出力されます" model.setting.errorLogPath)
      ]

    | PauseMode ->
      [
        Header "ポーズ"
        Text "スペース/Escボタンでコンティニュー"
        Text "スペースボタン長押しでタイトル"
      ]

    | GameOverMode ->
      let levelText = sprintf "ステージ: %d" model.game.level
      let scoreText = sprintf "スコア: %d" model.game.score
      [
        Header "ゲームオーバー"
        Text levelText
        Text scoreText

        Line
        urlButton "ツイートする(ブラウザを開きます)" (
          sprintf """「%s」をプレイしました！
%s
%s
@wraikny"""
            model.setting.title levelText scoreText
          |> System.Web.HttpUtility.UrlEncode
          |> sprintf "https://twitter.com/intent/tweet?text=%s"
        )
        Line
        Text "スペースボタン長押しでタイトル"

      ]