module Cbcf.ViewModel
open Cbcf.Update
open FSharpPlus

type UI =
  | Title of string
  | Header of string
  | Text of string
  | BoldText of string
  | SmallText of string
  | Large of string
  | Line
  | Button of string * (unit -> unit)

let view (model: Model) dispatch =
  let inline urlButton text (url: string) =
    Button(text, fun() ->
      url
      |> System.Diagnostics.Process.Start
      |> ignore
    )

  let inline msgButton text msg =
    Button(text, fun() ->
      dispatch msg
    )

  model.mode |> function
  | CreditMode page ->
    [
      yield Title "クレジット"
      match page with
      | One -> yield! [
          urlButton "作者: wraikny" "https://twitter.com/wraikny"
          urlButton "ゲームエンジン: Altseed" "http://altseed.github.io/"
          urlButton "猫: The Cat API" "https://thecatapi.com"
          urlButton "フォント: M+ FONTS" "https://mplus-fonts.osdn.jp/"
          Line
          msgButton "次へ" <| SetMode (CreditMode Two)
        ]
      | Two -> yield! [
          urlButton "イラスト: いらすとや" "https://www.irasutoya.com/"
          urlButton "効果音(猫): ポケットサウンド" "https://pocket-se.info"
          urlButton "効果音(他): くらげ工匠" "http://www.kurage-kosho.info/"
          urlButton "BGM: d-elf.com" "https://www.d-elf.com/"
          Line
          msgButton "タイトルに戻る" <| SetMode TitleMode
      ]
    ]

  | TitleMode ->
    [
      Title model.setting.title
      SmallText "wraikny @ Amusement Creators 雙峰祭2019"
      Line
      BoldText "スペースキー長押しでスタート！"
      Line
      msgButton "クレジットを開く" <| SetMode (CreditMode One)
    ]

  | SelectMode ->
    [
      yield Header "モードセレクト"
      if model.categories.Length = 0 then
        yield! [
          Text "データをダウンロード中..."
          BoldText "しばらくお待ち下さい"
          Line
          Text "セキュリティソフトによって処理が一時停止する場合があります"
        ]
      else
        let len = model.categories.Length
        let inline createItem i =
           model.categories.[(model.categoryIndex + i + len) % len]
           |> snd

        yield! [
          Line
          Text <| createItem -1
          Large <| createItem 0
          Text <| createItem 1
          Line
          BoldText "スペースキーで変更 / 長押しで決定"
        ]
    ]
  | WaitingMode ->
    [
      Header "画像をダウンロード中..."
      Line
      BoldText "しばらくお待ち下さい"
      Text "セキュリティソフトによって処理が一時停止する場合があります"
    ]
  | HowToPlayMode ->
    [
      Header "遊び方"
      Text "掃除機で猫を吸って、飛んでくる猫を避けよう！！！"
      Text "猫に当たると走馬灯が現れます。猫に想いを馳せる。"
      Text "クッキーを取ると回復！！　コインを取ると得点！！"
      Text "高ステージ・ハイスコアを目指してね！！"
      Line
      BoldText "ゲーム操作はスペースキーだけ！"
      Text "Escキーで一時停止もできるよ！"
    ]

  | GameMode -> []
  | ErrorMode e ->
    [
      Text <| e.GetType().ToString()
      Text <| e.Message
      Line
      Text "スタッフ/製作者に教えてもらえると嬉しいです"
      Text (sprintf "ログファイルは'%s'に出力されます" model.setting.errorLogPath)
    ]

  | PauseMode ->
    [
      Header "ポーズ"
      Text "タイトルに戻る: スペースキー長押し"
      BoldText "コンティニュー: スペースキー"
    ]

  | GameOverMode ->
    let levelText = sprintf "ステージ: %d" model.game.level
    let scoreText = sprintf "スコア: %d" model.game.score
    [
      Header "ゲームオーバー"
      Text levelText
      Text scoreText

      Line
      urlButton "ツイートする(ブラウザを開きます)" (
        let tag =
          model.setting.title
          |> String.replace " " ""
        sprintf """#%s をプレイしました！
%s
%s
@wraikny"""
          tag levelText scoreText
        |> System.Web.HttpUtility.UrlEncode
        |> sprintf "https://twitter.com/intent/tweet?text=%s"
      )
      Line
      BoldText "スペースキー長押しでタイトル"

    ]