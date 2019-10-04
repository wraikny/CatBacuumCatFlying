module Cbcf.IO

//open FSharp.Data
open FSharp.Data

type TheCatApi = JsonProvider<"""[{ "url":"hoge" }]""">
type TheCatApiCategory = JsonProvider<"""[{ "id":0, "name":"aaa" }]""">

let getTheCatApiCategoriesAsync apiKey =
  let url = "https://api.thecatapi.com/v1/categories"
  Http.AsyncRequestString
    ( url, httpMethod = "GET",
      query = [ "api_key", apiKey; "format", "json" ],
      headers = [ "Accept", "application/json" ]
    )

let getTheCatApiAsync (categoryIds: seq<int>) (limit: int) apiKey =
  let url = "https://api.thecatapi.com/v1/images/search"

  let categories =
    categoryIds
    |> Seq.map(fun x -> x.ToString())
    |> String.concat ","
    |> sprintf "[%s]"
  
  Http.AsyncRequestString
    (
      url, httpMethod = "GET",
      query = [
        yield! [|
        "api_key", apiKey
        "format", "json"
        "limit", limit.ToString()
        |]
        if Seq.isEmpty categoryIds |> not then
          yield "category_ids", categories
      ],
      headers = [ "Accept", "application/json" ]
    )

open System.Net
open System.IO
open System.Drawing
open System.Text

let downloadImage (url: string) = async {
  use client = new WebClient()
  return! client.OpenReadTaskAsync(url) |> Async.AwaitTask
}

let urlToFilename url =
  sprintf "%s.png" (Path.GetFileNameWithoutExtension url)

let saveImageStreamAsPng (filename: string) (stream: Stream) =
  use bitmap = new Bitmap(stream)
  if bitmap <> null then
    bitmap.Save(filename, Imaging.ImageFormat.Png)
  stream.Dispose()

let loadString(path) =
  asd.Engine.File.CreateStaticFile(path).Buffer
  |> Encoding.UTF8.GetString
  |> fun x -> x.Trim()

open Affogato.Collections
open Affogato.Helper

type TheCatImageLoader(apiKeyPath, onError) =
  let apiKey = loadString(apiKeyPath)

  member this.LoadCategoriesAsync(dispatch) =
    async {
      try
        let! json = getTheCatApiCategoriesAsync apiKey
        seq {
          for x in TheCatApiCategory.Parse json -> (x.Id, x.Name)
        }
        |> HashMap.ofSeq
        |> dispatch
      with e -> onError e
    }

  member this.DownloadAsync(dir, categories, limit) =
    async {
      try
        while true do
          let! json = getTheCatApiAsync categories limit apiKey

          let! streams =
            TheCatApi.Parse json
            |> Array.map(fun x -> async{
              let! stream = downloadImage x.Url
              return (x.Url, stream)
            })
            |> Async.Parallel

          for (url, s) in streams do
            urlToFilename url
            |> sprintf "%s/%s" dir
            |> flip saveImageStreamAsPng s

      with e -> onError e
    }