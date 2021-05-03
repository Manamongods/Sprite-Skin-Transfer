#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D;
using Unity.Collections;
using UnityEditor.U2D.PSD;
using UnityEditor.U2D.Animation;
using System.Reflection;
using UnityEditor.U2D.Sprites;
using UnityEngine.Rendering;

namespace SpriteSkinTransfer
{
    public static class SpriteSkinTransferUtility
    {
        private static NativeSlice<Vector3> fromv;
        private static NativeSlice<ushort> fromt;
        private static NativeSlice<BoneWeight> fromw;
        public static Dictionary<int, float> sums = new Dictionary<int, float>();
        public static List<Weight> sorted = new List<Weight>();


        public struct Weight
        {
            public int bone;
            public float weight;

            public Weight(int tri, float weight)
            {
                this.bone = tri;
                this.weight = weight;
            }
        }


        public static float Get(BoneWeight weight, int id, out int boneID)
        {
            switch (id)
            {
                case 0:
                    boneID = weight.boneIndex0;
                    return weight.weight0;
                case 1:
                    boneID = weight.boneIndex1;
                    return weight.weight1;
                case 2:
                    boneID = weight.boneIndex2;
                    return weight.weight2;
                default: //case 3:
                    boneID = weight.boneIndex3;
                    return weight.weight3;
            }
        }
        public static void Set(ref BoneWeight weight, int id, float value, int boneID)
        {
            switch (id)
            {
                case 0:
                    weight.weight0 = value;
                    weight.boneIndex0 = boneID;
                    break;
                case 1:
                    weight.weight1 = value;
                    weight.boneIndex1 = boneID;
                    break;
                case 2:
                    weight.weight2 = value;
                    weight.boneIndex2 = boneID;
                    break;
                default: //case 3:
                    weight.weight3 = value;
                    weight.boneIndex3 = boneID;
                    break;
            }
        }

        public static void PrepareWeightTransfer(Sprite fromSprite)
        {
            sums.Clear();
            sorted.Clear();

            fromv = fromSprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);
            fromt = fromSprite.GetIndices();
            fromw = fromSprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight);
        }

        public static void AddSample(Vector2 vert, float w)
        {
            int minID = GetClosestTriangle(vert, out Barycentric bestBary);
            if (minID != -1)
                AddSample(minID, bestBary, w);
        }
        public static void AddSample(int minID, Barycentric bestBary, float w)
        {
            sums.Clear();
            for (int boneChannel = 0; boneChannel < 4; boneChannel++)
            {
                for (int triCorner = 0; triCorner < 3; triCorner++)
                {
                    var weight = Get(fromw[fromt[minID + triCorner]], boneChannel, out int boneID) * bestBary[triCorner] * w;
                    if (weight > 0)
                    {
                        if (sums.ContainsKey(boneID))
                            sums[boneID] += weight;
                        else
                            sums.Add(boneID, weight);
                    }
                }
            }
        }

        public static void FillSorted(Vector2 vert, float blurSize, int blurCount, out int boneC)
        {
            if (blurCount == 0 || blurSize == 0)
                AddSample(vert, 1);
            else
            {
                int w = blurCount * 2 + 1;
                int totalC = w * w;
                float invC = 1f / totalC;
                float blurS = blurSize / blurCount;
                for (int x = -blurCount; x <= blurCount; x++)
                    for (int y = -blurCount; y <= blurCount; y++)
                        AddSample(vert + new Vector2(x * blurS, y * blurS), invC);
            }

            Sort();

            Normalize(out boneC);
        }
        public static void FillSorted(int minID, Barycentric bestBary, out int boneC)
        {
            AddSample(minID, bestBary, 1);

            Sort();

            Normalize(out boneC);
        }
        public static void Sort()
        {
            sorted.Clear();
            foreach (var kv in sums)
            {
                var tw = new Weight(kv.Key, kv.Value);

                bool success = false;
                for (int ii = 0; ii < sorted.Count; ii++)
                {
                    if (tw.weight > sorted[ii].weight && tw.weight > 0)
                    {
                        sorted.Insert(ii, tw);
                        success = true;
                        break;
                    }
                }
                if (!success)
                    sorted.Add(tw);
            }
        }
        public static void Normalize(out int boneC)
        {
            boneC = Mathf.Min(4, sorted.Count);
            float sum = 0;
            for (int ii = 0; ii < boneC; ii++)
                sum += sorted[ii].weight;
            float weightMult = 1 / Mathf.Max(0.00000000000001f, sum);

            for (int ii = 0; ii < boneC; ii++)
            {
                var sort = sorted[ii];
                sort.weight *= weightMult;
                sorted[ii] = sort;
            }
        }

        public static int GetClosestTriangle(Vector2 vert, out Barycentric bestBary)
        {
            float minDist = Mathf.Infinity;
            int minID = -1;
            bestBary = default;

            for (int ii = 0; ii < fromt.Length; ii += 3)
            {
                Vector2 a = fromv[fromt[ii + 0]];
                Vector2 b = fromv[fromt[ii + 1]];
                Vector2 c = fromv[fromt[ii + 2]];

                var unclampedBary = new Barycentric(a, b, c, vert);
                var div = Mathf.Max(1, unclampedBary.u + unclampedBary.v);
                var u = Mathf.Clamp01(unclampedBary.u / div);
                var v = Mathf.Clamp01(unclampedBary.v / div);
                var bary = new Barycentric(u, v, 1 - u - v);

                var clampedPoint = bary.Interpolate(a, b, c);

                var dist = (clampedPoint - vert).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    bestBary = bary;
                    minID = ii;
                }
            }

            return minID;
        }
    }
}
#endif