[<AutoOpen>]
module Cbcf.Utils

let inline outOf a b x = x < a || b < x

let inline lift (x: ^a): ^b = ( (^a or ^b) : (static member Lift:_->_) x)

let inline set y (x: ^a): ^a = (^a: (static member Set:_*_->_) (x, y))