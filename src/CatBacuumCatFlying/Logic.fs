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
  let inline clampY (setting: GameSetting) (isReflectable) x =
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
  let inline private mapPlayer f (model: GameModel): GameModel =
    { model with player = f model.player }

  let inline private mapFlyingCat f (model: GameModel): GameModel =
    { model with flyingCats = Array.choose f model.flyingCats }

  let inline private calculate (model: GameModel) =
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

      score, hp

    let newScore = model.scoreForLevelStage + score
    let scoreLebelUp = (model.scoreForLevelStage + score) > model.setting.levelScoreStage

    { model with
        flyingCats = model.flyingCats |> Array.filter (fun x -> not <| HashMap.containsKey x.Key collidedMap)
        score = score + model.score
        hp = hp + model.hp |> max zero |> min model.setting.hp
        scoreForLevelStage = if scoreLebelUp then zero else newScore
    } |> ifThen(scoreLebelUp) GameModel.LevelUp


  let private countup (model: GameModel): GameModel =
    let count = model.count + one
    { model with
        count = count
        generateCount = model.generateCount + one
    } |> ifThen (count % model.setting.levelFrameStage = 0u) GameModel.LevelUp


  let private addFlyingCatCheck (model: GameModel) =
    if model.generateCount >= model.generatePeriod then
      let stg = model.setting
      { model with generateCount = 0u }, (
        (monad {
          let! p = Random.double01
          let! q = Random.double01
          let! flag = Random.int 1 10
          let kind = flag |> function
            | 0 | 1 -> HP (0.2f * stg.hp * float32 p)
            | i when 1 < i && i < 9-> HP -(0.2f * stg.hp * float32 p)
            | 9 | 10 ->
              let p, _ = Utils.boxMullersMethod (float32 p) (float32 q)
              Score (0.5f * p * (float32 stg.levelScoreStage) |> abs |> uint32)
            | x -> failwithf "Unexpected flag %d" x

          let size = stg.flyingCatsSize
          let! posY = Random.float (float stg.ceilingHeight) (float <| stg.floorHeight - size.y)

          let currentPaths = model.imagePaths |> Map.find model.category
          let! imageIndex = Random.int 0 currentPaths.Length

          let pos = Vector2.init stg.generateX (float32 posY)

          return {
            kind = kind
            object = GameObject.Init(pos, size, Vector2.init -model.speeds.flyingCatsSpeed 0.0f, currentPaths.[imageIndex])
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
      |> calculate
      |> addFlyingCatCheck

    | AddFlyingCat x ->
      //printfn "AddFlyingCat %A" x
      { model with
          flyingCats = Array.append model.flyingCats [|x|]
      }, Cmd.none

    | SetPlayerImage s ->
      model
      |> mapPlayer(fun p -> { p with imagePath = s }), Cmd.none


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
    | _, AddImagePaths (c, ss) ->
      let xs =
        model.game.imagePaths |> Map.tryFind c
        |> function
          | Some x -> Array.append ss x
          | None -> ss

      let newMap = Map.add c xs model.game.imagePaths

      { model with
          game = { model.game with imagePaths = newMap }
      }, (
        let ps = newMap |> Map.find (fst model.categories.[model.categoryIndex])
        if model.mode = WaitingMode && ps.Length > model.setting.gameStartFileCount then
          Cmd.ofMsg(SetMode GameMode)
        else Cmd.none
      )

    | _, SetMode SelectMode ->
      { model with prevMode = model.mode; mode = SelectMode }
      , IO.loadCategoryAsync model.apiKey
        |> Async.Catch
        |> SideEffect.performWith(function
          | Choice1Of2 [||] ->
            SetMode(ErrorMode <| System.Exception("Categories list is empty"))
          | Choice1Of2 x ->
            SetCategories x
          | Choice2Of2 e ->
            SetMode(ErrorMode e)
        )

    | SelectMode, SetMode GameMode
    | WaitingMode, SetMode GameMode ->
      let category = fst model.categories.[model.categoryIndex]
      let ps = model.game.imagePaths |> Map.find category
      let cmd =
        (monad {
          let! i = Random.int 0 (ps.Length - 1)
          return SetPlayerImage ps.[i]
        }: Random.Generator<_>) |> SideEffect.performWith(GameMsg)

      { model with
          prevMode = model.mode
          mode = GameMode
          game = { model.game with category = category }
      }, cmd

    | _, SetMode (ErrorMode e as m) ->
      { model with prevMode = model.mode; mode = m}
      , Cmd.ofPort <| OutputLog(model.setting.errorLogPath, e.ToString())

    | _, SetMode m ->
      { model with prevMode = model.mode; mode = m }, Cmd.none

    | TitleMode, LongPress ->
      model, Cmd.ofMsg(SetMode SelectMode)

    | SelectMode, SetCategories x ->
      { model with categories = x }, Cmd.ofPort(LoadCatsCache x)

    | SelectMode, Release when model.categories.Length > 0 ->
      { model with
          categoryIndex =
            (model.categoryIndex + one) % model.categories.Length
      }, Cmd.none

    | SelectMode, LongPress when model.categories.Length > 0 ->
      monad {
        let! category = model.categories |> Array.tryItem model.categoryIndex

        let task =
          IO.downloadImages
            model.apiKey
            model.setting.theCatApiCacheDirectory
            category
            model.setting.requestLimit

        let ps = model.game.imagePaths |> Map.find (fst model.categories.[model.categoryIndex])
        let nextMode =
          if ps.Length > model.setting.gameStartFileCount then GameMode else WaitingMode

        return (model, Cmd.batch[
          Cmd.ofPort <| SelectedCategory task
          Cmd.ofMsg(SetMode nextMode)
        ])
      }
      |> Option.defaultValue (model, Cmd.none)

    | GameMode, GameMsg m ->
      model |> chain (GameModel.update m)

    | GameMode, Push ->
      model |> chain GameModel.push

    | GameMode, Release ->
      model |> chain GameModel.release
    
    | ErrorMode _, LongPress ->
      model, Cmd.ofMsg(SetMode model.prevMode)

    | TitleMode, _
    | SelectMode, _
    | GameMode, _
    | _, SetCategories _
    | WaitingMode, _
    | ErrorMode _, _
      -> model, Cmd.none