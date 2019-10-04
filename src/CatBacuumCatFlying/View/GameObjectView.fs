namespace Cbcf.View

open wraikny.MilleFeuille
open Affogato
open Affogato.Helper
open wraikny.Tart.Helper
open Cbcf

type GameObjectView() =
  inherit asd.GeometryObject2D()

  let textureView = new asd.TextureObject2D()
  let rect = new asd.RectangleShape()
  let sizeView = new asd.GeometryObject2D(Shape = rect, Color = asd.Color(0, 250, 0, 50))

  do
    base.AddDrawnChildWithoutColor(textureView)
    base.AddDrawnChildWithoutColor(sizeView)

  let lastPos: float32 Vector2 ref = ref zero
  let lastSize: float32 Vector2 ref = ref zero

  let update x y f =
    if !x <> y then
      x := y
      f(y)

  member this.Texture
    with get() = textureView.Texture
    and set(x) =
      textureView.Texture <- x
      if !lastSize <> zero then
        textureView.Scale <- (Vector2.toVector2DF !lastSize) / x.Size.To2DF()

  
  member this.Update(x: GameObject) =
    let area = x.Area

    update lastPos area.position <| fun x ->
      this.Position <- Vector2.toVector2DF x

    update lastSize area.size <| fun x ->
      let size = Vector2.toVector2DF x
      rect.DrawingArea <- asd.RectF(asd.Vector2DF(), size)
      if textureView.Texture <> null then
        textureView.Scale <- size / textureView.Texture.Size.To2DF()

  interface IUpdatee<GameObject> with
    member this.Update(x) = this.Update(x)


type FlyingCatView(healTex, damageTex, scoreTex) =
  inherit GameObjectView()

  let lastKind = ref -1

  let update x y f =
    if !x <> y then
      x := y
      f(y)

  interface IUpdatee<FlyingCat> with
    member this.Update(x) =
      base.Update(x.object)

      let flag, tex = x.kind |> function
        | HP x when x > 0.0f -> 0, healTex
        | HP _ -> 1, damageTex
        | Score _ -> 2, scoreTex

      update lastKind flag <| fun _ ->
        this.Texture <- tex
