#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteSkinTransfer
{
    [CreateAssetMenu(menuName = "Custom 2D/Sprite Skin Transfer", fileName = "Skin Transfer")]
    public class SpriteSkinTransfer : ScriptableObject
    {
        public Transfer[] transfers = new Transfer[1] { new Transfer() };

        [System.Serializable]
        public class Transfer
        {
            [Tooltip("The target .psb asset")]
            public Object target;
            [Tooltip("In case you want to copy the skin into another .psb. If null, destination is the same as source (target)")]
            public Object dstTargetOverride;
            [Tooltip("Transformations relative to Source's pivot.\nWill be applied inversely when transforming from source to destination")]
            public Transformation intoSrc;
            [Tooltip("Transformations relative to Destinations's pivot.\nWill be applied inversely when transforming from destination to source")]
            public Transformation intoDst;
            public int srcID, dstID;
            [Tooltip("True if you don't want to replace the destination's vertices and edges with source's.\nWill find nearest triangle and interpolate between its weights")]
            public bool onlyWeights = true;
            [Tooltip("Blur size (in world units) for sampling of the source's weights")]
            public float blurSize = 0.1f;
            [Range(0, 16)]
            [Tooltip("Blur radius (0 = 1x1, 1 = 3x3, 2 = 5x5, ...) for sampling of the source's weights")]
            public int blurCount = 0;

            [System.Serializable]
            public class Transformation
            {
                //public Matrix4x4 matrix = Matrix4x4.identity;
                public Vector2 scaler = Vector2.one;
                public float rotation = 0;
                public Vector2 offset = Vector2.zero;
            }
        }
    }
}
#endif