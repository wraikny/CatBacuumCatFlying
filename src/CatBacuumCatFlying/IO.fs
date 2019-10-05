module Cbcf.IO

open FSharp.Data

type TheCatApi = JsonProvider<"""[{ "url":"hoge" }]""">
type TheCatApiCategory = JsonProvider<"""[{ "id":0, "name":"aaa" }]""">

let private getTheCatApiCategoriesAsync apiKey =
  let url = "https://api.thecatapi.com/v1/categories"
  Http.AsyncRequestString
    ( url, httpMethod = "GET",
      query = [ "api_key", apiKey; "format", "json" ],
      headers = [ "Accept", "application/json" ]
    )

let private getTheCatApiAsync (categoryId: int) (limit: int) apiKey =
  let url = "https://api.thecatapi.com/v1/images/search"
  
  Http.AsyncRequestString
    (
      url, httpMethod = "GET",
      query = [
        "api_key", apiKey
        "format", "json"
        "limit", limit.ToString()
        "category_ids", categoryId.ToString()
      ],
      headers = [ "Accept", "application/json" ]
    )

open System.Net
open System.IO
open System.Drawing
open System.Text

let private downloadImage (url: string) = async {
  use client = new WebClient()
  return! client.OpenReadTaskAsync(url) |> Async.AwaitTask
}

let private urlToFilename url =
  sprintf "%s.png" (Path.GetFileNameWithoutExtension url)

let private saveImageStreamAsPng (filename: string) (stream: Stream) =
  use bitmap = new Bitmap(stream)
  if bitmap <> null then
    bitmap.Save(filename, Imaging.ImageFormat.Png)
  stream.Dispose()

let loadCategoryAsync apiKey = async {
  let! json = getTheCatApiCategoriesAsync apiKey
  return
    [| for x in TheCatApiCategory.Parse json -> (x.Id, x.Name) |]
}

let downloadImages apiKey dir (category, categoryName) limit dispatch = async {
  let! json = getTheCatApiAsync category limit apiKey
  printfn "Json:\n%s" json
  
  for x in TheCatApi.Parse json do
    let! stream = downloadImage x.Url
  
    let filename =
      urlToFilename x.Url
      |> sprintf "%s/%s/%s" dir categoryName
    saveImageStreamAsPng filename stream
    printfn "Saved %s" filename
    dispatch (category, filename)
}


module Altseed =
  let loadString(path) =
    asd.Engine.File.CreateStaticFile(path).Buffer
    |> Encoding.UTF8.GetString
