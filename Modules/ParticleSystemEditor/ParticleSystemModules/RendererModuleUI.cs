// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using System;

namespace UnityEditor
{
    internal class RendererModuleUI : ModuleUI
    {
        // Keep in sync with the one in ParticleSystemRenderer.h
        const int k_MaxNumMeshes = 4;

        // BaseRenderer and Renderer
        SerializedProperty m_CastShadows;
        SerializedProperty m_ReceiveShadows;
        SerializedProperty m_ShadowBias;
        SerializedProperty m_StaticShadowCaster;
        SerializedProperty m_MotionVectors;
        SerializedProperty m_Material;
        SerializedProperty m_SortingOrder;
        SerializedProperty m_SortingLayerID;
        SerializedProperty m_RenderingLayerMask;
        SerializedProperty m_RendererPriority;

        // From ParticleSystemRenderer
        SerializedProperty m_RenderMode;
        SerializedProperty m_MeshDistribution;
        SerializedProperty[] m_Meshes = new SerializedProperty[k_MaxNumMeshes];
        SerializedProperty[] m_MeshWeightings = new SerializedProperty[k_MaxNumMeshes];
        SerializedProperty[] m_ShownMeshes;
        SerializedProperty[] m_ShownMeshWeightings;

        SerializedProperty m_MinParticleSize;   //< How small is a particle allowed to be on screen at least? 1 is entire viewport. 0.5 is half viewport.
        SerializedProperty m_MaxParticleSize;   //< How large is a particle allowed to be on screen at most? 1 is entire viewport. 0.5 is half viewport.
        SerializedProperty m_CameraVelocityScale; //< How much the camera motion is factored in when determining particle stretching.
        SerializedProperty m_VelocityScale;     //< When Stretch Particles is enabled, defines the length of the particle compared to its velocity.
        SerializedProperty m_LengthScale;       //< When Stretch Particles is enabled, defines the length of the particle compared to its width.
        SerializedProperty m_SortMode;          //< What method of particle sorting to use. If none is specified, no sorting will occur.
        SerializedProperty m_SortingFudge;      //< Lower the number, most likely that these particles will appear in front of other transparent objects, including other particles.
        SerializedProperty m_NormalDirection;
        SerializedProperty m_AllowRoll;
        SerializedProperty m_FreeformStretching;
        SerializedProperty m_RotateWithStretchDirection;
        RendererEditorBase.Probes m_Probes;

        SerializedProperty m_RenderAlignment;
        SerializedProperty m_Pivot;
        SerializedProperty m_Flip;
        SerializedProperty m_UseCustomVertexStreams;
        SerializedProperty m_UseCustomTrailVertexStreams;
        SerializedProperty m_VertexStreams;
        SerializedProperty m_TrailVertexStreams;
        SerializedProperty m_MaskInteraction;
        SerializedProperty m_EnableGPUInstancing;
        SerializedProperty m_ApplyActiveColorSpace;

        ReorderableList m_VertexStreamsList;
        ReorderableList m_TrailVertexStreamsList;

        private class VertexStreamsStats
        {
            public int m_NumTexCoords;
            public int m_TexCoordChannelIndex;
            public int m_NumInstancedStreams;
            public bool m_HasTangent;
            public bool m_HasColor;
            public bool m_HasGPUInstancing;
        }

        VertexStreamsStats m_VertexStreamsStats;
        VertexStreamsStats m_TrailVertexStreamsStats;

        static PrefColor s_PivotColor = new PrefColor("Particle System/Pivot", 0.0f, 1.0f, 0.0f, 1.0f);
        static bool s_VisualizePivot = false;

        // Keep in sync with ParticleSystemRenderMode in ParticleSystemRenderer.h
        enum RenderMode
        {
            Billboard = 0,
            Stretch3D = 1,
            BillboardFixedHorizontal = 2,
            BillboardFixedVertical = 3,
            Mesh = 4,
            None = 5
        }

        private struct VertexStreamInfo
        {
            [Flags]
            public enum Mode
            {
                Particles = 1 << 0,
                Trails = 1 << 1,

                All = Particles | Trails
            };

            public static implicit operator VertexStreamInfo(string n)
            {
                return new VertexStreamInfo
                {
                    name = n,
                    mode = Mode.All
                };
            }

            public string name;
            public Mode mode;
        };

