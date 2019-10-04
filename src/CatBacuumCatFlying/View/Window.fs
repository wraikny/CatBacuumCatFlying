namespace Cbcf.View
open Affogato
open wraikny.MilleFeuille


type MenuSetting = {
  frameColor: asd.Color
  rectColor: asd.Color
  widthRate: float32
}

module Window =
  let create windowSize (menuSetting: MenuSetting) font =
    let ws = Vector2.toVector2DF windowSize
    let windowSetting =
      { UI.WindowSetting.Default(font) with
          animationFrame = 20u
          itemMargin = 15.0f
          itemAlignment = UI.WindowSetting.Center

          frameColor = menuSetting.frameColor
          //button = menuSetting.buttonColor
          //inputColor = menuSetting.inputColor
          //inputFocusColor = menuSetting.inputFocusColor

          rectColor = menuSetting.rectColor

          centerPositionRate = Vector2.init 0.5f 0.5f
          togglePositionRate = Vector2.init 0.5f 1.0f
          windowSize =
            UI.WindowSetting.WindowSize.FixWidth (ws.X * menuSetting.widthRate)
          toggleDirection = UI.WindowSetting.ToggleDirection.Y
          buttonSize = UI.WindowSetting.ButtonSize.AutoFit(asd.Vector2DF(10.0f, 10.0f), 0.8f)
      }


    let mouse =
      let mouse = new Input.CollidableMouse(5.0f, ColliderVisible = true)
      new UI.MouseButtonSelecter(mouse)


    mouse,
    new UI.MouseWindow(windowSetting, mouse,
        Position = ws * 0.5f
    )