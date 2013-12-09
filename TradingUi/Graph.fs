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
 
module Graph
 
open System
open System.Drawing
open System.Windows.Forms
 
let abs (x: float) = System.Math.Abs(x)
 
let log10 (x: float) = System.Math.Log10(x)
 
let floor (x: float) = System.Math.Floor(x)
 
let ceiling (x: float) = System.Math.Ceiling(x)

type Direction =
| Left
| Right
 
/// <summary>
/// Get a list of values between given high and low values rounded to the nearest round to value.
/// The lowest value will be the lowest rounded value above low, and the highest value the 
/// highest rounded value below high.
/// </summary>
/// <param name="high">Highest value. Must be greater than or equal to low.</param>
/// <param name="low">Lowest value. Must be less than or equal to high.</param>
/// <param name="roundTo">The values returned will be rounded to the nearest number * (10 ^ x) in this list.</param>
/// <param name="roughNumberOfValues">Roughly the number of values to be returned, as the numbers returned are rounded
/// they may not exactly fit the number of values given.</param>
/// <returns>List of values between given high and low values rounded to the nearest power of ten 
/// of the nearest round to (roundTo) value.</returns>
let getRoundedValuesBetween (high: float) low (roundTo: uint16 list) roughNumberOfValues =
    if high < low then raise(new System.ArgumentException("high must be greater than or equal to low.", "high"))
    if roundTo.IsEmpty then raise(new System.ArgumentException("roundTo must not be empty.", "roundTo"))
    if roughNumberOfValues <= 0 then raise(new System.ArgumentException("roughNumberOfValues must greater than 0.", "roughNumberOfValues"))
 
    if high = low then
        [high], high, low
    else
        let heightOfValue = abs(high - low) / float(roughNumberOfValues)
 
        let ceilingExponent = ceiling(log10(heightOfValue))
        let floorExponent = floor(log10(heightOfValue))
 
        let minRoundTo = float(List.min roundTo) * (10.0 ** ceilingExponent)
 
        let roundTo = minRoundTo :: List.map (fun x -> float(x) * (10.0 ** floorExponent)) roundTo
 
        let closestToRoundTo = List.minBy (fun x -> abs(heightOfValue - x)) roundTo
 
        let lowestLabel = closestToRoundTo * floor(low / closestToRoundTo)
        let highestLabel = closestToRoundTo * ceiling(high / closestToRoundTo)
 
        let numberOfLabels = abs(highestLabel - lowestLabel) / closestToRoundTo

        let labels = [ for i in 0..int(numberOfLabels) do yield lowestLabel + closestToRoundTo * float(i) ]
 
        labels, highestLabel, lowestLabel
 
let getHighestHighAndLowestLow records =
    let getHighestHighAndLowestLow (currentHigh, currentLow) (high, low, _, _) =
        let high = if high > currentHigh then high else currentHigh
        let low = if low < currentLow then low else currentLow
        high, low
 
    let startingValues = System.Double.MinValue, System.Double.MaxValue
 
    Array.fold getHighestHighAndLowestLow startingValues records
 
