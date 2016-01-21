using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental;
using UnityEditor.Experimental.Graph;
using UnityEngine;

namespace UnityEditor.MaterialGraph
{

    public sealed class DrawableMaterialNode : CanvasElement
    {
        private string m_Title;
        private readonly MaterialGraphDataSource m_Data;
        public BaseMaterialNode m_Node;
        private bool m_Error;

        private Rect m_PreviewArea;
        private Rect m_NodeUIRect;

        public DrawableMaterialNode(BaseMaterialNode node, float width, Type outputType, MaterialGraphDataSource data)
        {
            translation = node.position.min;
            scale = new Vector2(width, width);

            m_Node = node;
            m_Title = node.name;
            m_Data = data;
            m_Error = node.hasError;

            const float yStart = 10.0f;
            var vector3 = new Vector3(5.0f, yStart, 0.0f);
            Vector3 pos = vector3;

            // input slots
            foreach (var slot in node.inputSlots)
            {
                pos.y += 22;
                AddChild(new NodeAnchor(pos, typeof (Vector4), slot, data, Direction.eInput));
            }
            var inputYMax = pos.y + 22;

            // output port
            pos.x = width;
            pos.y = yStart;
            foreach (var slot in node.outputSlots)
            {
                // don't show empty output slots in collapsed mode
                if (node.drawMode == DrawMode.Collapsed && slot.edges.Count == 0)
                    continue;

                pos.y += 22;
                AddChild(new NodeAnchor(pos, typeof (Vector4), slot, data, Direction.eOutput));
            }
            pos.y += 22;

            pos.y = Mathf.Max(pos.y, inputYMax);

            var nodeUIHeight = m_Node.GetNodeUIHeight(width);
            m_NodeUIRect = new Rect(10, pos.y, width - 20, nodeUIHeight);
            pos.y += nodeUIHeight;

            if (node.hasPreview && node.drawMode != DrawMode.Collapsed)
            { 
                m_PreviewArea = new Rect(10, pos.y, width - 20, width - 20);
                pos.y += m_PreviewArea.height;
            }
            
            scale = new Vector3(pos.x, pos.y + 10.0f, 0.0f);
            
            KeyDown += OnDeleteNode;
            OnWidget += MarkDirtyIfNeedsTime;
            
            AddManipulator(new IMGUIContainer());
            AddManipulator(new Draggable());
        }

        private bool MarkDirtyIfNeedsTime(CanvasElement element, Event e, Canvas2D parent)
        {
            var childrenNodes = m_Node.CollectChildNodesByExecutionOrder();
            if (childrenNodes.Any(x => x is IRequiresTime))
                Invalidate();
            return true;
        }

        private bool OnDeleteNode(CanvasElement element, Event e, Canvas2D canvas)
        {
            if (e.type == EventType.Used)
                return false;

            if (e.keyCode == KeyCode.Delete)
            {
                m_Data.DeleteElement(this);
                return true;
            }
            return false;
        }
        
        public override void UpdateModel(UpdateType t)
        {
            base.UpdateModel(t);
            var pos = m_Node.position;
            pos.min = translation;
            m_Node.position = pos;
            EditorUtility.SetDirty (m_Node);
        }

        public override void Render(Rect parentRect, Canvas2D canvas)
        {
            Color backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.7f);
            Color selectedColor = new Color(1.0f, 0.7f, 0.0f, 0.7f);
            EditorGUI.DrawRect(new Rect(0, 0, scale.x, scale.y), m_Error ? Color.red : selected ? selectedColor : backgroundColor);
            GUI.Label(new Rect(0, 0, scale.x, 26f), GUIContent.none, new GUIStyle("preToolbar"));
            GUI.Label(new Rect(10, 2, scale.x - 20.0f, 16.0f), m_Title, EditorStyles.toolbarTextField);
            if (GUI.Button(new Rect(scale.x - 20f, 3f, 14f, 14f), m_Node.drawMode == DrawMode.Full ? "-" : "+"))
            {
                m_Node.drawMode = m_Node.drawMode == DrawMode.Full ? DrawMode.Collapsed : DrawMode.Full;
                ParentCanvas().ReloadData();
                ParentCanvas().Repaint();
                return;
            }

            if (m_Node.NodeUI(m_NodeUIRect))
            {
                // if we were changed, we need to redraw all the
                // dependent nodes.
                var dependentNodes = m_Node.CollectDependentNodes();
                foreach (var node in dependentNodes)
                {
                    foreach (var drawNode in m_Data.lastGeneratedNodes)
                    {
                        if (drawNode.m_Node == node)
                            drawNode.Invalidate();
                    }
                }
            }

            if (m_Node.hasPreview 
                && m_Node.drawMode != DrawMode.Collapsed 
                && m_PreviewArea.width > 0
                && m_PreviewArea.height > 0)
            {
                GL.sRGBWrite = (QualitySettings.activeColorSpace == ColorSpace.Linear);
                GUI.DrawTexture(m_PreviewArea, m_Node.RenderPreview(new Rect(0, 0, m_PreviewArea.width, m_PreviewArea.height)), ScaleMode.StretchToFill, false);
                GL.sRGBWrite = false;
            }
            
            base.Render(parentRect, canvas);
        }
        
        public static void OnGUI(List<CanvasElement> selection)
        {
            foreach (var selected in selection.Where(x => x is DrawableMaterialNode).Cast<DrawableMaterialNode>())
            {
                selected.m_Node.OnGUI();
                break;
            }
        }
    }
}