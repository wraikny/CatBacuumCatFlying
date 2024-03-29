﻿namespace Cbcf.View

[<AutoOpen>]
module Extension =
  type asd.Layer2D with
    member x.AddCamera(setting) = x.AddObject(new Camera(setting))


type ViewSetting = {
  apiKeyPath: string

  bacuumTexturePath: string

  menuSetting: MenuSetting
  fontPath: string
  titleSize: int
  headerSize: int
  largeSize: int
  textSize: int
  smallTextSize: int
  lineWidth: float32

  longPressFrameWait: int
  longPressFrame: int

  hitEffect: HitEffectSetting

  bacuumSE: string
  bacuumVolume: float32
  bacuumFadeSec: float32

  medicalSE: string
  coinSE: string
  enterSE: string
  clickSE: string
}


open Affogato
open Affogato.Helper
open wraikny.MilleFeuille
open wraikny.MilleFeuille.Updater
open System.Reactive
open System.Reactive.Linq
open System.Threading
open Cbcf
open Cbcf.Update
open Cbcf.ViewModel
open Elmish
open Elmish.Reactive


type MainScene(bgmId: int, setting: Setting, gameSetting: GameSetting, viewSetting: ViewSetting) =
  inherit Scene()

  let scoreObj = new asd.TextObject2D(Text = " ")
  let hitEffect = new HitEffect(viewSetting.hitEffect)

  let messenger =
    let apiKey = (IO.Altseed.loadString viewSetting.apiKeyPath).Trim()

    let bacuumSE = asd.Engine.Sound.CreateSoundSource(viewSetting.bacuumSE, false)
    bacuumSE.IsLoopingMode <- true
    let mutable bacuumSEId = None

    let port = {
      addEffect = hitEffect.AddEffect
      clear = fun() ->
        scoreObj.Text <- " "
        hitEffect.Clear()

      toggleBacuum = function
        | true ->
          let seid = asd.Engine.Sound.Play(bacuumSE)
          bacuumSEId <- Some seid
          asd.Engine.Sound.SetVolume(seid, viewSetting.bacuumVolume)
          asd.Engine.Sound.FadeIn(seid, viewSetting.bacuumFadeSec)

        | false ->
          bacuumSEId |> iter(fun seid ->
            asd.Engine.Sound.FadeOut(seid, viewSetting.bacuumFadeSec)
            bacuumSEId <- None
          )

      pause = fun() ->
        asd.Engine.Sound.Pause(bgmId)
        bacuumSEId |> iter asd.Engine.Sound.Pause

      resume = fun() ->
        asd.Engine.Sound.Resume(bgmId)
        bacuumSEId |> iter asd.Engine.Sound.Resume

      //playSE = fun path ->
      //  let source = asd.Engine.Sound.CreateSoundSource(path, true)
      //  let id = asd.Engine.Sound.Play
      //  ()
      playSE = fun kind ->
        let inline play path =
          asd.Engine.Sound.CreateSoundSource(path, true)
          |> asd.Engine.Sound.Play
        
        kind |> function
        | Coin -> viewSetting.coinSE
        | Medical -> viewSetting.medicalSE
        | Enter -> viewSetting.enterSE
        | Click -> viewSetting.clickSE
        |> play
        |> ignore
    }

    let init() = Update.Model.init(setting, gameSetting, apiKey, port)

    Program.mkProgram init Update.Model.update (fun m _ -> m)
    #if DEBUG
    |> Program.withTrace(fun msg _ ->
      if msg <> Msg.Tick then
        printfn "%A" msg
    )
    #endif
    |> Messenger.create

  do
    messenger.OnError.Add(fun (s, e) ->
      printfn "%s: %A" s e
      messenger.Dispatch(SetMode <| ErrorMode e)
    )

  let backLayer = new asd.Layer2D()

  let mainLayer = new asd.Layer2D(IsUpdated = false, IsDrawn = false)
  let effectLayer = new asd.Layer2D(IsUpdated = false, IsDrawn = false)
  let uiLayer = new asd.Layer2D()
  let longPressArcLayer = new asd.Layer2D()

  let gameLayers = [
    mainLayer
    effectLayer
  ]

  let player =
    new PlayerView(DrawingPriority = 2)

  let hpObj = new asd.GeometryObject2D()

  let bacuumObj =
    let tex = asd.Engine.Graphics.CreateTexture2D(viewSetting.bacuumTexturePath)
    new asd.TextureObject2D(
      Position = asd.Vector2DF(gameSetting.playerX, gameSetting.ceilingHeight),
      Texture = tex,
      CenterPosition = tex.Size.To2DF() * 0.5f,
      DrawingPriority = 3
    )

  do
    messenger
      .Add(fun x ->
        if x.mode = GameMode then
          (player :> IUpdatee<_>).Update(x.game.player)
      )

    messenger
      .Where(fun x -> x.mode = GameMode)
      .Select(fun x ->
        [ for a in x.game.flyingCats -> (a.Key, a) ]
      ).Subscribe(new ActorsUpdater<_, _, FlyingCat>(mainLayer, fun() -> new FlyingCatView(DrawingPriority = 1)))
      |> ignore

    let areaX = float32 gameSetting.areaSize.x
    let areaY = float32 gameSetting.areaSize.y
    let areaHeight = areaY - gameSetting.floorHeight

    let hpArea =
      asd.RectF(
        areaX * 0.1f,
        gameSetting.floorHeight + areaHeight * 0.1f,
        areaX * 0.8f,
        areaHeight * 0.1f
      )

    let rect = new asd.RectangleShape(DrawingArea=hpArea)

    let hpBar =
      new asd.GeometryObject2D(
        Shape = rect,
        Color = asd.Color(255, 0, 0, 255)
    )

    hpObj.Shape <- new asd.RectangleShape(DrawingArea=hpArea)

    messenger
      .Add(fun x ->
        if x.mode = GameMode then
          let rate = x.game.hp / gameSetting.hp
          let w = areaX * 0.8f * rate
          rect.DrawingArea <-
            asd.RectF(
              hpArea.Position, asd.Vector2DF(w, hpArea.Size.Y)
            )
      )
    hpObj.AddDrawnChildWithoutColor(hpBar)


  let createFont size outLineSize =
    asd.Engine.Graphics.CreateDynamicFont(
      viewSetting.fontPath, size, asd.Color(0, 0, 0, 255), outLineSize, asd.Color(0, 0, 0, 255)
    )

  let titleFont = createFont viewSetting.titleSize 0
  let headerFont = createFont viewSetting.headerSize 0
  let textFont = createFont viewSetting.textSize 0
  let boldFont = createFont viewSetting.textSize 1
  let smallFont = createFont viewSetting.smallTextSize 0
  let largeFont = createFont viewSetting.largeSize 1

  let mouse, window =
    Window.create
      (asd.Engine.WindowSize.ToVector2F())
      viewSetting.menuSetting
      textFont

  do
    messenger
      .Select(fun model -> view model messenger.Dispatch)
      .Add(fun contents ->
        (window.IsToggleOn, contents.IsEmpty)
        |> function
        | true, true ->
            window.Toggle(false, fun() ->
              gameLayers |> iter (fun layer ->
                layer.IsUpdated <- true
                layer.IsDrawn <- true
              )
            )
        | x, false ->
          window.UIContents <-
            contents |> List.map(function
              | Title s ->
                UI.TextWith(s, titleFont)
              | Header s ->
                UI.TextWith(s, headerFont)
              | Large s ->
                UI.TextWith(s, largeFont)
              | Text s -> UI.Text s
              | BoldText s -> UI.TextWith(s, boldFont)
              | SmallText s -> UI.TextWith(s, smallFont)
              | Line -> UI.Rect(viewSetting.lineWidth, 0.8f)
              | Button(s, f) -> UI.Button(s, fun() -> f())
            )

          if not x then
            gameLayers |> iter(fun layer ->
              layer.IsUpdated <- false
              layer.IsDrawn <- false
            )
            window.Toggle(true)

        | _, _ -> ()
          
      )

  let fpsText = new asd.TextObject2D(Font = largeFont)
  do
    scoreObj.Font <- largeFont

    fpsText.AddOnUpdateEvent(fun() ->
      fpsText.Text <- sprintf "FPS: %d" <| int asd.Engine.CurrentFPS
      let size = fpsText.Font.HorizontalSize(fpsText.Text)
      fpsText.Position <-
        asd.Engine.WindowSize.To2DF() - size.To2DF()
    )
    
    messenger
      .Add(fun x ->
        if x.mode = GameMode || x.mode = PauseMode then
          scoreObj.Text <- sprintf "ステージ:%d / スコア:%d" x.game.level x.game.score

          let size = scoreObj.Font.HorizontalSize(scoreObj.Text)
          scoreObj.Position <-
            asd.Vector2DI(
              asd.Engine.WindowSize.X - size.X
              , 0).To2DF()

          let size = fpsText.Font.HorizontalSize(fpsText.Text)
          fpsText.Position <-
            asd.Engine.WindowSize.To2DF() - size.To2DF()
      )

  let longPressArc =
    let ws = asd.Engine.WindowSize.To2DF()
    let m = 0.5f * min ws.X ws.Y
    new LongPressCircle(m * 0.6f, m * 0.8f)

  do
    messenger.Run()

  override this.OnRegistered() =
    this.AddLayer(backLayer)
    this.AddLayer(mainLayer)
    this.AddLayer(effectLayer)
    this.AddLayer(uiLayer)
    this.AddLayer(longPressArcLayer)

    backLayer.AddPostEffect(new NijiPostEffect())
    //backLayer.AddCamera(gameSetting)

    mainLayer.AddCamera(gameSetting)
    mainLayer.AddObject(hpObj)
    mainLayer.AddObject(player)
    mainLayer.AddObject(bacuumObj)

    effectLayer.AddCamera(gameSetting)
    hitEffect.Attach(effectLayer)

    uiLayer.AddObject(scoreObj)
    uiLayer.AddObject(fpsText)
    uiLayer.AddObject(window)
    uiLayer.AddMouseButtonSelecter(mouse, "Mouse")
    longPressArcLayer.AddObject(longPressArc)

    this.AddCoroutineAsParallel(seq {
      let mutable holdCount = 0
      let wf = viewSetting.longPressFrameWait
      let f = viewSetting.longPressFrame

      let inline isState state key =
        asd.Engine.Keyboard.GetKeyState key = state

      //let isPush = isState asd.ButtonState.Push
      let isRelease = isState asd.ButtonState.Release

      while true do
        if isRelease asd.Keys.Escape then
          messenger.LastModel.mode |> function
          | CreditMode _
          | SelectMode
          | GameOverMode
            ->
            messenger.Dispatch(SetMode TitleMode)
          | GameMode ->
            messenger.Dispatch(SetMode PauseMode)
          | PauseMode  ->
            messenger.Dispatch(SetMode GameMode)
          | _ -> ()

        asd.Engine.Keyboard.GetKeyState asd.Keys.Space
        |> function
        | asd.ButtonState.Push ->
          messenger.Dispatch(Push)

        | asd.ButtonState.Release ->
          if holdCount <= wf then
            messenger.Dispatch(Release)
          else
            longPressArc.SetRate(0.0f)

          holdCount <- 0

        | asd.ButtonState.Hold ->
          if messenger.LastModel.mode.EnabledLongPress && (holdCount < f + wf) then
            holdCount <- holdCount + one

            if holdCount > wf then
              longPressArc.SetRate(
                float32 (holdCount - wf) / float32 f
              )

            if holdCount = f + wf then
              longPressArc.SetRate(0.0f)
              messenger.Dispatch(LongPress)

        | _ -> ()

        yield()
    })


  override this.OnUpdated() =
    if messenger.LastModel.mode = GameMode then
      messenger.Dispatch(Msg.Tick)
    
    //messenger.NotifyView()
