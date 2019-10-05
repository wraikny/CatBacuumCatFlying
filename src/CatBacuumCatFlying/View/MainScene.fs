namespace Cbcf.View

[<AutoOpen>]
module Extension =
  type asd.Layer2D with
    member x.AddCamera(setting) = x.AddObject(new Camera(setting))


type ViewSetting = {
  apiKeyPath: string

  menuSetting: MenuSetting
  fontPath: string
  titleSize: int
  headerSize: int
  largeSize: int
  textSize: int
  lineWidth: float32

  longPressFrameWait: int
  longPressFrame: int
}


open Affogato
open Affogato.Helper
open wraikny.Tart.Helper
open wraikny.Tart.Core
open wraikny.MilleFeuille
open wraikny.MilleFeuille.Objects
open System.Reactive.Linq

open Cbcf
open Cbcf.ViewModel


type MainScene(setting: Setting, gameSetting: GameSetting, viewSetting: ViewSetting) =
  inherit Scene()

  let initModel =
    let apiKey = (IO.Altseed.loadString viewSetting.apiKeyPath).Trim()
    Model.Init(setting, gameSetting, apiKey)

  let messenger =
    Messenger.Create({seed = System.Random().Next()}, {
      init = initModel, Cmd.none
      update = Logic.Model.update
      view = id
    })

  let background =
    let rect =
      Rectangle.init zero gameSetting.areaSize
      |>> map float32
      |> Rectangle.toRectangleShape

    new asd.GeometryObject2D(
      Shape = rect,
      Color = asd.Color(237, 233, 161, 255)
    )

  let backLayer = new asd.Layer2D()
  let layer = new asd.Layer2D(IsUpdated = false, IsDrawn = false)
  let uiLayer = new asd.Layer2D()
  let longPressArcLayer = new asd.Layer2D()

  let player =
    new GameObjectView()

  let hpObj = new asd.GeometryObject2D()

  let mutable lastModel = initModel

  do
    messenger.OnError.Add(printfn "%A")
    //messenger.Msg.Add(printfn "Msg : %A")
    messenger.ViewModel.Add(fun x -> lastModel <- x)

    messenger.ViewModel
      .Where(fun x -> x.mode = GameMode)
      .Select(fun x -> x.game.player)
      .Add((player :> IUpdatee<_>).Update)

    messenger.ViewModel
      .Where(fun x -> x.mode = GameMode)
      .Select(fun x ->
        [ for a in x.game.flyingCats -> (a.Key, a.object) ]
      ).Subscribe(new ActorsUpdater<_, _, _>(layer, true, {
        create = fun() -> new FlyingCatView()
        onError = raise
        onCompleted = ignore
      }))
      |> ignore

    let areaX = float32 gameSetting.areaSize.x
    let areaY = float32 gameSetting.areaSize.y
    let areaHeight = areaY - gameSetting.floorHeight

    let hpArea =
      asd.RectF(
        areaX * 0.1f,
        gameSetting.floorHeight + areaHeight * 0.5f,
        areaX * 0.8f,
        areaHeight * 0.1f
      )

    let rect = new asd.RectangleShape(DrawingArea=hpArea)

    let hpBar =
      new asd.GeometryObject2D(
        Shape = rect,
        Color = asd.Color(255, 0, 0, 255)
    )

    hpObj.Shape <- new asd.RectangleShape(DrawingArea=hpArea)

    messenger.ViewModel
      .Add(fun x ->
        if x.mode = GameMode then
          let rate = x.game.hp / gameSetting.hp
          let w = areaX * 0.8f * rate
          rect.DrawingArea <-
            asd.RectF(
              hpArea.Position, asd.Vector2DF(w, hpArea.Size.Y)
            )
      )
    hpObj.AddDrawnChildWithoutColor(hpBar)


      
      


  let createFont size outLineSize =
    asd.Engine.Graphics.CreateDynamicFont(
      viewSetting.fontPath, size, asd.Color(0, 0, 0, 255), outLineSize, asd.Color(0, 0, 0, 255)
    )

  let titleFont = createFont viewSetting.titleSize 0
  let headerFont = createFont viewSetting.headerSize 0
  let textFont = createFont viewSetting.textSize 0
  let largeFont = createFont viewSetting.largeSize 1

  let mouse, window =
    Window.create
      (asd.Engine.WindowSize.To2DF().ToVector2())
      viewSetting.menuSetting
      textFont

  do
    messenger.ViewModel
      .Select(view)
      .Add(fun contents ->
        window.UIContents <-
          contents |> List.map(function
            | Title s ->
              UI.TextWith(s, titleFont)
            | Header s ->
              UI.TextWith(s, headerFont)
            | Large s ->
              UI.TextWith(s, largeFont)
            | Text s -> UI.Text s
            | Line -> UI.Rect(viewSetting.lineWidth, 0.8f)
          )

        if window.IsToggleOn && contents.IsEmpty then
          window.Toggle(false, fun() ->
            layer.IsUpdated <- true
            layer.IsDrawn <- true
          )
        elif not window.IsToggleOn && not contents.IsEmpty then
          layer.IsUpdated <- false
          layer.IsDrawn <- false
          window.Toggle(true)
      )

  let scoreObj = new asd.TextObject2D(Font = largeFont)
  let fpsText = new asd.TextObject2D(Font = largeFont)
  do
    fpsText.AddOnUpdateEvent(fun() ->
      fpsText.Text <- sprintf "FPS: %d" <| int asd.Engine.CurrentFPS
      let size = fpsText.Font.HorizontalSize(fpsText.Text)
      fpsText.Position <-
        asd.Vector2DF(float32 asd.Engine.WindowSize.X - float32 size.X, fpsText.Position.Y)
    )
    
    messenger.ViewModel
      .Add(fun x ->
        if x.mode = GameMode then
          scoreObj.Text <- sprintf " Level = %d, Score = %d" x.game.level x.game.score
          let size = scoreObj.Font.HorizontalSize(scoreObj.Text)
          scoreObj.Position <-
            asd.Vector2DI(
              asd.Engine.WindowSize.X - size.X
              , 0).To2DF()
          fpsText.Position <-
            asd.Vector2DF(fpsText.Position.X, float32 size.Y)
      )

  let longPressArc =
    let ws = asd.Engine.WindowSize.To2DF()
    let m = 0.5f * min ws.X ws.Y
    new LongPressCircle(m * 0.6f, m * 0.8f)

  do
    let logfileLock = System.Object()
    messenger.ViewMsg.Add(fun x ->
      printfn "Port: %A" x
      x |>function
      | LoadCatsCache categories ->
        async {
          try
            for (c, s) in categories do
              let path = sprintf "%s/%s" setting.theCatApiCacheDirectory s
              if System.IO.Directory.Exists(path) then
                System.IO.Directory.GetFiles(path)
                |> fun x -> AddImagePaths(c, x) |> messenger.Enqueue
              else
                System.IO.Directory.CreateDirectory(path)
                |> ignore
            printfn "Finished LoadCatsCache"
           with e ->
            printfn "%A" e
        } |> Async.Start
      | SelectedCategory f ->
        async {
          try
            do! f (fun (c, s) -> AddImagePaths(c, [|s|]) |> messenger.Enqueue)
            printfn "Finished SelectedCategory"
          with e ->
            printfn "%A" e
        } |> Async.Start
      | OutputLog (filepath, t) ->
        async {
          try
            lock logfileLock <| fun _ ->
              System.IO.File.AppendAllText(filepath, t)
            printfn "Finished OutputLog"
          with e ->
            printfn "%A" e
        } |> Async.Start
    )

  override this.OnRegistered() =
    messenger.StartAsync()

    this.AddLayer(backLayer)
    this.AddLayer(layer)
    this.AddLayer(uiLayer)
    this.AddLayer(longPressArcLayer)

    backLayer.AddObject(background)
    backLayer.AddCamera(gameSetting)

    layer.AddCamera(gameSetting)
    layer.AddObject(hpObj)
    layer.AddObject(player)

    uiLayer.AddObject(scoreObj)
    uiLayer.AddObject(fpsText)
    uiLayer.AddObject(window)
    longPressArcLayer.AddObject(longPressArc)

    this.AddCoroutine(seq {
      let mutable holdCount = 0
      let wf = viewSetting.longPressFrameWait
      let f = viewSetting.longPressFrame

      while true do
        asd.Engine.Keyboard.GetKeyState asd.Keys.Space
        |> function
        | asd.ButtonState.Push ->
          messenger.Enqueue(Push)

        | asd.ButtonState.Release ->
          if holdCount <= wf then
            messenger.Enqueue(Release)
          else
            holdCount <- 0
            longPressArc.SetRate(0.0f)

        | asd.ButtonState.Hold ->
          lastModel.mode |> function
          | GameMode | WaitingMode -> ()
          | _ when holdCount <= f + wf ->
            holdCount <- holdCount + one

            if holdCount > wf then
              longPressArc.SetRate(
                float32 (holdCount - wf) / float32 f
              )
            if holdCount > f + wf then
              holdCount <- 0
              longPressArc.SetRate(0.0f)
              messenger.Enqueue(LongPress)

          | _ -> ()
        | _ -> ()

        yield()
    })


  override this.OnUpdated() =
    if lastModel.mode = GameMode then
      messenger.Enqueue(Msg.Tick)
    
    messenger.NotifyView()
