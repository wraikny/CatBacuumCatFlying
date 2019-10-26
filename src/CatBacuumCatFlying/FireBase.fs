module Cbcf.FireBase

open System
open System.Text
open FSharp.Data
open System.Linq
open System.Threading.Tasks

open Affogato.Helper

type Config = JsonProvider<"../../firebaseConfig.json">

let loadConfig path =
  asd.Engine.File.CreateStaticFile(path).Buffer
  |> Encoding.UTF8.GetString
  |> Config.Parse


type Data = {
  mode: string
  name: string
  score: uint32
  frame: uint32
  stage: uint16
  damageSum: uint32
  coinCount: uint16
  catCount: uint16
  healCount: uint16
}

let config = Config.GetSample()