        class Texts
        {
            public GUIContent renderMode = EditorGUIUtility.TrTextContent("Render Mode", "Defines the render mode of the particle renderer.");
            public GUIContent material = EditorGUIUtility.TrTextContent("Material", "Defines the material used to render particles.");
            public GUIContent trailMaterial = EditorGUIUtility.TrTextContent("Trail Material", "Defines the material used to render particle trails.");
            public GUIContent meshes = EditorGUIUtility.TrTextContent("Meshes", "Specifies the Meshes to render for each particle. When using a non-uniform distribution, you can also specify the weightings for each Mesh.");
            public GUIContent meshDistribution = EditorGUIUtility.TrTextContent("Mesh Distribution", "Specifies the method Unity uses to randomly assign Meshes to particles.");
            public GUIContent minParticleSize = EditorGUIUtility.TrTextContent("Min Particle Size", "How small is a particle allowed to be on screen at least? 1 is entire viewport. 0.5 is half viewport.");
            public GUIContent maxParticleSize = EditorGUIUtility.TrTextContent("Max Particle Size", "How large is a particle allowed to be on screen at most? 1 is entire viewport. 0.5 is half viewport.");
            public GUIContent cameraSpeedScale = EditorGUIUtility.TrTextContent("Camera Scale", "How much the camera speed is factored in when determining particle stretching.");
            public GUIContent speedScale = EditorGUIUtility.TrTextContent("Speed Scale", "Defines the length of the particle compared to its speed.");
            public GUIContent lengthScale = EditorGUIUtility.TrTextContent("Length Scale", "Defines the length of the particle compared to its width. This determines the base length of particles when they don't move. A value of 1 is neutral, causing no stretching or squashing.");
            public GUIContent freeformStretching = EditorGUIUtility.TrTextContent("Freeform Stretching", "Enables alternative stretching behavior where particles don't get thin when viewed head-on and where particle rotation can be independent from stretching direction.");
            public GUIContent rotateWithStretchDirection = EditorGUIUtility.TrTextContent("Rotate With Stretch", "Rotate the particles based on the direction they are stretched in. This is added on top of other particle rotation.");
            public GUIContent sortingFudge = EditorGUIUtility.TrTextContent("Sorting Fudge", "Lower the number and most likely these particles will appear in front of other transparent objects, including other particles.");
            public GUIContent sortingFudgeDisabledDueToSortingGroup = EditorGUIUtility.TrTextContent("Sorting Fudge", "This is disabled as the Sorting Group component handles the sorting for this Renderer.");
            public GUIContent sortMode = EditorGUIUtility.TrTextContent("Sort Mode", "Draw order of the particles. They can be sorted by:\n- Distance (from the camera position)\n- Oldest particles in front\n- Youngest prticles in front\n- Depth (distance from the camera plane)");
            public GUIContent rotation = EditorGUIUtility.TrTextContent("Rotation", "Set whether the rotation of the particles is defined in Screen or World space.");
            public GUIContent castShadows = EditorGUIUtility.TrTextContent("Cast Shadows", "Only opaque materials cast shadows");
            public GUIContent receiveShadows = EditorGUIUtility.TrTextContent("Receive Shadows", "Only opaque materials receive shadows. When using deferred rendering, all opaque objects receive shadows.");
            public GUIContent shadowBias = EditorGUIUtility.TrTextContent("Shadow Bias", "Apply a shadow bias to prevent self-shadowing artifacts. The specified value is the proportion of the particle size.");
            public GUIContent staticShadowCaster = EditorGUIUtility.TrTextContent("Static Shadow Caster", " When enabled, Unity considers this renderer as being static for the sake of shadow rendering. If the SRP implements cached shadow maps, this field indicates to the render pipeline what renderers are considered static and what renderers are considered dynamic.");
            public GUIContent motionVectors = EditorGUIUtility.TrTextContent("Motion Vectors", "Specifies whether the Particle System renders 'Per Object Motion', 'Camera Motion', or 'No Motion' vectors to the Camera Motion Vector Texture. Note that there is no built-in support for Per-Particle Motion.");
            public GUIContent normalDirection = EditorGUIUtility.TrTextContent("Normal Direction", "Value between 0.0 and 1.0. If 1.0 is used, normals will point towards camera. If 0.0 is used, normals will point out in the corner direction of the particle.");
            public GUIContent allowRoll = EditorGUIUtility.TrTextContent("Allow Roll", "Allows billboards to roll with the camera. It is often useful to disable this option when using VR.");

            public GUIContent sortingLayer = EditorGUIUtility.TrTextContent("Sorting Layer", "Name of the Renderer's sorting layer.");
            public GUIContent sortingOrder = EditorGUIUtility.TrTextContent("Order in Layer", "Renderer's order within a sorting layer");
            public GUIContent space = EditorGUIUtility.TrTextContent("Render Alignment", "Specifies if the particles face the camera, align to world axes, or stay local to the system's transform.");
            public GUIContent alignedToDirectionSpace = EditorGUIUtility.TrTextContent("Render Alignment", "Specifies if the particles face the camera, align to world axes, or stay local to the system's transform. When using Align to Direction in the Shape module, Particle Systems only support Local and World Render Alignments.");
            public GUIContent pivot = EditorGUIUtility.TrTextContent("Pivot", "Applies an offset to the pivot of particles, as a multiplier of its size.");
            public GUIContent flip = EditorGUIUtility.TrTextContent("Flip", "Cause some particles to be flipped horizontally and/or vertically. (Set between 0 and 1, where a higher value causes more to flip)");
            public GUIContent flipMeshes = EditorGUIUtility.TrTextContent("Flip", "Cause some mesh particles to be flipped along each of their axes. Use a shader with CullMode=None, to avoid inside-out geometry. (Set between 0 and 1, where a higher value causes more to flip)");
            public GUIContent visualizePivot = EditorGUIUtility.TrTextContent("Visualize Pivot", "Render the pivot positions of the particles.");
            public GUIContent useCustomVertexStreams = EditorGUIUtility.TrTextContent("Custom Vertex Streams", "Choose whether to send custom particle data to the shader.");
            public GUIContent useCustomTrailVertexStreams = EditorGUIUtility.TrTextContent("Custom Trail Vertex Streams", "Choose whether to send custom particle trail data to the shader.");
            public GUIContent enableGPUInstancing = EditorGUIUtility.TrTextContent("Enable Mesh GPU Instancing", "When rendering mesh particles, use GPU Instancing on platforms where it is supported, and when using shaders that contain a Procedural Instancing pass (#pragma instancing_options procedural).");
            public GUIContent applyActiveColorSpace = EditorGUIUtility.TrTextContent("Apply Active Color Space", "When using Linear Rendering, particle colors will be converted appropriately before being passed to the GPU.");

            public GUIContent meshGPUInstancingTrailsWarning = EditorGUIUtility.TrTextContent("GPU Instancing does not support using the same shader for the Trails. Please use a different shader, or disable the GPU Instancing checkbox.");

