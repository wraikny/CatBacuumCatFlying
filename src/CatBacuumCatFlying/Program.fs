module Cbcf.Program
open System
open Affogato

let setting = {
  areaSize = Vector2.init 1600 900
  playerSize = Vector2.init 128.0f 256.0f
  flyingCatsSize = Vector2.init 64.0f 32.0f

  playerX = 200.0f

  ceilingHeight = 100.0f
  floorHeight = 800.0f

  initSpeeds = {
    bacuumSpeed = 8.0f
    fallingSpeed = 8.0f
    flyingCatsSpeed = 4.0f
  }
  diffSpeeds = {
    bacuumSpeed = 0.2f
    fallingSpeed = 0.2f
    flyingCatsSpeed = 0.2f
  }

  levelScoreStage = 100u
  levelFrameStage = 60u
}

open Cbcf.View

let viewSetting = {
  player = "animal_stand_neko.png"
}

[<STAThread; EntryPoint>]
let main _ =
  asd.Engine.Initialize("猫バキューム猫飛んでいる", 800, 600, asd.EngineOption())
  |> ignore

  #if DEBUG
  asd.Engine.File.AddRootDirectory("Resources")
  #endif

  asd.Engine.ChangeScene(new MainScene(setting, viewSetting))

  while asd.Engine.DoEvents() do asd.Engine.Update()

  asd.Engine.Terminate()

  0
