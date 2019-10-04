module Cbcf.Program
open System
open Affogato

let setting = {
  requestLimit = 50
  theCatApiCacheDirectory = "TheCatApiCache"
  gameStartFileCount = 10

  errorLogPath = "Log.txt"

  title = "Cat Bacuum Cat Flying"
}

let gameSetting = {
  areaSize = Vector2.init 1600 900
  playerSize = Vector2.init 128.0f 256.0f
  flyingCatsSize = Vector2.init 128.0f 64.0f

  playerX = 200.0f

  ceilingHeight = 100.0f
  floorHeight = 800.0f

  initSpeeds = {
    bacuumSpeed = 8.0f
    fallingSpeed = 8.0f
    flyingCatsSpeed = 8.0f
  }
  diffSpeeds = {
    bacuumSpeed = 0.2f
    fallingSpeed = 0.2f
    flyingCatsSpeed = 0.2f
  }

  hp = 100.0f

  generatePerMin = 30.0f, 2.0f
  generateX = 3000.0f

  levelScoreStage = 100u
  levelFrameStage = 60u * 10u
}

open Cbcf.View

let viewSetting = {
  apiKeyPath = "apiKey.txt"

  menuSetting = {
    frameColor = asd.Color(3, 252, 244, 255)
    rectColor = asd.Color(115, 3, 252, 255)
  
    widthRate = 0.8f
  }

  fontPath = "mplus-1c-regular.ttf"

  titleSize = 40
  headerSize = 30
  textSize = 20
  lineWidth = 5.0f

  longPressFrame = 180
}

[<STAThread; EntryPoint>]
let main _ =
  asd.Engine.Initialize(setting.title, 800, 450, asd.EngineOption())
  |> ignore

  #if DEBUG
  asd.Engine.File.AddRootDirectory("Resources")
  #endif

  asd.Engine.ChangeScene(new MainScene(setting, gameSetting, viewSetting))

  while asd.Engine.DoEvents() do asd.Engine.Update()

  asd.Engine.Terminate()

  0
