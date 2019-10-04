namespace Cbcf.View
open wraikny.MilleFeuille

type Camera(setting: Cbcf.Setting) =
  inherit asd.CameraObject2D()

  let areaSize = setting.areaSize |> Vector2.toVector2DI

  let ws = asd.Engine.WindowSize.To2DF()
  let dst =
    let src = areaSize.To2DF()
    let r = ws / src
    if r.X > r.Y then
      // 横長
      let w = ws.Y * src.X / src.Y
      asd.RectF((ws.X - w) * 0.5f, 0.0f, w, ws.Y).ToI()
    else
      // 縦長
      let h = ws.X * src.Y / src.X
      asd.RectF(0.0f, (ws.Y - h) * 0.5f, ws.X, h).ToI()

  do
    base.Src <- asd.RectI(asd.Vector2DI(), areaSize)
    base.Dst <- dst