            // Keep in sync with enum in ParticleSystemRenderer.h
            public GUIContent[] particleTypes = new GUIContent[]
            {
                EditorGUIUtility.TrTextContent("Billboard"),
                EditorGUIUtility.TrTextContent("Stretched Billboard"),
                EditorGUIUtility.TrTextContent("Horizontal Billboard"),
                EditorGUIUtility.TrTextContent("Vertical Billboard"),
                EditorGUIUtility.TrTextContent("Mesh"),
                EditorGUIUtility.TrTextContent("None")
            };

            public GUIContent[] sortTypes = new GUIContent[]
            {
                EditorGUIUtility.TrTextContent("None"),
                EditorGUIUtility.TrTextContent("By Distance"),
                EditorGUIUtility.TrTextContent("Oldest in Front"),
                EditorGUIUtility.TrTextContent("Youngest in Front"),
                EditorGUIUtility.TrTextContent("By Depth")
            };

            public GUIContent[] spaces = new GUIContent[]
            {
                EditorGUIUtility.TrTextContent("View"),
                EditorGUIUtility.TrTextContent("World"),
                EditorGUIUtility.TrTextContent("Local"),
                EditorGUIUtility.TrTextContent("Facing"),
                EditorGUIUtility.TrTextContent("Velocity")
            };

            public GUIContent[] alignedToDirectionSpaces = new GUIContent[]
            {
                EditorGUIUtility.TrTextContent("World"),
                EditorGUIUtility.TrTextContent("Local")
            };

            public GUIContent[] localSpace = new GUIContent[]
            {
                EditorGUIUtility.TrTextContent("Local")
            };

            public GUIContent[] motionVectorOptions = new GUIContent[]
            {
                EditorGUIUtility.TrTextContent("Camera Motion Only"),
                EditorGUIUtility.TrTextContent("Per Object Motion"),
                EditorGUIUtility.TrTextContent("Force No Motion")
            };

            public GUIContent maskingMode = EditorGUIUtility.TrTextContent("Masking", "Defines the masking behavior of the particles. See Sprite Masking documentation for more details.");
            public GUIContent[] maskInteractions = new GUIContent[]
            {
                EditorGUIUtility.TrTextContent("No Masking"),
                EditorGUIUtility.TrTextContent("Visible Inside Mask"),
                EditorGUIUtility.TrTextContent("Visible Outside Mask")
            };

            public GUIContent[] meshDistributionOptions = new GUIContent[]
            {
                EditorGUIUtility.TrTextContent("Uniform Random"),
                EditorGUIUtility.TrTextContent("Non-uniform Random")
            };

            private static VertexStreamInfo ParticleVertexStream(string n) { return new VertexStreamInfo { name = n, mode = VertexStreamInfo.Mode.Particles }; }
            private static VertexStreamInfo TrailVertexStream(string n) { return new VertexStreamInfo { name = n, mode = VertexStreamInfo.Mode.Trails }; }

            private string[] vertexStreamsMenu = { "Position", "Normal", "Tangent", "Color", "UV/UV1", "UV/UV2", "UV/UV3", "UV/UV4", "UV/AnimBlend", "UV/AnimFrame", "Center", "VertexID", "Size/Size.x", "Size/Size.xy", "Size/Size.xyz", "Rotation/Rotation", "Rotation/Rotation3D", "Rotation/RotationSpeed", "Rotation/RotationSpeed3D", "Velocity", "Speed", "Lifetime/AgePercent", "Lifetime/InverseStartLifetime", "Random/Stable.x", "Random/Stable.xy", "Random/Stable.xyz", "Random/Stable.xyzw", "Random/Varying.x", "Random/Varying.xy", "Random/Varying.xyz", "Random/Varying.xyzw", "Custom/Custom1.x", "Custom/Custom1.xy", "Custom/Custom1.xyz", "Custom/Custom1.xyzw", "Custom/Custom2.x", "Custom/Custom2.xy", "Custom/Custom2.xyz", "Custom/Custom2.xyzw", "Noise/Sum.x", "Noise/Sum.xy", "Noise/Sum.xyz", "Noise/Impulse.x", "Noise/Impulse.xy", "Noise/Impulse.xyz", "MeshIndex", "ParticleIndex", "ColorPackedAsTwoFloats", "MeshAxisOfRotation", "Trails/NextCenter", "Trails/PreviousCenter", "Trails/PercentageAlongTrail", "Trails/Width" };
            public VertexStreamInfo[] vertexStreamsPacked = { "Position", "Normal", "Tangent", "Color", "UV", ParticleVertexStream("UV2"), ParticleVertexStream("UV3"), ParticleVertexStream("UV4"), "AnimBlend", "AnimFrame", "Center", "VertexID", "Size", "Size.xy", "Size.xyz", "Rotation", "Rotation3D", "RotationSpeed", "RotationSpeed3D", "Velocity", "Speed", "AgePercent", "InverseStartLifetime", "StableRandom.x", "StableRandom.xy", "StableRandom.xyz", "StableRandom.xyzw", "VariableRandom.x", "VariableRandom.xy", "VariableRandom.xyz", "VariableRandom.xyzw", "Custom1.x", "Custom1.xy", "Custom1.xyz", "Custom1.xyzw", "Custom2.x", "Custom2.xy", "Custom2.xyz", "Custom2.xyzw", "NoiseSum.x", "NoiseSum.xy", "NoiseSum.xyz", "NoiseImpulse.x", "NoiseImpulse.xy", "NoiseImpulse.xyz", ParticleVertexStream("MeshIndex"), "ParticleIndex", "ColorPackedAsTwoFloats", "MeshAxisOfRotation", TrailVertexStream("NextCenter"), TrailVertexStream("PreviousCenter"), TrailVertexStream("PercentageAlongTrail"), TrailVertexStream("TrailWidth") }; // Keep in sync with enums in ParticleSystemRenderer.h and ParticleSystemEnums.cs
            public string[] vertexStreamPackedTypes = { "POSITION.xyz", "NORMAL.xyz", "TANGENT.xyzw", "COLOR.xyzw" }; // all other types are floats
            public int[] vertexStreamTexCoordChannels = { 0, 0, 0, 0, 2, 2, 2, 2, 1, 1, 3, 1, 1, 2, 3, 1, 3, 1, 3, 3, 1, 1, 1, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 1, 2, 3, 1, 1, 2, 3, 3, 3, 1, 1 };
            public string channels = "xyzw|xyz";
            public int vertexStreamsInstancedStart = 8;

