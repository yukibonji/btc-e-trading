﻿(*
    Copyright (C) 2013  Matthew Mcveigh
 
    This file is part of F# Unaffiliated BTC-E Trading Framework.
 
    F# Unaffiliated BTC-E Trading Framework is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 3 of the License, or (at your option) any later version.
 
    F# Unaffiliated BTC-E Trading Framework is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
    Lesser General Public License for more details.
 
    You should have received a copy of the GNU Lesser General Public
    License along with F# Unaffiliated BTC-E Trading Framework. If not, see <http://www.gnu.org/licenses/>.
*)

namespace TradingGraph

module IScrollbar =

    open System
    open System.Drawing
    open System.Drawing.Drawing2D

    type IScrollbar =
        abstract member Draw : 
            graphics:Graphics -> 
            leftMostRecord:int ->
            numberOfRecords:int ->
            width:int * height:int -> 
            candleWidth:int ->
            candleLeftMargin:int ->
            unit

        abstract member ViewMoved : leftMostRecord:int -> unit

        abstract member MouseDown : unit -> unit

        abstract member MouseUp : unit -> unit

        [<CLIEvent>]
        abstract member RedrawEvent : IEvent<unit>