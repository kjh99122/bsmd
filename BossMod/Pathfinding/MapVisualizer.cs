﻿using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BossMod.Pathfinding
{
    public class MapVisualizer
    {
        public Map Map;
        public int GoalPriority;
        public WPos StartPos;
        public float ScreenPixelSize = 10;
        public List<(WPos center, float ir, float or, Angle dir, Angle halfWidth)> Sectors = new();
        public List<(WPos origin, float lenF, float lenB, float halfWidth, Angle dir)> Rects = new();

        private ThetaStar _pathfind;

        public MapVisualizer(Map map, int goalPriority, WPos startPos)
        {
            Map = map;
            GoalPriority = goalPriority;
            StartPos = startPos;
            _pathfind = BuildPathfind();
            RunPathfind();
        }

        public void Draw()
        {
            var tl = ImGui.GetCursorScreenPos();
            var br = tl + new Vector2(Map.Width, Map.Height) * ScreenPixelSize;
            var tr = new Vector2(br.X, tl.Y);
            var bl = new Vector2(tl.X, br.Y);
            //ImGui.Dummy(br - tl);
            var cursorEnd = ImGui.GetCursorPos();
            cursorEnd.Y += Map.Height * ScreenPixelSize + 10;
            var dl = ImGui.GetWindowDrawList();

            // blocked squares / goal
            int nodeIndex = 0;
            int pfPathNode = -1;
            for (int y = 0; y < Map.Height; ++y)
            {
                for (int x = 0; x < Map.Width; ++x, ++nodeIndex)
                {
                    var corner = tl + new Vector2(x, y) * ScreenPixelSize;
                    var cornerEnd = corner + new Vector2(ScreenPixelSize, ScreenPixelSize);

                    var pix = Map[x, y];
                    if (pix.MaxG < float.MaxValue)
                    {
                        var alpha = 1 - (pix.MaxG > 0 ? pix.MaxG / Map.MaxG : 0);
                        uint c = 128 + (uint)(alpha * 127);
                        c = c | (c << 8) | 0xff000000;
                        dl.AddRectFilled(corner, cornerEnd, c);
                    }
                    else if (pix.Priority > 0)
                    {
                        var alpha = pix.Priority / Map.MaxPriority;
                        uint c = 128 + (uint)(alpha * 127);
                        c = (c << 8) | 0xff000000;
                        dl.AddRectFilled(corner, cornerEnd, c);
                    }

                    ref var pfNode = ref _pathfind.NodeByIndex(nodeIndex);
                    if (pfNode.OpenHeapIndex != 0)
                    {
                        dl.AddCircle((corner + cornerEnd) / 2, 3, pfNode.OpenHeapIndex < 0 ? 0xff0000ff : 0xffff0080, 0, pfNode.OpenHeapIndex == 1 ? 2 : 1);
                    }

                    if (ImGui.IsMouseHoveringRect(corner, cornerEnd))
                    {
                        ImGui.SetCursorPosX(cursorEnd.X + Map.Width * ScreenPixelSize + 10);
                        if (pix.MaxG < float.MaxValue)
                        {
                            ImGui.TextUnformatted($"Pixel at {x}x{y}: blocked, g={pix.MaxG:f3}");
                        }
                        else if (pix.Priority > 0)
                        {
                            ImGui.TextUnformatted($"Pixel at {x}x{y}: goal, prio={pix.Priority}");
                        }
                        else
                        {
                            ImGui.TextUnformatted($"Pixel at {x}x{y}: normal");
                        }

                        if (pfNode.OpenHeapIndex != 0)
                        {
                            ImGui.SetCursorPosX(cursorEnd.X + Map.Width * ScreenPixelSize + 10);
                            ImGui.TextUnformatted($"PF: g={pfNode.GScore:f3}, h={pfNode.HScore:f3}, g+h={pfNode.GScore + pfNode.HScore:f3}, parent={pfNode.ParentX}x{pfNode.ParentY}, index={pfNode.OpenHeapIndex}");

                            pfPathNode = nodeIndex;
                        }
                    }
                }
            }

            // border
            dl.AddLine(tl, tr, 0xffffffff, 2);
            dl.AddLine(tr, br, 0xffffffff, 2);
            dl.AddLine(br, bl, 0xffffffff, 2);
            dl.AddLine(bl, tl, 0xffffffff, 2);

            // grid
            for (int x = 1; x < Map.Width; ++x)
            {
                var off = new Vector2(x * ScreenPixelSize, 0);
                dl.AddLine(tl + off, bl + off, 0xffffffff, 1);
            }
            for (int y = 1; y < Map.Height; ++y)
            {
                var off = new Vector2(0, y * ScreenPixelSize);
                dl.AddLine(tl + off, tr + off, 0xffffffff, 1);
            }

            // pathfinding
            ImGui.SetCursorPosX(cursorEnd.X + Map.Width * ScreenPixelSize + 10);
            if (ImGui.Button("Reset pf"))
                ResetPathfind();
            ImGui.SameLine();
            if (ImGui.Button("Step pf"))
                StepPathfind();
            ImGui.SameLine();
            if (ImGui.Button("Run pf"))
                RunPathfind();

            var pfRes = _pathfind.CurrentResult();
            if (pfRes >= 0)
            {
                ImGui.SetCursorPosX(cursorEnd.X + Map.Width * ScreenPixelSize + 10);
                ImGui.TextUnformatted($"Path length: {_pathfind.NodeByIndex(pfRes).GScore:f3}");
            }

            if (pfPathNode == -1)
                pfPathNode = _pathfind.CurrentResult();
            if (pfPathNode >= 0)
                DrawPath(dl, tl, pfPathNode);

            // shapes
            foreach (var c in Sectors)
            {
                DrawSector(dl, tl, c.center, c.ir, c.or, c.dir, c.halfWidth);
            }
            foreach (var r in Rects)
            {
                var direction = r.dir.ToDirection();
                var side = r.halfWidth * direction.OrthoR();
                var front = r.origin + r.lenF * direction;
                var back = r.origin - r.lenB * direction;
                dl.AddQuad(tl + Map.WorldToGridFrac(front + side) * ScreenPixelSize, tl + Map.WorldToGridFrac(front - side) * ScreenPixelSize, tl + Map.WorldToGridFrac(back - side) * ScreenPixelSize, tl + Map.WorldToGridFrac(back + side) * ScreenPixelSize, 0xff0000ff);
            }

            ImGui.SetCursorPos(cursorEnd);
        }

        public void StepPathfind() => _pathfind.ExecuteStep();
        public void RunPathfind() => _pathfind.Execute();
        public void ResetPathfind() => _pathfind = BuildPathfind();

        private void DrawSector(ImDrawListPtr dl, Vector2 tl, WPos center, float ir, float or, Angle dir, Angle halfWidth)
        {
            if (halfWidth.Rad <= 0 || or <= 0 || ir >= or)
                return;

            var sCenter = tl + Map.WorldToGridFrac(center) * ScreenPixelSize;
            if (halfWidth.Rad >= MathF.PI)
            {
                dl.AddCircle(sCenter, or / Map.Resolution * ScreenPixelSize, 0xff0000ff);
                if (ir > 0)
                    dl.AddCircle(sCenter, ir / Map.Resolution * ScreenPixelSize, 0xff0000ff);
            }
            else
            {
                float sDir = MathF.PI / 2 - dir.Rad;
                dl.PathArcTo(sCenter, ir / Map.Resolution * ScreenPixelSize, sDir + halfWidth.Rad, sDir - halfWidth.Rad);
                dl.PathArcTo(sCenter, or / Map.Resolution * ScreenPixelSize, sDir - halfWidth.Rad, sDir + halfWidth.Rad);
                dl.PathStroke(0xff0000ff, ImDrawFlags.Closed, 1);
            }
        }

        private void DrawPath(ImDrawListPtr dl, Vector2 tl, int startingIndex)
        {
            if (startingIndex < 0)
                return;

            int from = startingIndex;
            int x1 = startingIndex % Map.Width;
            int y1 = startingIndex / Map.Height;
            int x2 = _pathfind.NodeByIndex(from).ParentX;
            int y2 = _pathfind.NodeByIndex(from).ParentY;
            while (x1 != x2 || y1 != y2)
            {
                dl.AddLine(tl + new Vector2(x1 + 0.5f, y1 + 0.5f) * ScreenPixelSize, tl + new Vector2(x2 + 0.5f, y2 + 0.5f) * ScreenPixelSize, 0xffff00ff, 2);
                x1 = x2;
                y1 = y2;
                from = y1 * Map.Width + x1;
                x2 = _pathfind.NodeByIndex(from).ParentX;
                y2 = _pathfind.NodeByIndex(from).ParentY;
            }
        }

        private ThetaStar BuildPathfind()
        {
            var s = Map.WorldToGrid(StartPos);
            return new(Map, Map.Goals().Where(g => g.priority >= GoalPriority).Select(g => (g.x, g.y)), Math.Clamp(s.x, 0, Map.Width), Math.Clamp(s.y, 0, Map.Height));
        }
    }
}