            public GUIContent[] vertexStreamsMenuContent;

            public Texts()
            {
                vertexStreamsMenuContent = vertexStreamsMenu.Select(x => new GUIContent(x)).ToArray();
            }
        }
        private static Texts s_Texts;

        public RendererModuleUI(ParticleSystemUI owner, SerializedObject o, string displayName)
            : base(owner, o, "ParticleSystemRenderer", displayName, VisibilityState.VisibleAndFolded)
        {
            m_ToolTip = "Specifies how the particles are rendered.";
        }

        protected override void Init()
        {
            if (m_CastShadows != null)
                return;
            if (s_Texts == null)
                s_Texts = new Texts();

            m_CastShadows = GetProperty0("m_CastShadows");
            m_ReceiveShadows = GetProperty0("m_ReceiveShadows");
            m_ShadowBias = GetProperty0("m_ShadowBias");
            m_StaticShadowCaster = GetProperty0("m_StaticShadowCaster");
            m_MotionVectors = GetProperty0("m_MotionVectors");
            m_Material = GetProperty0("m_Materials.Array.data[0]");
            m_SortingOrder = GetProperty0("m_SortingOrder");
            m_RenderingLayerMask = GetProperty0("m_RenderingLayerMask");
            m_RendererPriority = GetProperty0("m_RendererPriority");
            m_SortingLayerID = GetProperty0("m_SortingLayerID");

            m_RenderMode = GetProperty0("m_RenderMode");
            m_MeshDistribution = GetProperty0("m_MeshDistribution");
            m_MinParticleSize = GetProperty0("m_MinParticleSize");
            m_MaxParticleSize = GetProperty0("m_MaxParticleSize");
            m_CameraVelocityScale = GetProperty0("m_CameraVelocityScale");
            m_VelocityScale = GetProperty0("m_VelocityScale");
            m_LengthScale = GetProperty0("m_LengthScale");
            m_SortingFudge = GetProperty0("m_SortingFudge");
            m_SortMode = GetProperty0("m_SortMode");
            m_NormalDirection = GetProperty0("m_NormalDirection");
            m_AllowRoll = GetProperty0("m_AllowRoll");
            m_FreeformStretching = GetProperty0("m_FreeformStretching");
            m_RotateWithStretchDirection = GetProperty0("m_RotateWithStretchDirection");

            m_Probes = new RendererEditorBase.Probes();
            m_Probes.Initialize(serializedObject);

            m_RenderAlignment = GetProperty0("m_RenderAlignment");
            m_Pivot = GetProperty0("m_Pivot");
            m_Flip = GetProperty0("m_Flip");

            m_Meshes[0] = GetProperty0("m_Mesh");
            m_Meshes[1] = GetProperty0("m_Mesh1");
            m_Meshes[2] = GetProperty0("m_Mesh2");
            m_Meshes[3] = GetProperty0("m_Mesh3");

            m_MeshWeightings[0] = GetProperty0("m_MeshWeighting");
            m_MeshWeightings[1] = GetProperty0("m_MeshWeighting1");
            m_MeshWeightings[2] = GetProperty0("m_MeshWeighting2");
            m_MeshWeightings[3] = GetProperty0("m_MeshWeighting3");

            List<SerializedProperty> shownMeshes = new List<SerializedProperty>();
            List<SerializedProperty> shownMeshWeightings = new List<SerializedProperty>();
            for (int i = 0; i < m_Meshes.Length; ++i)
            {
                // Always show the first mesh
                if (i == 0 || m_Meshes[i].objectReferenceValue != null)
                {
                    shownMeshes.Add(m_Meshes[i]);
                    shownMeshWeightings.Add(m_MeshWeightings[i]);
                }
            }
            m_ShownMeshes = shownMeshes.ToArray();
            m_ShownMeshWeightings = shownMeshWeightings.ToArray();

            m_MaskInteraction = GetProperty0("m_MaskInteraction");

            m_EnableGPUInstancing = GetProperty0("m_EnableGPUInstancing");
            m_ApplyActiveColorSpace = GetProperty0("m_ApplyActiveColorSpace");

            m_UseCustomVertexStreams = GetProperty0("m_UseCustomVertexStreams");
            m_UseCustomTrailVertexStreams = GetProperty0("m_UseCustomTrailVertexStreams");

            m_VertexStreams = GetProperty0("m_VertexStreams");
            m_VertexStreamsList = new ReorderableList(serializedObject, m_VertexStreams, true, true, true, true);
            m_VertexStreamsList.elementHeight = kReorderableListElementHeight;
            m_VertexStreamsList.headerHeight = 0;
            m_VertexStreamsList.onAddDropdownCallback = OnVertexStreamListAddDropdownCallback;
            m_VertexStreamsList.onCanRemoveCallback = OnVertexStreamListCanRemoveCallback;
            m_VertexStreamsList.drawElementCallback = DrawParticleVertexStreamListElementCallback;

            m_TrailVertexStreams = GetProperty0("m_TrailVertexStreams");
            m_TrailVertexStreamsList = new ReorderableList(serializedObject, m_TrailVertexStreams, true, true, true, true);
            m_TrailVertexStreamsList.elementHeight = kReorderableListElementHeight;
            m_TrailVertexStreamsList.headerHeight = 0;
            m_TrailVertexStreamsList.onAddDropdownCallback = OnTrailVertexStreamListAddDropdownCallback;
            m_TrailVertexStreamsList.onCanRemoveCallback = OnVertexStreamListCanRemoveCallback;
            m_TrailVertexStreamsList.drawElementCallback = DrawTrailVertexStreamListElementCallback;

            s_VisualizePivot = EditorPrefs.GetBool("VisualizePivot", false);
        }

