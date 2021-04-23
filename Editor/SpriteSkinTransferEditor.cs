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
    [CustomPropertyDrawer(typeof(SpriteSkinTransfer.Transfer))]
    public class SpriteSkinTransferTransferEditor : PropertyDrawer
    {
        private const float SPACE = 0.5f;
        private const BindingFlags gf = BindingFlags.GetField | BindingFlags.Instance;
        private const BindingFlags pgf = gf | BindingFlags.Public;
        private const BindingFlags npgf = gf | BindingFlags.NonPublic;
        private const BindingFlags gp = BindingFlags.GetProperty | BindingFlags.Instance;
        private const BindingFlags pgp = gp | BindingFlags.Public;
        private const BindingFlags npgp = gp | BindingFlags.NonPublic;



        private static object GetField(object o, string name, BindingFlags bf)
        {
            return o.GetType().GetField(name, bf).GetValue(o);
        }
        private static object GetProperty(object o, string name, BindingFlags bf)
        {
            return o.GetType().GetProperty(name, bf).GetValue(o);
        }
        private static void SetField(object o, string name, BindingFlags bf, object val)
        {
            o.GetType().GetField(name, bf).SetValue(o, val);
        }

        private static object[] GetMetas(AssetImporter importer, out string[] names)
        {
            //PSDImporter only, don't know where this sort of data is stored for rigged non-psbs

            try
            {
                var dataArray = GetField(importer, "m_RigSpriteImportData", gf | BindingFlags.NonPublic);
                var l = (int)GetProperty(dataArray, "Count", pgp);
                object[] metas = new object[l];
                for (int i = 0; i < l; i++)
                    metas[i] = dataArray.GetType().GetProperty("Item").GetValue(dataArray, new object[] { i });
                names = new string[metas.Length];
                for (int i = 0; i < metas.Length; i++)
                    names[i] = (string)GetProperty(metas[i], "name", pgf);
                return metas;
            }
            catch
            {
                names = new string[0];
                return new object[0];
            }
        }



        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lines = 6;
            int spaces = 7;
            var onlyWeights = property.FindPropertyRelative("onlyWeights");
            if (onlyWeights.boolValue)
            {
                lines += 2;
                spaces++;
            }
            var intoDst = property.FindPropertyRelative("intoDst");
            var intoSrc = property.FindPropertyRelative("intoSrc");
            float into = EditorGUI.GetPropertyHeight(intoDst) + EditorGUI.GetPropertyHeight(intoSrc);
            return EditorGUIUtility.singleLineHeight * (lines + spaces * SPACE) + into;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var target = property.FindPropertyRelative("target");
            var dstTargetOverride = property.FindPropertyRelative("dstTargetOverride");
            var onlyWeights = property.FindPropertyRelative("onlyWeights");
            var srcID = property.FindPropertyRelative("srcID");
            var dstID = property.FindPropertyRelative("dstID");
            var intoDst = property.FindPropertyRelative("intoDst");
            var intoSrc = property.FindPropertyRelative("intoSrc");
            var blurSize = property.FindPropertyRelative("blurSize");
            var blurCount = property.FindPropertyRelative("blurCount");


            EditorGUI.BeginProperty(position, label, property);

            var r = position;
            r.height = EditorGUIUtility.singleLineHeight;
            void Line() => r.y += r.height;
            void Space() => r.y += SPACE * r.height;
            void SpaceLine()
            {
                Space();
                Line();
            }


            #region Finds Metas and Names
            var targetAsset = target.objectReferenceValue;
            AssetImporter importer = null;
            object[] metas = new string[0];
            string[] names = new string[0];
            if (targetAsset)
            {
                importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(targetAsset));
                metas = GetMetas(importer, out names);
            }

            var dstTargetAsset = dstTargetOverride.objectReferenceValue;
            AssetImporter dstImporter;
            object[] dstMetas;
            string[] dstNames;
            if (dstTargetAsset)
            {
                dstImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(dstTargetAsset));
                dstMetas = GetMetas(dstImporter, out dstNames);
            }
            else
            {
                dstMetas = metas;
                dstNames = names;
                dstImporter = importer;
                dstTargetAsset = targetAsset;
            }
            #endregion

            static Sprite GetSprite(Object asset, string name)
            {
                var children = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset));
                for (int childID = 0; childID < children.Length; childID++)
                {
                    var sprite = children[childID] as Sprite;
                    if (sprite != null && sprite.name == name)
                        return sprite;
                }

                return null;
            }


            Space();

            if (metas.Length > 0 && dstMetas.Length > 0)
            {
                if (GUI.Button(r, "Transfer"))
                {
                    Undo.RegisterImporterUndo(AssetDatabase.GetAssetPath(dstTargetAsset), "Transfer Skin");

                    var srcMeta = metas[srcID.intValue];
                    var srcVerts = (List<Vertex2DMetaData>)GetField(srcMeta, "vertices", pgf);
                    var dstMeta = dstMetas[dstID.intValue];
                    var dstVerts = (List<Vertex2DMetaData>)GetField(dstMeta, "vertices", pgf);

                    Vector2 srcPivot = (Vector2)GetProperty(srcMeta, "pivot", pgp | BindingFlags.FlattenHierarchy);
                    Vector2 dstPivot = (Vector2)GetProperty(dstMeta, "pivot", pgp | BindingFlags.FlattenHierarchy);
                    Rect srcRect = (Rect)GetProperty(srcMeta, "rect", pgp | BindingFlags.FlattenHierarchy);
                    Rect dstRect = (Rect)GetProperty(dstMeta, "rect", pgp | BindingFlags.FlattenHierarchy);
                    Vector2Int srcUVTr = (Vector2Int)GetField(srcMeta, "uvTransform", pgf);
                    Vector2Int dstUVTr = (Vector2Int)GetField(dstMeta, "uvTransform", pgf);

                    Vector2 offset = -(dstRect.min - srcRect.min) + (dstUVTr - srcUVTr);

                    #region Transforming
                    Matrix4x4 intoDstMat = Matrix4x4.identity; // intoDst.FindPropertyRelative("matrix").;
                    Vector2 intoDstSc = intoDst.FindPropertyRelative("scaler").vector2Value;
                    float intoDstRot = intoDst.FindPropertyRelative("rotation").floatValue;
                    Vector2 intoDstOff = intoDst.FindPropertyRelative("offset").vector2Value;

                    Matrix4x4 intoSrcMat = Matrix4x4.identity;
                    Vector2 intoSrcSc = intoSrc.FindPropertyRelative("scaler").vector2Value;
                    float intoSrcRot = intoSrc.FindPropertyRelative("rotation").floatValue;
                    Vector2 intoSrcOff = intoSrc.FindPropertyRelative("offset").vector2Value;

                    var absSrcPivot = Vector2.Scale(srcRect.size, srcPivot);
                    var absDstPivot = Vector2.Scale(dstRect.size, dstPivot);

                    Vector2 Rotate(Vector2 v, float ang)
                    {
                        var sin = Mathf.Sin(ang * Mathf.Deg2Rad);
                        var cos = Mathf.Cos(ang * Mathf.Deg2Rad);
                        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
                    }
                    Vector2 Into(Vector2 v, Matrix4x4 m, Vector2 scaler, float rot, Vector2 offset, Vector2 pivot)
                    {
                        return Rotate(((Vector2)m.MultiplyPoint(v) - pivot) * scaler, rot) + pivot + offset;
                    }
                    Vector2 OutOf(Vector2 v, Matrix4x4 m, Vector2 scaler, float rot, Vector2 offset, Vector2 pivot)
                    {
                        return m.inverse.MultiplyPoint(Rotate((v - offset - pivot), -rot) / scaler + pivot);
                    }

                    Vector2 TransformFromSrcToDst(Vector2 v)
                    {
                        return Into(OutOf(v, intoSrcMat, intoSrcSc, intoSrcRot, intoSrcOff, absSrcPivot) + offset, intoDstMat, intoDstSc, intoDstRot, intoDstOff, absDstPivot);
                    }
                    Vector2 TransformFromDstToSrc(Vector2 v)
                    {
                        return Into(OutOf(v, intoDstMat, intoDstSc, intoDstRot, intoDstOff, absDstPivot) - offset, intoSrcMat, intoSrcSc, intoSrcRot, intoSrcOff, absSrcPivot);
                    }
                    #endregion

                    if (onlyWeights.boolValue)
                    {
                        var fromSprite = GetSprite(targetAsset, names[srcID.intValue]); //var toSprite = GetSprite(dstTargetPSD, names[dstID.intValue]);

                        #region Definition Scale
                        //In case the texture has been downsized because of Max Size
                        float definitionScale = 1;
                        var dataProviderFactories = new SpriteDataProviderFactories();
                        dataProviderFactories.Init();
                        ISpriteEditorDataProvider ai = dataProviderFactories.GetSpriteEditorDataProviderFromObject(importer);
                        if (ai != null)
                        {
                            static float CalculateDefinitionScale(Texture2D texture, ITextureDataProvider dataProvider)
                            {
                                float definitionScale = 1;
                                if (texture != null && dataProvider != null)
                                {
                                    dataProvider.GetTextureActualWidthAndHeight(out int actualWidth, out int actualHeight);
                                    float definitionScaleW = texture.width / (float)actualWidth;
                                    float definitionScaleH = texture.height / (float)actualHeight;
                                    definitionScale = Mathf.Min(definitionScaleW, definitionScaleH);
                                }
                                return definitionScale;
                            }
                            definitionScale = CalculateDefinitionScale(fromSprite.texture, ai.GetDataProvider<ITextureDataProvider>());
                        }
                        #endregion

                        SpriteSkinTransferUtility.PrepareWeightTransfer(fromSprite);

                        for (int i = 0; i < dstVerts.Count; i++)
                        {
                            var vert = (TransformFromDstToSrc(dstVerts[i].position) - absSrcPivot) * definitionScale / fromSprite.pixelsPerUnit;

                            SpriteSkinTransferUtility.FillSorted(vert, blurSize.floatValue, blurCount.intValue, out int boneC);

                            var toVert = dstVerts[i];

                            //Clears all 4 weights
                            for (int ii = 0; ii < 4; ii++)
                                SpriteSkinTransferUtility.Set(ref toVert.boneWeight, ii, 0f, 0); //Debug.Log(Get(towii.boneWeight, ii, out int boneID) + " " + boneID);

                            //Sets any weights
                            for (int ii = 0; ii < boneC; ii++)
                            {
                                var sort = SpriteSkinTransferUtility.sorted[ii];
                                SpriteSkinTransferUtility.Set(ref toVert.boneWeight, ii, sort.weight, sort.bone);
                            }

                            dstVerts[i] = toVert;
                        }
                    }
                    else
                    {
                        dstVerts.Clear();
                        dstVerts.AddRange(srcVerts);
                        for (int i = 0; i < dstVerts.Count; i++)
                        {
                            var v = dstVerts[i];
                            v.position = TransformFromSrcToDst(v.position);
                            dstVerts[i] = v;
                        }

                        SetField(dstMeta, "indices", pgf, ((int[])GetField(srcMeta, "indices", pgf)).Clone());
                        SetField(dstMeta, "edges", pgf, ((Vector2Int[])GetField(srcMeta, "edges", pgf)).Clone());
                    }


                    //Copies over bones
                    var sbf = dstMeta.GetType().GetField("spriteBone", gf | BindingFlags.Public);
                    sbf.SetValue(dstMeta, new List<SpriteBone>(sbf.GetValue(srcMeta) as List<SpriteBone>));

                    var cd = importer.GetType().GetField("m_CharacterData", npgf);
                    var character = (CharacterData)cd.GetValue(importer);
                    int GetPart(object meta)
                    {
                        string id = ((GUID)meta.GetType().GetProperty("spriteID").GetValue(meta)).ToString();
                        for (int i = 0; i < character.parts.Length; i++)
                            if (character.parts[i].spriteId == id)
                                return i;
                        return -1;
                    }
                    character.parts[GetPart(dstMeta)].bones = character.parts[GetPart(srcMeta)].bones;
                    cd.SetValue(importer, character);


                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }

            SpaceLine();


            EditorGUI.PropertyField(r, target, new GUIContent("Target"));
            SpaceLine();
            EditorGUI.PropertyField(r, dstTargetOverride, new GUIContent("Destination Target Override"));
            SpaceLine();

            srcID.intValue = EditorGUI.Popup(r, "Source", srcID.intValue, names);
            Line();
            EditorGUI.PropertyField(r, intoSrc, new GUIContent("Into Source Transformation"), true);
            r.y += EditorGUI.GetPropertyHeight(intoSrc);
            Space();

            dstID.intValue = EditorGUI.Popup(r, "Destination", dstID.intValue, dstNames);
            Line();
            EditorGUI.PropertyField(r, intoDst, new GUIContent("Into Destination Transformation"), true);
            r.y += EditorGUI.GetPropertyHeight(intoDst);
            Space();

            EditorGUI.PropertyField(r, onlyWeights, new GUIContent("Only Transfer Weights"));
            SpaceLine();

            if (onlyWeights.boolValue)
            {
                EditorGUI.PropertyField(r, blurSize, new GUIContent("Blur Size"));
                Line();
                EditorGUI.PropertyField(r, blurCount, new GUIContent("Blur Count"));
                SpaceLine();
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif