#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace GPS
{
    public class WaypointsView : MonoBehaviour
    {
        public WayPointsSO WayPointsSO;

        private int NodesCount => WayPointsSO ? WayPointsSO.nodes.Count : 100;
        private int MinIndex => IndexCenter - Range.x;
        private int MaxIndex => IndexCenter + Range.y;

        public Vector2Int Range = new Vector2Int(100, 100);

        [PropertyRange(0, "NodesCount")] public int IndexCenter = 0;

        public float RadiusGizmo = 1f;
        public float RadiusGizmo2 = 2f;
        public int Step = 100;
        public bool ShowSecondPriorityPoints = true;

        public Color ColorMain = Color.red;
        public Color ColorSecond = Color.magenta;
        public Color ColorSelected = Color.green;
        public Color ColorSelected2 = Color.yellow;

        public int FontSize = 30;
        public float UpTextValue = 5f;

        private GUIStyle _style;


        [Header("Delete")] public int IndexStartToDelete;
        public int IndexEndToDelete;

        public bool ShowIndices = true;
        public bool ShowCoordinates = true;

        [Header("Add point")] public Transform AddPointTransform;


        private Vector3? _selected;

        [Button]
        public void AddPoint()
        {
            WayPointNode point = new WayPointNode();
            point.position = new NodePosition(AddPointTransform.position);
            point.id = (uint)WayPointsSO.nodes.Count;
            WayPointsSO.nodes.Add(point);

            Debug.Log("Point added:" + point.position);
            EditorUtility.SetDirty(WayPointsSO);
        }


        [Button]
        public void Delete()
        {
            using Logger _ = new Logger("Deletion. Nodes count", () => WayPointsSO.nodes.Count);
            WayPointsSO.nodes.RemoveRange(IndexStartToDelete, IndexEndToDelete - IndexStartToDelete + 1);
            EditorUtility.SetDirty(WayPointsSO);
        }

        [Button]
        public void Move()
        {
            using Logger _ = new Logger("Move. Nodes Count", () => WayPointsSO.nodes.Count);

            List<WayPointNode> points = WayPointsSO.nodes;
            for (int i = IndexEndToDelete; i >= IndexStartToDelete; i--)
            {
                if (i < 0 || i >= points.Count)
                {
                    continue;
                }

                points[i].position = new NodePosition(AddPointTransform.position);
            }

            EditorUtility.SetDirty(WayPointsSO);
        }

        [Button]
        private void TestPoint()
        {
            int depID = GPSTracker.FindClosestPoint(AddPointTransform.position);
            Debug.Log("DepID:" + depID);
            NodePosition pos = GPSTracker.nodes[depID].position;

            _selected = AsV3(pos);
            Debug.Log("Selected:" + _selected);
        }

        [Button]
        private void UpdateGrids()
        {
            using Logger _ = new Logger("WayPointsSO.grid", () => WayPointsSO.grid.Count);
            WayPointsSO.grid.Clear();

            foreach (WayPointNode wayPoint in WayPointsSO.nodes)
            {
                WayPointsSO.AddAreaID(wayPoint.id,
                    GPSTracker.GetAreaID(AsV3(wayPoint)));
            }

            EditorUtility.SetDirty(WayPointsSO);
        }

        public float FilterDistance = 1f;
        [SerializeField] private float NextSphereRadius = 0.5f;
        [SerializeField] private Color NextNodePointerColor = Color.yellow;
        [SerializeField] private Vector3 LookOffset = new Vector3(0, 1, -1);

        [Button]
        private void RemoveFarNextNodes()
        {
            int removed = 0;
            using Logger _ = new Logger("Remove Far Next Nodes", () => removed);
            foreach (WayPointNode node in WayPointsSO.nodes)
            {
                Vector3 pos = AsV3(node);
                int startCount = node.next.Length;
                node.next = node.next
                    .Where(next => (AsV3(WayPointsSO[next]) - pos).sqrMagnitude <= FilterDistance * FilterDistance)
                    .ToArray();
                removed += startCount - node.next.Length;
            }

            EditorUtility.SetDirty(WayPointsSO);
        }

        private bool _enabled;

        [ButtonGroup("Edit Mode"), DisableIf("_enabled")]
        private void Enable()
        {
            _enabled = true;
            SceneView.duringSceneGui += HandleSceneGui;
        }

        [ButtonGroup("Edit Mode"), EnableIf("_enabled")]
        private void Disable()
        {
            _enabled = false;
            SceneView.duringSceneGui -= HandleSceneGui;
        }

        private Vector3? _lastPoint;
        [SerializeField] private float FillStep = 5;


        private void HandleSceneGui(SceneView sceneView)
        {
            Event e = Event.current;
            if (e == null)
                return;
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                if (Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out RaycastHit hit))
                {
                    TryFillWithPoints(hit.point);
                }
            }

            GUIUtility.hotControl = 0;
        }

        private WayPointNode _lastNode;

        private void TryFillWithPoints(Vector3 newPoint)
        {
            if (!_lastPoint.HasValue)
            {
                _lastPoint = newPoint;
                WayPointNode last = new WayPointNode
                {
                    position = new NodePosition(newPoint), id = (uint)WayPointsSO.nodes.Count,
                    next = Array.Empty<uint>()
                };
                WayPointsSO.nodes.Add(last);
                return;
            }

            Vector3 lastPoint = _lastPoint.Value;
            Vector3 delta = newPoint - lastPoint;
            Vector3 dir = delta.normalized;
            Vector3 curr = lastPoint;
            while (curr.FurtherThan(newPoint, FillStep))
            {
                curr += dir * FillStep;
                WayPointNode wayPointNode = new WayPointNode
                {
                    position = new NodePosition(curr), id = (uint)WayPointsSO.nodes.Count
                };
                _lastNode.next = new[] { wayPointNode.id };
                WayPointsSO.nodes.Add(wayPointNode);
                _lastNode = wayPointNode;
            }

            WayPointNode newPointNode = new WayPointNode
            {
                position = new NodePosition(newPoint), id = (uint)WayPointsSO.nodes.Count, next = Array.Empty<uint>()
            };
            if (_lastNode != null)
                _lastNode.next = new[] { newPointNode.id };
            WayPointsSO.nodes.Add(newPointNode);
            _lastNode = newPointNode;
            _lastPoint = newPoint;
            EditorUtility.SetDirty(WayPointsSO);
        }


        private void OnValidate()
        {
            _style = new GUIStyle
            {
                fontSize = FontSize,
                normal =
                {
                    textColor = ColorMain
                },
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnDrawGizmos()
        {
            List<WayPointNode> points = WayPointsSO.nodes;
            
            for (int i = MinIndex; i < MaxIndex; i++)
            {
                if (i < 0)
                {
                    continue;
                }

                if (i >= points.Count)
                {
                    break;
                }

                WayPointNode point = points[i];
                Vector3 pos = AsV3(point);

                float add = 1;
                bool selected = false;
                if (i == IndexStartToDelete || i == IndexEndToDelete)
                {
                    add = 3;
                    selected = true;
                }

                if (i % Step == 0)
                {
                    Gizmos.color = selected ? ColorSelected : ColorMain;
                    Gizmos.DrawCube(pos, Vector3.one * RadiusGizmo2 * add);

                    Gizmos.color = selected ? ColorSelected : ColorMain;
                    Vector3 up = Vector3.up * UpTextValue;

                    if (ShowIndices)
                    {
                        Handles.color = i == IndexCenter ? ColorMain : ColorSecond;
                        string text1 = $"Index: {i}. Index NODE:{point.id}";
                        Handles.Label(pos + Vector3.right * RadiusGizmo2 + up, text1, _style);
                    }

                    if (ShowCoordinates)
                    {
                        Handles.color = ColorSecond;
                        string text2 = $"Pos: {pos}";
                        Handles.Label(pos - Vector3.forward * RadiusGizmo2 + up, text2, _style);
                    }
                }
                else
                {
                    if (ShowSecondPriorityPoints)
                    {
                        Gizmos.color = ColorSecond;
                        Gizmos.DrawWireSphere(pos, RadiusGizmo * add);
                    }
                }
            }

            Vector3 arrowOffset = Vector3.up * .4f;
            for (int i = MinIndex; i < MaxIndex; i++)
            {
                if (i < 0)
                {
                    continue;
                }

                if (i >= points.Count)
                {
                    break;
                }

                WayPointNode point = points[i];
                Vector3 pos = AsV3(point);

                if (i % Step == 0)
                {
                    foreach (uint u in point.next)
                    {
                        Vector3 nextPos = AsV3(points[(int)u]);
                        Gizmos.color = NextNodePointerColor;
                        Gizmos.DrawLine(pos, nextPos);
                        Vector3 offset = pos + (nextPos - pos) * .8f;

                        Gizmos.DrawLine(nextPos, offset + arrowOffset);
                        Gizmos.DrawLine(nextPos, offset - arrowOffset);
                    }
                }
            }

            if (!_selected.HasValue)
                return;
            Gizmos.color = ColorSelected2;
            Gizmos.DrawCube(_selected.Value, Vector3.one * RadiusGizmo2 * 3);
        }

        private static Vector3 AsV3(WayPointNode node) => AsV3(node.position);

        private static Vector3 AsV3(NodePosition node) => new Vector3(node.x, node.y, node.z);

        private class Logger : IDisposable
        {
            private readonly string _description;
            private readonly Func<object> _object;

            public Logger(string description, Func<object> o)
            {
                _description = description;
                _object = o;
                Debug.Log($"BEFORE: {_description} : {_object()}");
            }

            public void Dispose()
            {
                Debug.Log($"AFTER: {_description} : {_object()}");
            }
        }
    }
}

#endif