        override public void OnInspectorGUI(InitialModuleUI initial)
        {
            var renderer = serializedObject.targetObject as Renderer;

            EditorGUI.BeginChangeCheck();
            RenderMode renderMode = (RenderMode)GUIPopup(s_Texts.renderMode, m_RenderMode, s_Texts.particleTypes);
            bool renderModeChanged = EditorGUI.EndChangeCheck();

            if (!m_RenderMode.hasMultipleDifferentValues)
            {
                if (renderMode == RenderMode.Mesh)
                {
                    GUIPopup(s_Texts.meshDistribution, m_MeshDistribution, s_Texts.meshDistributionOptions);
                    DoListOfMeshesGUI();

                    if (renderModeChanged && m_Meshes[0].objectReferenceInstanceIDValue == 0 && !m_Meshes[0].hasMultipleDifferentValues)
                        m_Meshes[0].objectReferenceValue = Resources.GetBuiltinResource(typeof(Mesh), "Cube.fbx");
                }
                else if (renderMode == RenderMode.Stretch3D)
                {
                    EditorGUI.indentLevel++;
                    GUIFloat(s_Texts.cameraSpeedScale, m_CameraVelocityScale);
                    GUIFloat(s_Texts.speedScale, m_VelocityScale);
                    GUIFloat(s_Texts.lengthScale, m_LengthScale);
                    GUIToggle(s_Texts.freeformStretching, m_FreeformStretching);
                    if (m_FreeformStretching.boolValue && !m_FreeformStretching.hasMultipleDifferentValues)
                    {
                        GUIToggle(s_Texts.rotateWithStretchDirection, m_RotateWithStretchDirection);
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            GUIToggle(s_Texts.rotateWithStretchDirection, true);
                        }
                    }
                    EditorGUI.indentLevel--;
                }

                if (renderMode != RenderMode.None)
                {
                    if (renderMode != RenderMode.Mesh)
                        GUIFloat(s_Texts.normalDirection, m_NormalDirection);
                }
            }

            if (renderMode != RenderMode.None)
            {
                if (m_Material != null) // The renderer's material list could be empty
                    GUIObject(s_Texts.material, m_Material);
            }

            var trailMaterial = serializedObject.FindProperty("m_Materials.Array.data[1]"); // Optional - may fail
            if (trailMaterial != null) // Only show if the system has a second material slot
                GUIObject(s_Texts.trailMaterial, trailMaterial);

