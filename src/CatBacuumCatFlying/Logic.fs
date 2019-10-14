namespace Cbcf.Logic

open Cbcf
open Affogato
open Affogato.Helper
open Affogato.Collections
open FSharpPlus

module Random =
  let rand = System.Random()

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

  let inline private sizeChange (setting: GameSetting) (x: GameObject) =
    let minY = setting.ceilingHeight
    let maxY = setting.floorHeight - setting.playerSize.y

    let rate = Easing.calculateF Easing.Linear ((x.pos.y - minY)/(maxY-minY))
    let scale = setting.playerSizeRate * (1.0f - rate) +  rate
    let size = setting.playerSize .* scale
    { x with
        pos = { x.pos with x = setting.playerX - size.x * 0.5f }
        size = size
    }


  let inline update setting (x: GameObject) =
    x
    |> GameObject.move
    |> sizeChange setting
    |> clampY setting false


module FlyingCat =
  let inline outOfArea (x: FlyingCat) =
    if Rectangle2.right x.object.Area < 0.0f then None else Some x

  let inline update (x: FlyingCat) =
    x
    |> GameObject.move
    |> outOfArea


open Elmish

module GameModel =
  let inline private mapPlayer f (model: GameModel): GameModel =
    { model with player = f model.player }

  let inline private mapFlyingCat f (model: GameModel): GameModel =
    { model with flyingCats = Array.choose f model.flyingCats }

  let inline private calculate (port: Port) (model: GameModel) =
    let mutable score = zero
    let mutable hp = zero

    let nextFlyingCats = [|
      for x in model.flyingCats do
        if GameObject.inCollision model.player x then
          x.kind |> function
          | Score a -> score <- score + a
          | HP a -> hp <- hp + a

          port.addEffect(x)
        else
          yield x
    |]

    let newScore = model.scoreForLevelStage + score
    let newHp = hp + model.hp |> max zero |> min model.setting.hp
    let scoreLebelUp = (model.scoreForLevelStage + score) > model.setting.levelScoreStage

    { model with
        flyingCats = nextFlyingCats
        score = score + model.score
        hp = newHp
        scoreForLevelStage = if scoreLebelUp then zero else newScore
    } |> ifThen(scoreLebelUp) GameModel.LevelUp
    , (if newHp = zero then Cmd.ofMsg(SetMode GameOverMode) else Cmd.none)


  let private countup (model: GameModel): GameModel =
    let count = model.count + one
    { model with
        count = count
        generateCount = model.generateCount + one
    } |> ifThen (count % model.setting.levelFrameStage = 0u) GameModel.LevelUp


  let private addFlyingCatCheck (model: GameModel) =
    if model.generateCount >= model.generatePeriod then
      let stg = model.setting
      { model with generateCount = zero; nextId = model.nextId + one }, (
        let p = Random.rand.NextDouble()
        let q = Random.rand.NextDouble()
        let flag = Random.rand.Next(1, 10)
        let kind = flag |> function
          | 0 | 1 -> HP (0.2f * stg.hp * float32 p)
          | i when 1 < i && i < 9-> HP -(0.2f * stg.hp * float32 p)
          | 9 | 10 ->
            let p, _ = Utils.boxMullersMethod (float32 p) (float32 q)
            Score (0.5f * p * (float32 stg.levelScoreStage) |> abs |> uint32)
          | x -> failwithf "Unexpected flag %d" x

        let size = stg.flyingCatsSize
        let posY =
          let a = stg.ceilingHeight
          let b = stg.floorHeight - size.y
          (float32 <| Random.rand.NextDouble()) * (b - a) + a

        let currentPaths =
          model.imagePaths
          |> Map.tryFind model.category
          |> Option.defaultValue empty
        let imageIndex = Random.rand.Next(0, currentPaths.Length)

        let pos = Vector2.init stg.generateX (float32 posY)

        Msg.AddFlyingCat {
          kind = kind
          object = GameObject.Init(model.nextId, pos, size, Vector2.init -model.speeds.flyingCatsSpeed 0.0f, currentPaths.[imageIndex])
        }
        |> Cmd.ofMsg
      )
    else
      model, Cmd.none

  let andThen f (m, c) =
    let m, c' = f m
    m, Cmd.batch[c; c']

  let update (port: Port) (msg: GameMsg) (model: GameModel) =
    msg |> function
    | Tick ->
      let stg = model.setting

      model
      |> countup
      |> mapPlayer (Player.update stg)
      |> mapFlyingCat (FlyingCat.update)
      |> calculate port
      |> andThen addFlyingCatCheck

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

open System.Threading
open System.Collections.Generic

module Model =
  let inline chain f m =
    let g, c = f m.game
    set g m, c

  let inline init arg =
    Model.Init arg, Cmd.none

  let loadCatsCache (setting: Setting) (categories: _ []) dispatch =
    async {
      try
        let ctx = SynchronizationContext.Current
        do! Async.SwitchToThreadPool()

        let msgs = List<_>(categories.Length)
        for (c, s) in categories do
          let path = sprintf "%s/%s" setting.theCatApiCacheDirectory s

          if System.IO.Directory.Exists(path) then
            System.IO.Directory.GetFiles(path)
            |> fun x -> AddImagePaths(c, x)
            |> msgs.Add

          else
            System.IO.Directory.CreateDirectory(path)
            |> ignore

        do! Async.SwitchToContext(ctx)
        msgs |> iter(dispatch)

        printfn "Finished LoadCatsCache"
       with e ->
        printfn "%A" e
    }

  //let selectedCategory f dispatch =
  //  async {
  //    try
  //      do! f (AddImagePaths >> dispatch)
  //      printfn "finished selectedcategory"
  //    with e ->
  //      printfn "%A" e
  //  }

  let logfileLock = System.Object()

  let outputLog filepath t =
    async {
      try
        lock logfileLock <| fun _ ->
          System.IO.File.AppendAllText(filepath, t)
        printfn "Finished OutputLog"
      with e ->
        printfn "%A" e
    }


  let update (msg: Msg) (model: Model) =
    (model.mode, msg) |> function
    | _, AddImagePaths (c, ss) ->
      let xs =
        model.game.imagePaths
        |> Map.tryFind c
        |> Option.defaultValue empty
        |> Array.append ss

      let newMap = Map.add c xs model.game.imagePaths

      { model with
          game = { model.game with imagePaths = newMap }
      }, (
        //let ps = newMap |> Map.find (fst model.categories.[model.categoryIndex])
        if model.mode = WaitingMode then
          Cmd.ofMsg(SetMode GameMode)
        else Cmd.none
      )

    | _, SetMode SelectMode ->
      { model with prevMode = model.mode; mode = SelectMode }
      , async {
        let! child =
          IO.loadCategoryAsync model.apiKey
          |> Async.Catch
          |> Async.StartChild

        match! child with
          | Choice1Of2 [||] ->
            return SetMode(ErrorMode <| System.Exception("Categories list is empty"))
          | Choice1Of2 x ->
            return SetCategories x
          | Choice2Of2 e ->
            return SetMode(ErrorMode e)

      } |> Cmd.OfAsyncImmediate.result

    | SelectMode, SetMode GameMode
    | WaitingMode, SetMode GameMode ->
      let category = fst model.categories.[model.categoryIndex]
      let ps = model.game.imagePaths |> Map.tryFind category |> Option.defaultValue empty
      let cmd =
        let i = Random.rand.Next(0, ps.Length - 1)
        SetPlayerImage ps.[i] |> GameMsg |> Cmd.ofMsg

      { model with
          prevMode = model.mode
          mode = GameMode
          game = { model.game with category = category }
      }, cmd

    | _, SetMode (ErrorMode e as m) ->
      outputLog (model.setting.errorLogPath) (string e)
      |> Async.Start

      { model with prevMode = model.mode; mode = m}, Cmd.none

    | _, SetMode m ->
      { model with prevMode = model.mode; mode = m }, Cmd.none

    | TitleMode, LongPress ->
      model, Cmd.ofMsg(SetMode SelectMode)

    | SelectMode, SetCategories categories ->
      let sub dispatch =
        loadCatsCache model.setting categories dispatch
        |> Async.StartImmediate

      { model with
          categories = categories
      }, Cmd.ofSub sub

    | SelectMode, Release when model.categories.Length > 0 ->
      { model with
          categoryIndex =
            (model.categoryIndex + one) % model.categories.Length
      }, Cmd.none

    | SelectMode, LongPress when model.categories.Length > 0 ->
      monad {
        let! category = model.categories |> Array.tryItem model.categoryIndex

        let ps =
          model.game.imagePaths
          |> Map.tryFind (fst model.categories.[model.categoryIndex])
          |> Option.defaultValue empty
        
        if ps.Length >= model.setting.gameStartFileCount then 
          return model, Cmd.ofMsg(SetMode GameMode)
        else
          return model, Cmd.batch[
            Cmd.ofMsg(SetMode WaitingMode)
            Cmd.OfAsyncImmediate.perform(fun() -> async {
              let! child =
                IO.downloadImages
                  model.apiKey
                  model.setting.theCatApiCacheDirectory
                  category
                  model.setting.requestLimit
                |> Async.StartChild
              return! child
            }) () (AddImagePaths)
          ]
      }
      |> Option.defaultValue (model, Cmd.none)

    | GameMode, GameMsg m ->
      model |> chain (GameModel.update model.port m)

    | GameMode, Push ->
      model |> chain GameModel.push

    | GameMode, Release ->
      model |> chain GameModel.release
    
    //| ErrorMode _, LongPress ->
    //  model, Cmd.ofMsg(SetMode model.prevMode)

    | PauseMode, Release ->
      model, Cmd.ofMsg(SetMode GameMode)

    | PauseMode, LongPress ->
      model.port.clear()
      { model with
          categoryIndex = zero
          game = GameModel.Restart(model.game)
      }
      , Cmd.ofMsg(SetMode TitleMode)

    | GameOverMode, LongPress ->
      model, Cmd.ofMsg(SetMode TitleMode)

    | PauseMode, _
    | TitleMode, _
    | SelectMode, _
    | GameMode, _
    | _, SetCategories _
    | WaitingMode, _
    | GameOverMode, _
    | ErrorMode _, _
      -> model, Cmd.none