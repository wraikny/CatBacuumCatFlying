namespace Cbcf.View

[<AutoOpen>]
module Extension =
  type asd.Layer2D with
    member x.AddCamera(setting) = x.AddObject(new Camera(setting))


type ViewSetting = {
  theCatApiCacheDirectory: string
}


open Affogato
open Affogato.Helper
open wraikny.Tart.Helper
open wraikny.Tart.Core
open wraikny.MilleFeuille
open wraikny.MilleFeuille.Objects
open System.Reactive.Linq

open Cbcf


type MainScene(setting: Setting, viewSetting: ViewSetting) =
  inherit Scene()

  let initModel = Model.Init(setting)

  let messenger =
    Messenger.Create({seed = 0}, {
      init = initModel, Cmd.none
      update = Logic.Model.update
      view = id
    })

  let background =
    let rect =
      Rectangle.init zero setting.areaSize
      |>> map float32
      |> Rectangle.toRectangleShape

    new asd.GeometryObject2D(
      Shape = rect,
      Color = asd.Color(237, 233, 161, 255)
    )

  let backLayer = new asd.Layer2D()
  let layer = new asd.Layer2D()

  let player =
    new GameObjectView()
      //Texture = asd.Engine.Graphics.CreateTexture2D(viewSetting.player)
    //)

  let mutable lastModel = initModel

  do
    messenger.ViewModel.Add(fun x -> lastModel <- x)

    messenger
      .ViewModel
      .Where(fun x -> x.mode = Game)
      .Select(fun x -> x.game.player)
      .Add((player :> IUpdatee<_>).Update)

    messenger
      .ViewModel
      .Where(fun x -> x.mode = Game)
      .Select(fun x ->
        [ for a in x.game.flyingCats -> (a.Key, a.object) ]
      ).Subscribe(new ActorsUpdater<_, _, _>(layer, {
        create = fun() -> new GameObjectView()
        onError = raise
        onCompleted = ignore
      }))
      |> ignore

    //messenger.ViewModel.Add(fun x -> x.game.flyingCats.Length |> printfn "%d")


  override this.OnRegistered() =
    this.AddLayer(backLayer)
    this.AddLayer(layer)

    backLayer.AddObject(background)
    backLayer.AddCamera(setting)

    layer.AddObject(player)
    layer.AddCamera(setting)

    messenger.StartAsync()


  override this.OnUpdated() =
    
    messenger.Enqueue(Msg.Tick)

    asd.Engine.Keyboard.GetKeyState asd.Keys.Space
    |> function
    | asd.ButtonState.Push ->
      messenger.Enqueue(Push)
    | asd.ButtonState.Release ->
      messenger.Enqueue(Release)
    | _ -> ()
    
    messenger.NotifyView()