            if (renderMode != RenderMode.None)
            {
                if (!m_RenderMode.hasMultipleDifferentValues)
                {
                    GUIPopup(s_Texts.sortMode, m_SortMode, s_Texts.sortTypes);
                    if (renderer != null && SortingGroup.invalidSortingGroupID != renderer.sortingGroupID)
                    {
                        using (new EditorGUI.DisabledScope(true))
                            GUIFloat(s_Texts.sortingFudgeDisabledDueToSortingGroup, m_SortingFudge);
                    }
                    else
                    {
                        GUIFloat(s_Texts.sortingFudge, m_SortingFudge);
                    }

                    if (renderMode != RenderMode.Mesh)
                    {
                        GUIFloat(s_Texts.minParticleSize, m_MinParticleSize);
                        GUIFloat(s_Texts.maxParticleSize, m_MaxParticleSize);
                    }

                    if (renderMode == RenderMode.Billboard || renderMode == RenderMode.Mesh)
                    {
                        bool anyAlignToDirection = m_ParticleSystemUI.m_ParticleSystems.FirstOrDefault(o => o.shape.enabled && o.shape.alignToDirection) != null;
                        if (anyAlignToDirection)
                        {
                            EditorGUI.BeginChangeCheck();
                            int selectedIndex = (m_RenderAlignment.hasMultipleDifferentValues || m_RenderAlignment.intValue == (int)ParticleSystemRenderSpace.World) ? 0 : 1;
                            selectedIndex = GUIPopup(s_Texts.alignedToDirectionSpace, selectedIndex, s_Texts.alignedToDirectionSpaces);
                            if (EditorGUI.EndChangeCheck())
                                m_RenderAlignment.intValue = (selectedIndex == 0) ? (int)ParticleSystemRenderSpace.World : (int)ParticleSystemRenderSpace.Local;
                        }
                        else
                        {
                            GUIPopup(s_Texts.space, m_RenderAlignment, s_Texts.spaces);
                        }
                    }

                    if (renderMode == RenderMode.Mesh)
                        GUIVector3Field(s_Texts.flipMeshes, m_Flip);
                    else
                        GUIVector3Field(s_Texts.flip, m_Flip);

                    if (renderMode == RenderMode.Billboard)
                        GUIToggle(s_Texts.allowRoll, m_AllowRoll);

                    if (renderMode == RenderMode.Mesh && SupportedRenderingFeatures.active.particleSystemInstancing)
                    {
                        GUIToggle(s_Texts.enableGPUInstancing, m_EnableGPUInstancing);

                        if (!m_ParticleSystemUI.multiEdit && m_EnableGPUInstancing.boolValue)
                        {
                            Material materialAsset = m_Material?.objectReferenceValue as Material;
                            Material trailMaterialAsset = trailMaterial?.objectReferenceValue as Material;

                            if (materialAsset != null && trailMaterialAsset != null)
                            {
                                if (trailMaterialAsset.shader == materialAsset.shader)
                                {
                                    if (ShaderUtil.HasProceduralInstancing(materialAsset.shader))
                                        EditorGUILayout.HelpBox(s_Texts.meshGPUInstancingTrailsWarning.text, MessageType.Error, true);
                                }
                            }
                        }
                    }
                }

                GUIVector3Field(s_Texts.pivot, m_Pivot);

                if (EditorGUIUtility.comparisonViewMode == EditorGUIUtility.ComparisonViewMode.None)
                {
                    EditorGUI.BeginChangeCheck();
                    s_VisualizePivot = GUIToggle(s_Texts.visualizePivot, s_VisualizePivot);
                    if (EditorGUI.EndChangeCheck())
                        EditorPrefs.SetBool("VisualizePivot", s_VisualizePivot);
                }

                GUIPopup(s_Texts.maskingMode, m_MaskInteraction, s_Texts.maskInteractions);
                GUIToggle(s_Texts.applyActiveColorSpace, m_ApplyActiveColorSpace);

                if (GUIToggle(s_Texts.useCustomVertexStreams, m_UseCustomVertexStreams))
                    DoVertexStreamsGUI(m_VertexStreamsList, ref m_VertexStreamsStats, m_Material, renderMode);

                if (trailMaterial != null) // Only show if the system has a second material slot
                {
                    if (GUIToggle(s_Texts.useCustomTrailVertexStreams, m_UseCustomTrailVertexStreams))
                        DoVertexStreamsGUI(m_TrailVertexStreamsList, ref m_TrailVertexStreamsStats, trailMaterial, renderMode);
                }
            }

            EditorGUILayout.Space();

            GUIPopup(s_Texts.castShadows, m_CastShadows, EditorGUIUtility.TempContent(m_CastShadows.enumDisplayNames));

            if (m_CastShadows.hasMultipleDifferentValues || m_CastShadows.intValue != 0)
            {
                RenderPipelineAsset srpAsset = GraphicsSettings.currentRenderPipeline;
                if (srpAsset != null)
                    GUIToggle(s_Texts.staticShadowCaster, m_StaticShadowCaster);
            }

            if (SupportedRenderingFeatures.active.receiveShadows)
            {
                // Disable ReceiveShadows options for Deferred rendering path
                if (SceneView.IsUsingDeferredRenderingPath())
                {
                    using (new EditorGUI.DisabledScope(true)) { GUIToggle(s_Texts.receiveShadows, true); }
                }
                else
                {
                    GUIToggle(s_Texts.receiveShadows, m_ReceiveShadows);
                }
            }

            if (renderMode != RenderMode.Mesh)
                GUIFloat(s_Texts.shadowBias, m_ShadowBias);

            if (SupportedRenderingFeatures.active.motionVectors)
                GUIPopup(s_Texts.motionVectors, m_MotionVectors, s_Texts.motionVectorOptions);

            GUISortingLayerField(s_Texts.sortingLayer, m_SortingLayerID);
            GUIInt(s_Texts.sortingOrder, m_SortingOrder);

            List<ParticleSystemRenderer> renderers = new List<ParticleSystemRenderer>();
            foreach (ParticleSystem ps in m_ParticleSystemUI.m_ParticleSystems)
            {
                renderers.Add(ps.GetComponent<ParticleSystemRenderer>());
            }
            var renderersArray = renderers.ToArray();
            m_Probes.OnGUI(renderersArray, renderers.FirstOrDefault(), true);

            RendererEditorBase.DrawRenderingLayer(m_RenderingLayerMask, renderer, renderersArray, true);
            RendererEditorBase.DrawRendererPriority(m_RendererPriority, true);
        }

        private void DoListOfMeshesGUI()
        {
            var additionalProperties = (m_MeshDistribution.hasMultipleDifferentValues || m_MeshDistribution.intValue == (int)ParticleSystemMeshDistribution.UniformRandom) ? null : m_ShownMeshWeightings;
            GUIListOfObjectFields(s_Texts.meshes, m_ShownMeshes, additionalProperties);

            // Minus button
            Rect rect = GetControlRect((int)EditorGUI.kSingleLineHeight);
            rect.x = rect.xMax - kPlusAddRemoveButtonWidth * 2 - kPlusAddRemoveButtonSpacing;
            rect.width = kPlusAddRemoveButtonWidth;
            if (m_ShownMeshes.Length > 1)
            {
                if (MinusButton(rect))
                {
                    m_ShownMeshes[m_ShownMeshes.Length - 1].objectReferenceValue = null;

                    List<SerializedProperty> shownMeshes = new List<SerializedProperty>(m_ShownMeshes);
                    List<SerializedProperty> shownMeshWeightings = new List<SerializedProperty>(m_ShownMeshWeightings);

                    shownMeshes.RemoveAt(shownMeshes.Count - 1);
                    shownMeshWeightings.RemoveAt(shownMeshWeightings.Count - 1);

                    m_ShownMeshes = shownMeshes.ToArray();
                    m_ShownMeshWeightings = shownMeshWeightings.ToArray();
                }
            }

            // Plus button
            if (m_ShownMeshes.Length < k_MaxNumMeshes && !m_ParticleSystemUI.multiEdit)
            {
                rect.x += kPlusAddRemoveButtonWidth + kPlusAddRemoveButtonSpacing;
                if (PlusButton(rect))
                {
                    List<SerializedProperty> shownMeshes = new List<SerializedProperty>(m_ShownMeshes);
                    List<SerializedProperty> shownMeshWeightings = new List<SerializedProperty>(m_ShownMeshWeightings);

                    shownMeshes.Add(m_Meshes[shownMeshes.Count]);
                    shownMeshWeightings.Add(m_MeshWeightings[shownMeshWeightings.Count]);

                    m_ShownMeshes = shownMeshes.ToArray();
                    m_ShownMeshWeightings = shownMeshWeightings.ToArray();
                }
            }
        }

