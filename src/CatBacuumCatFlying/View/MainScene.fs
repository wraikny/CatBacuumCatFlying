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
  textSize: int
  lineWidth: float32

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
    Messenger.Create({seed = 0}, {
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
  let layer = new asd.Layer2D()
  let uiLayer = new asd.Layer2D()

  let player =
    new GameObjectView()

  let mutable lastModel = initModel

  do
    messenger.Msg.Add(printfn "%A")
    messenger.ViewModel.Add(fun x -> lastModel <- x)

    messenger
      .ViewModel
      .Where(fun x -> x.mode = GameMode)
      .Select(fun x -> x.game.player)
      .Add((player :> IUpdatee<_>).Update)

    messenger
      .ViewModel
      .Where(fun x -> x.mode = GameMode)
      .Select(fun x ->
        [ for a in x.game.flyingCats -> (a.Key, a.object) ]
      ).Subscribe(new ActorsUpdater<_, _, _>(layer, {
        create = fun() -> new GameObjectView()
        onError = raise
        onCompleted = ignore
      }))
      |> ignore


  let createFont size =
    asd.Engine.Graphics.CreateDynamicFont(
      viewSetting.fontPath, size, asd.Color(0, 0, 0, 255), 0, asd.Color()
    )

  let titleFont = createFont viewSetting.titleSize
  let headerFont = createFont viewSetting.headerSize
  let textFont = createFont viewSetting.textSize

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
            | Text s -> UI.Text s
            | Line -> UI.Rect(viewSetting.lineWidth, 0.8f)
          )

        if window.IsToggleOn && contents.IsEmpty then
          window.Toggle(false, fun() ->
            layer.IsUpdated <- true
          )
        elif not window.IsToggleOn && not contents.IsEmpty then
          layer.IsUpdated <- false
          window.Toggle(true)
      )



  override this.OnRegistered() =
    messenger.StartAsync()

    this.AddLayer(backLayer)
    this.AddLayer(layer)
    this.AddLayer(uiLayer)

    backLayer.AddObject(background)
    backLayer.AddCamera(gameSetting)

    layer.AddCamera(gameSetting)
    layer.AddObject(player)

    uiLayer.AddObject(window)

    this.AddCoroutine(seq {
      let mutable holdCount = 0
      while true do
        asd.Engine.Keyboard.GetKeyState asd.Keys.Space
        |> function
        | asd.ButtonState.Push ->
          messenger.Enqueue(Push)
        | asd.ButtonState.Release ->
          messenger.Enqueue(Release)
          holdCount <- 0
        | asd.ButtonState.Hold ->
          holdCount <- holdCount + one
          if holdCount > viewSetting.longPressFrame then
            holdCount <- 0
            messenger.Enqueue(LongPress)
        | _ -> ()

        yield()
    })


  override this.OnUpdated() =
    if lastModel.mode = GameMode then
      messenger.Enqueue(Msg.Tick)
    
    messenger.NotifyView()
