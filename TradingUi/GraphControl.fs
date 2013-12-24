(*
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

namespace TradingUi
 
module GraphControl =

    open System
    open System.Drawing
    open System.Windows.Forms
    open System.Drawing.Drawing2D

    open TradingUi.GraphFunctions

    let areCoordinatesOutOfBounds (x, y) (width, height) =
            x < 0 || x > width || y < 0 || y > height

    open Scrollbar
    open Graph

    let getNumberOfRecords (graphs:ResizeArray<IGraph>) = 
        (Array.maxBy (fun (graph:IGraph) -> graph.NumberOfRecords()) <| graphs.ToArray()).NumberOfRecords()

    let getWidestMargin (graphs:ResizeArray<IGraph>) = 
        (Array.maxBy (fun (graph:IGraph) -> graph.RecordMargin()) <| graphs.ToArray()).RecordMargin()

    let getWidestRecord (graphs:ResizeArray<IGraph>) = 
        (Array.maxBy (fun (graph:IGraph) -> graph.RecordWidth()) <| graphs.ToArray()).RecordWidth()

    let getHighAndLow (graphs:IGraph array) leftMostRecord numberOfRecordsDisplayed =
        let highsAndLows = 
            [| for graph in graphs do
                match graph.HighAndLow leftMostRecord numberOfRecordsDisplayed with
                | Some(values) -> yield values
                | None -> () |]

        let high, _ = Array.maxBy (fun (high, _) -> high) highsAndLows
        let _, low = Array.minBy (fun (_, low) -> low) highsAndLows

        high, low
 
    type GraphControl(
                        scrollbar: IScrollbar,
                        graph: IGraph
        ) as this =
        inherit Control()

        let mutable leftMostRecord = 0

        let graphs = ResizeArray<IGraph>()

        let mutable mouseDown: (int * int * int * int) option = None
        let mutable lastMouse: (int * int) option = None
        
        let mouseMovements = System.Collections.Generic.Queue<int * int64>()
        let maxVelocity, friction = 30m, 1m

        let labelFont = new Font("Consolas", float32(8))

        let marginTop, marginBottom = 20, 20

        let runOnUiThread func =
            let action = Action(fun () -> func())
            this.Invoke(action) |> ignore

        let moveLeft i =
            let numberOfRecords = getNumberOfRecords graphs

            let i = int <| ceil i 
            if leftMostRecord - i < 0 then
                leftMostRecord <- 0
            else if leftMostRecord - i > numberOfRecords - 1 then 
                leftMostRecord <- numberOfRecords - 1
            else
                leftMostRecord <- leftMostRecord - i
            runOnUiThread (fun () -> this.Invalidate())

        let cancelScrolling = new Event<unit>()

        do
            this.BackColor <- Color.FromArgb(10, 10, 10)
            this.DoubleBuffered <- true
            graphs.Add(graph)

        [<CLIEvent>]
        member this.CancelScrolling = cancelScrolling.Publish

        member this.AddGraph graph =
            graphs.Add graph

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

            let graphsArray = graphs.ToArray()
            let recordMargin = getWidestMargin graphs
            let recordWidth = getWidestRecord graphs

            let recordNumber = mapXCoordinateToRecordNumber (float event.X) recordWidth recordMargin leftMostRecord |> int

            mouseDown <- Some(event.X, event.Y, leftMostRecord, recordNumber)
            this.Focus() |> ignore

            mouseMovements.Clear()
            mouseMovements.Enqueue(event.X, DateTime.Now.Ticks)
            cancelScrolling.Trigger()

        override this.OnMouseUp(event:MouseEventArgs) =
            base.OnMouseUp event
            mouseDown <- None

            if mouseMovements.Count > 1 then
                let velocity (mouseMovements: System.Collections.Generic.Queue<int * int64>) =
                    let firstX, firstTime = mouseMovements.Peek()

                    let rec getMouseMovedStats distance time =
                        if mouseMovements.Count > 0 then
                            let x, time = mouseMovements.Dequeue()
                            let milliseconds = time / TimeSpan.TicksPerMillisecond - firstTime / TimeSpan.TicksPerMillisecond
                            getMouseMovedStats (x + (firstX - distance)) milliseconds
                        else
                            distance, time

                    let distance, time = getMouseMovedStats 0 <| int64 0

                    let distance, time = float distance, float time

                    let velocity = decimal <| (abs(distance) / time) * (if distance < 0.0 then -5.0 else 5.0)

                    if velocity > maxVelocity then maxVelocity
                    else if velocity < -maxVelocity then -maxVelocity
                    else velocity
            
                let kineticScroll = new MailboxProcessor<string>(fun inbox ->
                    let rec loop i timeout (velocity: decimal) moveLeft =
                        async { 
                            let! result = inbox.TryReceive timeout

                            if result.IsSome then
                                mouseMovements.Clear()
                            else
                                let velocity = 
                                    if Math.Floor(velocity) > 0m then 
                                        velocity - friction
                                    else if Math.Ceiling(velocity) < 0m then
                                        velocity + friction
                                    else
                                        0m

                                moveLeft velocity

                                if not <| (Math.Abs(velocity) < Math.Abs(friction)) then
                                    return! loop (i + 1) timeout velocity moveLeft
                        } 
                    loop 0 25 (velocity mouseMovements) moveLeft)

                kineticScroll.Start()

                Event.add (fun _ -> kineticScroll.Post "stop") cancelScrolling.Publish

        member private this.TryMoveRecords (eventX, eventY) =
            let recordMargin = getWidestMargin graphs
            let recordWidth = getWidestRecord graphs

            match mouseDown with
            | Some(x, y, leftMostRecordWhenMouseDown, recordWhenMouseDown) when not <| areCoordinatesOutOfBounds (eventX, eventY) (this.Width, this.Height) -> 
                let numberOfRecords = getNumberOfRecords graphs
                let x = float eventX
                leftMostRecord <- moveRecords recordWhenMouseDown x leftMostRecordWhenMouseDown recordWidth recordMargin numberOfRecords
            | _ -> ()

        override this.OnMouseMove(event:MouseEventArgs) =
            base.OnMouseMove event
 
            lastMouse <- Some(event.X, event.Y)

            if mouseDown <> None then
                let maxMouseMovementsRecorded = 5
                if mouseMovements.Count > maxMouseMovementsRecorded then
                    mouseMovements.Dequeue() |> ignore
                mouseMovements.Enqueue(event.X, System.DateTime.Now.Ticks)

            this.TryMoveRecords (event.X, event.Y) 

            this.Invalidate()

        override this.OnMouseWheel(event:MouseEventArgs) =
            base.OnMouseWheel event

            let change = if event.Delta > 0 then 1 else -1
            for graph in graphs do
                graph.Zoom change

            this.Invalidate()
 
        override this.OnPaint (event:PaintEventArgs) =
            base.OnPaint event

            let graphsArray = graphs.ToArray()

            let recordMargin = getWidestMargin graphs
            let recordWidth = getWidestRecord graphs

            let numberOfRecordsDisplayed = getNumberOfRecordsCanBeDisplayed this.Width recordWidth recordMargin

            let high, low = getHighAndLow graphsArray leftMostRecord numberOfRecordsDisplayed

            let (labels: float list), highLabel, lowLabel = getRoundedValuesBetween high low [uint16(1);uint16(2);uint16(5)] 10

            let gap = if labels.Length = 1 then 0 else abs(float(labels.Head - labels.Tail.Head)) |> int

            let drawableHeight = this.Height - marginTop - marginBottom

            let drawGraph (graph: IGraph) = 
                graph.Draw event.Graphics leftMostRecord (this.Width, drawableHeight) recordWidth recordMargin (float32 high, float32 low) (float32 gap) marginTop

            Array.iter drawGraph graphsArray

            paintYAxis event.Graphics labels (this.Width, drawableHeight) labelFont (Color.FromArgb(170, 170, 170)) marginTop
            
            let numberOfRecords = getNumberOfRecords graphs
            scrollbar.Draw event.Graphics leftMostRecord numberOfRecords (this.Width, this.Height) recordWidth recordMargin

            match lastMouse with
            | Some(x, y) when not <| areCoordinatesOutOfBounds (x, y) (this.Width, this.Height) -> 
                let recordNumber = mapXCoordinateToRecordNumber (float x) recordWidth recordMargin leftMostRecord |> int
                let x = mapRecordNumberToXCoordinate recordNumber recordWidth recordMargin leftMostRecord
                let x = x + int(ceiling(float(recordWidth / 2)))
                paintCoordinates event.Graphics (x, y) (this.Width, this.Height)
            | _ -> ()