        private class StreamCallbackData
        {
            public StreamCallbackData(UnityEditorInternal.ReorderableList l, SerializedProperty prop, int s)
            {
                list = l;
                streamProp = prop;
                stream = s;
            }

            public UnityEditorInternal.ReorderableList list;
            public SerializedProperty streamProp;
            public int stream;
        }

        private void SelectVertexStreamCallback(object obj)
        {
            StreamCallbackData data = (StreamCallbackData)obj;

            ReorderableList.defaultBehaviours.DoAddButton(data.list);

            var element = data.streamProp.GetArrayElementAtIndex(data.list.index);
            element.intValue = data.stream;

            m_ParticleSystemUI.m_RendererSerializedObject.ApplyModifiedProperties();
        }

        private void DoVertexStreamsGUI(ReorderableList list, ref VertexStreamsStats stats, SerializedProperty serializedMaterial, RenderMode renderMode)
        {
            ParticleSystemRenderer renderer = m_ParticleSystemUI.m_ParticleSystems[0].GetComponent<ParticleSystemRenderer>();
            bool hasGPUInstancing = false;
            if (renderMode == RenderMode.Mesh && list == m_VertexStreamsList)
                hasGPUInstancing = renderer.supportsMeshInstancing;

            // render list
            stats = new VertexStreamsStats();
            stats.m_HasGPUInstancing = hasGPUInstancing;
            list.DoLayoutList();

            if (!m_ParticleSystemUI.multiEdit)
            {
                // error messages
                string errors = "";

                // check we have the same streams as the assigned shader
                if (serializedMaterial != null)
                {
                    Material material = serializedMaterial.objectReferenceValue as Material;
                    int totalChannelCount = stats.m_NumTexCoords * 4 + stats.m_TexCoordChannelIndex;
                    bool tangentError = false, colorError = false, uvError = false;
                    bool anyErrors = ParticleSystem.CheckVertexStreamsMatchShader(stats.m_HasTangent, stats.m_HasColor, totalChannelCount, material, ref tangentError, ref colorError, ref uvError);
                    if (anyErrors)
                    {
                        errors += "Vertex streams do not match the shader inputs. Particle systems may not render correctly. Ensure your streams match and are used by the shader.";
                        if (tangentError)
                            errors += "\n- TANGENT stream does not match.";
                        if (colorError)
                            errors += "\n- COLOR stream does not match.";
                        if (uvError)
                            errors += "\n- TEXCOORD streams do not match.";
                    }
                }

                // check we aren't using too many texcoords
                int maxTexCoords = ParticleSystem.GetMaxTexCoordStreams();
                if (stats.m_NumTexCoords > maxTexCoords || (stats.m_NumTexCoords == maxTexCoords && stats.m_TexCoordChannelIndex > 0))
                {
                    if (errors != "")
                        errors += "\n\n";
                    errors += "Only " + maxTexCoords + " TEXCOORD streams are supported.";
                }

                // check input meshes aren't using too many UV streams
                if (renderMode == RenderMode.Mesh && list == m_VertexStreamsList)
                {
                    Mesh[] meshes = new Mesh[k_MaxNumMeshes];
                    int numMeshes = renderer.GetMeshes(meshes);
                    for (int i = 0; i < numMeshes; i++)
                    {
                        if (meshes[i].HasVertexAttribute(VertexAttribute.TexCoord4))
                        {
                            if (errors != "")
                                errors += "\n\n";
                            errors += "Meshes may only use a maximum of 4 input UV streams.";
                        }
                    }
                }

                if (errors != "")
                {
                    GUIContent warning = EditorGUIUtility.TextContent(errors);
                    EditorGUILayout.HelpBox(warning.text, MessageType.Error, true);
                }
            }
        }

        private void OnVertexStreamListAddDropdownCallback(Rect rect, ReorderableList list)
        {
            OnVertexStreamListAddDropdownCallbackInternal(rect, list, VertexStreamInfo.Mode.Particles);

        }

        private void OnTrailVertexStreamListAddDropdownCallback(Rect rect, ReorderableList list)
        {
            OnVertexStreamListAddDropdownCallbackInternal(rect, list, VertexStreamInfo.Mode.Trails);
        }

