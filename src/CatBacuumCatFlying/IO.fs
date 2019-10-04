module Cbcf.IO

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

let loadCategoryAsync apiKey = async {
  let! json = getTheCatApiCategoriesAsync apiKey
  return
    [ for x in TheCatApiCategory.Parse json -> (x.Id, x.Name) ]
}

let download apiKey dir categories limit dispatch = async {
  let! json = getTheCatApiAsync categories limit apiKey
  
  let! streams =
    TheCatApi.Parse json
    |> Array.map(fun x -> async{
      let! stream = downloadImage x.Url
      return (x.Url, stream)
    })
    |> Async.Parallel
  
  for (url, s) in streams do
    let filename =
      urlToFilename url
      |> sprintf "%s/%s" dir
    saveImageStreamAsPng filename s
    dispatch filename
}


module Altseed =
  let loadString(path) =
    asd.Engine.File.CreateStaticFile(path).Buffer
    |> Encoding.UTF8.GetString
