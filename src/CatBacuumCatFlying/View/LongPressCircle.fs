namespace Cbcf.View

type LongPressCircle(r1, r2) =
  inherit asd.GeometryObject2D(Position = asd.Engine.WindowSize.To2DF() * 0.5f)

  let arc =
    new asd.ArcShape(
      NumberOfCorners = 100,
      InnerDiameter = r1 * 2.0f,
      OuterDiameter = r2 * 2.0f
    )
  do
    base.Shape <- arc
    base.Color <- asd.Color(255, 165, 163, 200)

  member __.SetRate(t) =
    let e = t * 100.0f |> int
    arc.EndingCorner <- e