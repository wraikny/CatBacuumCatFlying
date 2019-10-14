namespace Cbcf.View

open Cbcf

open Affogato
open Affogato.Helper
open wraikny.MilleFeuille
open wraikny.MilleFeuille.Updater

type HitEffect(scaleDiff: float32, frame) =
  inherit Layer2DComponent<asd.Layer2D>("HitEffect")

  let alpha = 200

  let pooling =
    ObjectsPool(
      (fun() ->
        new asd.TextureObject2D()
      ), 4)

  member __.Clear() =
    pooling.Clear()
    for obj in base.Owner.Objects do obj.Dispose()


  member this.AddEffect(flyingCat: FlyingCat) =
    let area = flyingCat.object.Area
    let path = flyingCat.object.imagePath

    let obj = pooling.Pop()

    if isNull obj.Layer then
      this.Owner.AddObject(obj)

    obj.Color <- asd.Color(255, 255, 255, alpha)
    obj.IsUpdated <- true
    obj.IsDrawn <- true

    obj.Texture <- asd.Engine.Graphics.CreateTexture2D(path)

    let texSize = obj.Texture.Size.To2DF()
    obj.CenterPosition <- texSize * 0.5f

    obj.Position <-
      area
      |> Rectangle.centerPosition
      |> Vector2.toVector2DF

    let scale =
      area.size
      |> Vector2.toVector2DF
      |> devidedBy texSize

    obj.Scale <- scale

    obj.AddCoroutineAsParallel(seq {
      for i in 1..frame ->
        let a = Easing.calculate Easing.OutSine frame i
        obj.Scale <- scale + asd.Vector2DF(1.0f, 1.0f) * scaleDiff * a

        let b = Easing.calculate Easing.InSine frame i
        obj.Color <- asd.Color(255, 255, 255, int <| (1.0f - b) * (float32 alpha))

      obj.IsUpdated <- false
      obj.IsDrawn <- false
      pooling.Push(obj)
      yield()

    })