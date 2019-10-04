module Cbcf.Program
open System

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