type CoinGraph(records: (float * float * float * float) array) as this =
    inherit Control()

    let mutable leftMostRecord = 0

    let mutable candleWidth = 7

    let mutable mouseDown: (int * int) option = None
    
    let mutable recordWhenMouseDown: int option = None
 
    let mutable lastMouse: (int * int) option = None

    let mouseMovements = ResizeArray<int * int64>()

    let marginTop = 20

    let marginBottom = 20

    let candleLeftMargin = 3

    let runOnUiThread func =
        let action = Action(fun () -> func())
        this.Invoke(action) |> ignore

    let mapXCoordinateToRecordNumber x =
        floor(x / float(candleWidth + candleLeftMargin)) + float(leftMostRecord)

    let mapRecordNumberToXCoordinate recordNumber =
        (recordNumber - leftMostRecord) * (candleWidth + candleLeftMargin)

    let mapValueToYCoordinate (highestHigh, lowestLow) gap value =
        let pixel = float32(this.Height) / (highestHigh - lowestLow + float32(gap) * float32(2))
        (highestHigh + gap - value) * pixel

    let mapHeight height (highestHigh, lowestLow) gap =
        let pixel = float32(this.Height) / (highestHigh - lowestLow + float32(gap) * float32(2))
        height * pixel
 
    let getCandleStickLine limits (high, low) x gap =
        let x = x + float32(ceiling(float(candleWidth / 2)))

        let mapValueToY = mapValueToYCoordinate limits gap

        PointF(x, mapValueToY high), PointF(x, mapValueToY low)
 
    let getCandleStick limits (high, low, opening, closing) x gap =
        let top, bottom = if opening > closing then opening, closing else closing, opening

        let y = mapValueToYCoordinate limits gap top

        let height = mapHeight (top - bottom) limits gap

        RectangleF(x, y, float32(candleWidth), height)

    let paintCandleStick (graphics:Graphics) (high: float, low: float, opening: float, closing: float) candlestickNumber limits gap =
        let candleColour = if closing > opening then Color.Green else Color.Red

        use pen = new Pen(candleColour, float32(0.5))
        use brush = new SolidBrush(candleColour)

        let x = mapRecordNumberToXCoordinate candlestickNumber |> float32

        let rect = getCandleStick limits (float32(high), float32(low), float32(opening), float32(closing)) x gap
 
        graphics.FillRectangle(brush, rect)
        
        let high, low = getCandleStickLine limits (float32(high), float32(low)) x gap

        graphics.DrawLine(pen, high, low)
 
    let paintCandleSticks (graphics:Graphics) (high, low) gap records =
        let paintCandleStick = paintCandleStick graphics
        Array.iteri (fun i record -> paintCandleStick record (i + leftMostRecord) (high, low) gap) records
 
    let paintCoordinates (graphics:Graphics) (lastMouseX, lastMouseY) =
        let startHorizontal, endHorizontal = Point(0, lastMouseY), Point(this.Width, lastMouseY)
        let startVertical, endVertical = Point(lastMouseX, 0), Point(lastMouseX, this.Height)
 
        use pen = new Pen(Color.DarkGray, float32(0.5))
 
        graphics.DrawLine(pen, startHorizontal, endHorizontal)
        graphics.DrawLine(pen, startVertical, endVertical)
 
    let paintYAxisLabel (graphics:Graphics) y label font widestLabelWidth =
        let rightMargin = 5
 
        let stringMeasurements = graphics.MeasureString(label, font)
        let stringLength = stringMeasurements.Width
 
        use pen = new Pen(Color.FromArgb(170, 170, 170), float32(0.5))
 
        let lineLength = float32(10)
 
        let labelPadding = float32(25)

        let textY = y - float32(stringMeasurements.Height) / float32(2)
 
        let lineStart = PointF(float32(this.Width), y)
        let lineEnd = PointF(lineStart.X - lineLength, y)
        graphics.DrawLine(pen, lineStart, lineEnd)
 
        let textStart = PointF(lineEnd.X - widestLabelWidth - labelPadding, textY)
        graphics.DrawString(label, font, new SolidBrush(Color.FromArgb(170, 170, 170)), textStart)
 
        let lineStart = PointF(textStart.X - labelPadding, y)
        let lineEnd = PointF(lineStart.X - lineLength, y)
        graphics.DrawLine(pen, lineStart, lineEnd)
 
    let paintYAxis (graphics:Graphics) (labels: 'a list) =
        use font = new Font("Consolas", float32(8))

        let labels = List.map (fun label -> label.ToString()) labels

        let widestLabelWidth = List.fold (fun width label -> 
            let currentWidth = graphics.MeasureString(label, font).Width
            if currentWidth > width then currentWidth else width) (float32(0)) labels
 
        let paintLabel i label =
            let y = (float32(this.Height) / float32(labels.Length + 1)) * float32(labels.Length - i)
            paintYAxisLabel graphics y label font widestLabelWidth
 
        List.iteri (fun i x -> paintLabel i <| x.ToString()) labels

    let getNumberOfRecordsCanBeDisplayed () =
        this.Width / (candleWidth + candleLeftMargin)

    let velocity ((fromX, fromTime: int64), (toX, toTime: int64)) =
        let changeInX = decimal <| abs(fromX - toX |> float)
        let changeInTime = decimal  <| abs(fromTime - toTime |> float)

        changeInX / changeInTime

    let direction ((fromX, _), (toX, _)) =
        if fromX - toX < 0 then Direction.Left else Direction.Right

    let isPreviousVelocityLowerAndSameDirection (mouseMovements: ResizeArray<int * int64>) =
        if mouseMovements.Count < 4 then
            false 
        else
            let last = mouseMovements.[mouseMovements.Count - 1], mouseMovements.[mouseMovements.Count - 2]
            let previous = mouseMovements.[mouseMovements.Count - 2], mouseMovements.[mouseMovements.Count - 3]

            velocity last > velocity previous && direction last = direction previous
 
    do
        this.BackColor <- Color.FromArgb(10, 10, 10)
        this.DoubleBuffered <- true

    override this.OnMouseEnter event =
        base.OnMouseEnter event
        
        Cursor.Hide()

    override this.OnMouseLeave event =
        base.OnMouseLeave event

        Cursor.Show()

        lastMouse <- None
 
        this.Invalidate()

    override this.OnMouseDown(event:MouseEventArgs) =
        base.OnMouseDown event
        recordWhenMouseDown <- Some(leftMostRecord)
        mouseDown <- Some(event.X, event.Y)
        this.Focus() |> ignore

        Async.CancelDefaultToken()

    override this.OnMouseUp(event:MouseEventArgs) =
        base.OnMouseUp event
        recordWhenMouseDown <- None
        mouseDown <- None

        let kineticScroll = async { 
            for i in 0..10 do 
                if leftMostRecord + i < records.Length - 1 then
                    leftMostRecord <- leftMostRecord + i
                    runOnUiThread (fun () -> this.Invalidate())
                    do! Async.Sleep(25)
        }

        Async.Start kineticScroll |> ignore

        mouseMovements.Clear()

    member private this.MoveRecords distance direction recordWhenMouseDown =
        match direction with
        | Left -> leftMostRecord <- 
                                    if recordWhenMouseDown + distance / (candleWidth + candleLeftMargin) >= records.Length then records.Length - 1
                                    else recordWhenMouseDown + distance / (candleWidth + candleLeftMargin)
        | Right -> leftMostRecord <- 
                                    if recordWhenMouseDown - distance / (candleWidth + candleLeftMargin) < 0 then 0
                                    else recordWhenMouseDown - distance / (candleWidth + candleLeftMargin)
 
    override this.OnMouseMove(event:MouseEventArgs) =
        base.OnMouseMove event
 
        lastMouse <- Some(event.X, event.Y)

        if recordWhenMouseDown <> None then
            if mouseMovements |> isPreviousVelocityLowerAndSameDirection then
                mouseMovements.Clear()
            mouseMovements.Add(event.X, System.DateTime.Now.Ticks)

        match mouseDown, recordWhenMouseDown with
        | Some(x, y), Some(recordWhenMouseDown) when not <| this.AreCoordinatesOutOfBounds (event.X, event.Y) -> 
            let change = float <| event.X - x
            let direction = if change >= 0.0 then Right else Left
            let distance = abs(change)
            this.MoveRecords (int(distance)) direction recordWhenMouseDown
        | _ -> ()

        this.Invalidate()

    member this.AreCoordinatesOutOfBounds(x, y) =
        x < 0 || x > this.Width || y < 0 || y > this.Height

    override this.OnMouseWheel(event:MouseEventArgs) =
        base.OnMouseWheel event

        let change = if event.Delta > 0 then 1 else -1

        if candleWidth + change > 0 && candleWidth + change < 20 then
            candleWidth <- candleWidth + change

        this.Invalidate()
 
    override this.OnPaint (event:PaintEventArgs) =
        base.OnPaint event

        let graphics = event.Graphics

        let lastRecord = leftMostRecord + getNumberOfRecordsCanBeDisplayed() - 1

        let lastRecord = if lastRecord >= records.Length then records.Length - 1 else lastRecord

        let records = records.[leftMostRecord..lastRecord]

        let high, low = getHighestHighAndLowestLow records
 
        let (labels: float list), highLabel, lowLabel = getRoundedValuesBetween high low [uint16(1);uint16(2);uint16(5)] 10

        let gap = if labels.Length = 1 then 0 else abs(float(labels.Head - labels.Tail.Head)) |> int

        paintCandleSticks graphics (float32 highLabel, float32 lowLabel) (float32 gap) records
 
        paintYAxis graphics labels
 
        match lastMouse with
        | Some(x, y) when not <| this.AreCoordinatesOutOfBounds(x, y) -> 
            let x = float(x) |> mapXCoordinateToRecordNumber |> int |> mapRecordNumberToXCoordinate
            let x = x + int(ceiling(float(candleWidth / 2)))
            paintCoordinates graphics (x, y)
        | _ -> ()