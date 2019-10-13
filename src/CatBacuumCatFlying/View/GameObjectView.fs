namespace Cbcf.View

open wraikny.MilleFeuille
open wraikny.MilleFeuille.Updater
open Affogato
open Affogato.Helper
open Cbcf

type GameObjectView() =
  inherit asd.GeometryObject2D()

  let textureView = new asd.TextureObject2D()
  let rect = new asd.RectangleShape()
  let sizeView = new asd.GeometryObject2D(Shape = rect, Color = asd.Color(0, 250, 0, 50))

  do
    base.AddDrawnChildWithAll(textureView)
    //base.AddDrawnChildWithoutColor(sizeView)

  let lastPos: float32 Vector2 ref = ref zero
  let lastSize: float32 Vector2 ref = ref zero
  let lastPath: string ref = ref ""

  let mutable texSize = None

  let update x y f =
    if !x <> y then
      x := y
      f(y)

  member this.Texture
    with get() = textureView.Texture
    and set(x) =
      textureView.Texture <- x
      if !lastSize <> zero then
        let texSize' = x.Size.To2DF()
        texSize <- Some texSize'
        textureView.Scale <- (Vector2.toVector2DF !lastSize) / texSize'

  
  member this.Update(x: GameObject) =
    let area = x.Area

    update lastPos area.position <| fun x ->
      this.Position <- Vector2.toVector2DF x

    update lastSize area.size <| fun x ->
      let size = Vector2.toVector2DF x
      rect.DrawingArea <- asd.RectF(asd.Vector2DF(), size)
      texSize
      |> Option.iter(fun x ->
        textureView.Scale <- size / x
      )

    update lastPath x.imagePath <| fun x ->
      if x <> null && System.IO.File.Exists(x) then
        this.Texture <- asd.Engine.Graphics.CreateTexture2D(x)

  interface IUpdatee<GameObject> with
    member this.Update(x) = this.Update(x)


type FlyingCatView() =
  inherit GameObjectView()

  interface IUpdatee<FlyingCat> with
    member this.Update(x) =
      base.Update(x.object)

      let color = x.kind |> function
        | HP x when x > 0.0f -> asd.Color(0, 255, 0, 255)
        | HP _ -> asd.Color(255, 0, 0, 255)
        | Score _ -> asd.Color(0, 0, 255, 255)
      this.Color <- color