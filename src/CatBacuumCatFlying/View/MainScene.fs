namespace Cbcf.View

[<AutoOpen>]
module Extension =
  type asd.Layer2D with
    member x.AddCamera(setting) = x.AddObject(new Camera(setting))


type ViewSetting = {
  apiKeyPath: string

  bacuumTexturePath: string

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
open wraikny.MilleFeuille
open wraikny.MilleFeuille.Updater
open System.Reactive
open System.Reactive.Linq
open System.Threading
open Cbcf
open Cbcf.ViewModel
open Elmish


type MainScene(setting: Setting, gameSetting: GameSetting, viewSetting: ViewSetting) =
  inherit Scene()

  //let notifier = new Subjects.Subject<_>()

  //let mutable dispatch = Unchecked.defaultof<_>
  //let mutable lastModel = Unchecked.defaultof<_>
  let messenger =
    let apiKey = (IO.Altseed.loadString viewSetting.apiKeyPath).Trim()

    let init() = Logic.Model.init(setting, gameSetting, apiKey)

    Program.mkProgram init Logic.Model.update (fun m _ -> m)
    |> Program.withErrorHandler(printfn "%A")
    #if DEBUG
    |> Program.withTrace(fun msg _ ->
      if msg <> Msg.Tick then
        printfn "%A" msg
    )
    #endif
    |> Messenger.create


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
    new GameObjectView(DrawingPriority = 2)

  let hpObj = new asd.GeometryObject2D()

  let bacuumObj =
    let tex = asd.Engine.Graphics.CreateTexture2D(viewSetting.bacuumTexturePath)
    new asd.TextureObject2D(
      Position = asd.Vector2DF(gameSetting.playerX, gameSetting.ceilingHeight),
      Texture = tex,
      CenterPosition = tex.Size.To2DF() * 0.5f,
      DrawingPriority = 3
    )

  do
    messenger.View
      .Add(fun x ->
        if x.mode = GameMode then
          (player :> IUpdatee<_>).Update(x.game.player)
      )

    messenger.View
      .Where(fun x -> x.mode = GameMode)
      .Select(fun x ->
        [ for a in x.game.flyingCats -> (a.Key, a.object) ]
      ).Subscribe(new ActorsUpdater<_, _, _>(layer, fun() -> new FlyingCatView(DrawingPriority = 1)))
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

    messenger.View
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

  let _, window =
    Window.create
      (asd.Engine.WindowSize.ToVector2F())
      viewSetting.menuSetting
      textFont

  do
    messenger.View
      .Select(view)
      .Add(fun contents ->
        (window.IsToggleOn, contents.IsEmpty)
        |> function
        | true, true ->
            window.Toggle(false, fun() ->
              layer.IsUpdated <- true
              layer.IsDrawn <- true
            )
        | x, false ->
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

          if not x then
            layer.IsUpdated <- false
            layer.IsDrawn <- false
            window.Toggle(true)

        | _, _ -> ()
          
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
    
    messenger.View
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
    messenger.Start()

  override this.OnRegistered() =

    this.AddLayer(backLayer)
    this.AddLayer(layer)
    this.AddLayer(uiLayer)
    this.AddLayer(longPressArcLayer)

    backLayer.AddObject(background)
    backLayer.AddCamera(gameSetting)

    layer.AddCamera(gameSetting)
    layer.AddObject(hpObj)
    layer.AddObject(player)
    layer.AddObject(bacuumObj)

    uiLayer.AddObject(scoreObj)
    uiLayer.AddObject(fpsText)
    uiLayer.AddObject(window)
    longPressArcLayer.AddObject(longPressArc)

    this.AddCoroutineAsParallel(seq {
      let mutable holdCount = 0
      let wf = viewSetting.longPressFrameWait
      let f = viewSetting.longPressFrame

      let isPush(key) =
        asd.Engine.Keyboard.GetKeyState key = asd.ButtonState.Push

      while true do
        messenger.LastModel.mode |> function
        | GameMode when isPush(asd.Keys.Escape) ->
          messenger.Enqueue(SetMode PauseMode)
        | PauseMode when isPush(asd.Keys.Escape) || isPush(asd.Keys.Space) ->
          messenger.Enqueue(SetMode GameMode)
        | _ -> ()

        asd.Engine.Keyboard.GetKeyState asd.Keys.Space
        |> function
        | asd.ButtonState.Push ->
          messenger.Enqueue(Push)

        | asd.ButtonState.Release ->
          if holdCount <= wf then
            messenger.Enqueue(Release)
          else
            longPressArc.SetRate(0.0f)

          holdCount <- 0

        | asd.ButtonState.Hold ->
          if messenger.LastModel.mode.EnabledLongPress && (holdCount <= f + wf) then
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

        yield()
    })


  override this.OnUpdated() =
    if messenger.LastModel.mode = GameMode then
      messenger.Enqueue(Msg.Tick)
    
    //messenger.NotifyView()