        private void OnVertexStreamListAddDropdownCallbackInternal(Rect rect, ReorderableList list, VertexStreamInfo.Mode mode)
        {
            SerializedProperty vertexStreams = list.serializedProperty;

            var notEnabled = new List<int>();
            for (int i = 0; i < s_Texts.vertexStreamsPacked.Length; ++i)
            {
                if ((s_Texts.vertexStreamsPacked[i].mode & mode) != 0)
                {
                    bool exists = false;
                    for (int j = 0; j < vertexStreams.arraySize; ++j)
                    {
                        if (vertexStreams.GetArrayElementAtIndex(j).intValue == i)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                        notEnabled.Add(i);
                }
            }

            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < notEnabled.Count; ++i)
                menu.AddItem(s_Texts.vertexStreamsMenuContent[notEnabled[i]], false, SelectVertexStreamCallback, new StreamCallbackData(list, vertexStreams, notEnabled[i]));
            menu.ShowAsContext();
            Event.current.Use();
        }

        private static bool OnVertexStreamListCanRemoveCallback(ReorderableList list)
        {
            SerializedProperty vertexStreams = list.serializedProperty;

            // dont allow position stream to be removed
            SerializedProperty vertexStream = vertexStreams.GetArrayElementAtIndex(list.index);
            return (s_Texts.vertexStreamsPacked[vertexStream.intValue].name != "Position");
        }

        private void DrawParticleVertexStreamListElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            DrawVertexStreamListElementCallback(rect, index, isActive, isFocused, m_VertexStreamsList, m_VertexStreamsStats, isWindowView);
        }

        private void DrawTrailVertexStreamListElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            DrawVertexStreamListElementCallback(rect, index, isActive, isFocused, m_TrailVertexStreamsList, m_TrailVertexStreamsStats, isWindowView);
        }

        private static void DrawVertexStreamListElementCallback(Rect rect, int index, bool isActive, bool isFocused, ReorderableList list, VertexStreamsStats stats, bool windowed)
        {
            SerializedProperty vertexStreams = list.serializedProperty;

            SerializedProperty vertexStream = vertexStreams.GetArrayElementAtIndex(index);
            int vertexStreamValue = vertexStream.intValue;
            string tcName = windowed ? "TEX" : "TEXCOORD";
            string instancedName = windowed ? "INST" : "INSTANCED";

            int numChannels = s_Texts.vertexStreamTexCoordChannels[vertexStreamValue];
            if (stats.m_HasGPUInstancing && (vertexStreamValue >= s_Texts.vertexStreamsInstancedStart || s_Texts.vertexStreamsPacked[vertexStream.intValue].name == "Color"))
            {
                if (s_Texts.vertexStreamsPacked[vertexStream.intValue].name == "Color")
                {
                    numChannels = 4;
                    stats.m_HasColor = true;
                }
                string swizzle = s_Texts.channels.Substring(0, numChannels);
                GUI.Label(rect, s_Texts.vertexStreamsPacked[vertexStreamValue].name + " (" + instancedName + stats.m_NumInstancedStreams + "." + swizzle + ")", ParticleSystemStyles.Get().label);
                stats.m_NumInstancedStreams++;
            }
            else if (numChannels != 0)
            {
                int swizzleLength = (stats.m_TexCoordChannelIndex + numChannels > 4) ? numChannels + 1 : numChannels;
                string swizzle = s_Texts.channels.Substring(stats.m_TexCoordChannelIndex, swizzleLength);
                GUI.Label(rect, s_Texts.vertexStreamsPacked[vertexStreamValue].name + " (" + tcName + stats.m_NumTexCoords + "." + swizzle + ")", ParticleSystemStyles.Get().label);
                stats.m_TexCoordChannelIndex += numChannels;
                if (stats.m_TexCoordChannelIndex >= 4)
                {
                    stats.m_TexCoordChannelIndex -= 4;
                    stats.m_NumTexCoords++;
                }
            }
            else
            {
                GUI.Label(rect, s_Texts.vertexStreamsPacked[vertexStreamValue].name + " (" + s_Texts.vertexStreamPackedTypes[vertexStreamValue] + ")", ParticleSystemStyles.Get().label);
                if (s_Texts.vertexStreamsPacked[vertexStreamValue].name == "Tangent")
                    stats.m_HasTangent = true;
                if (s_Texts.vertexStreamsPacked[vertexStreamValue].name == "Color")
                    stats.m_HasColor = true;
            }
        }

        // render pivots
        override public void OnSceneViewGUI()
        {
            if (s_VisualizePivot == false)
                return;

            Color oldColor = Handles.color;
            Handles.color = s_PivotColor;
            Matrix4x4 oldMatrix = Handles.matrix;

            Vector3[] lineSegments = new Vector3[6];

            foreach (ParticleSystem ps in m_ParticleSystemUI.m_ParticleSystems)
            {
                ParticleSystem.Particle[] particles = new ParticleSystem.Particle[ps.particleCount];
                int count = ps.GetParticles(particles);

                Matrix4x4 transform = Matrix4x4.identity;
                if (ps.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                {
                    transform = ps.localToWorldMatrix;
                }
                Handles.matrix = transform;

                for (int i = 0; i < count; i++)
                {
                    ParticleSystem.Particle particle = particles[i];
                    Vector3 size = particle.GetCurrentSize3D(ps) * 0.05f;

                    lineSegments[0] = particle.position - (Vector3.right * size.x);
                    lineSegments[1] = particle.position + (Vector3.right * size.x);

                    lineSegments[2] = particle.position - (Vector3.up * size.y);
                    lineSegments[3] = particle.position + (Vector3.up * size.y);

                    lineSegments[4] = particle.position - (Vector3.forward * size.z);
                    lineSegments[5] = particle.position + (Vector3.forward * size.z);

                    Handles.DrawLines(lineSegments);
                }
            }

            Handles.color = oldColor;
            Handles.matrix = oldMatrix;
        }
    }
} // namespace UnityEditor
