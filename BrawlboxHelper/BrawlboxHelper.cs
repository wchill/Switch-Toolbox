﻿using System;
using System.Collections.Generic;
using System.Linq;
using Syroot.NintenTools.NSW.Bfres;
using ResU = Syroot.NintenTools.Bfres;
using System.Windows;
using Syroot.Maths;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;

namespace BrawlboxHelper
{
    public class FSHUConverter
    {
        public static ResU.ShaderParamAnim Clr02Fshu(string FileName)
        {
            CLR0Node clr0 = NodeFactory.FromFile(null, FileName) as CLR0Node;

            ResU.ShaderParamAnim fshu = new ResU.ShaderParamAnim();
            fshu.FrameCount = clr0.FrameCount;
            fshu.Name = clr0.Name;
            fshu.Path = clr0.OriginalPath;
            fshu.UserData = new ResU.ResDict<Syroot.NintenTools.Bfres.UserData>();

            //Set flags
            if (clr0.Loop)
                fshu.Flags |= ResU.ShaderParamAnimFlags.Looping;

            //Set mat anims and then calculate data after
            foreach (var entry in clr0.Children)
                fshu.ShaderParamMatAnims.Add(Clr0Entry2ShaderMatAnim(clr0, (CLR0MaterialNode)entry));

            fshu.BakedSize = CalculateBakeSize(fshu);
            fshu.BindIndices = SetIndices(fshu);

            return fshu;
        }

        public static ResU.ShaderParamMatAnim Clr0Entry2ShaderMatAnim(CLR0Node clr0, CLR0MaterialNode clrMaterial)
        {
            ResU.ShaderParamMatAnim matAnim = new ResU.ShaderParamMatAnim();
            matAnim.Name = clrMaterial.Name;
            foreach (var entry in clrMaterial.Children)
            {
                ushort curveIndex = 0;
                ushort constantIndex = 0;

                CLR0MaterialEntryNode ParamEntry = (CLR0MaterialEntryNode)entry;

                //Add constants for RGBA if constant
                if (ParamEntry.Constant)
                {
                    //Red
                    matAnim.Constants.Add(new ResU.AnimConstant()
                    {
                        AnimDataOffset = 0,
                        Value = (float)ParamEntry.Colors[0].R / 255f,
                    });

                    //Green
                    matAnim.Constants.Add(new ResU.AnimConstant()
                    {
                        AnimDataOffset = 4,
                        Value = (float)ParamEntry.Colors[0].G / 255f,
                    });

                    //Blue
                    matAnim.Constants.Add(new ResU.AnimConstant()
                    {
                        AnimDataOffset = 8,
                        Value = (float)ParamEntry.Colors[0].B / 255f,
                    });

                    //Alpha
                    matAnim.Constants.Add(new ResU.AnimConstant()
                    {
                        AnimDataOffset = 12,
                        Value = (float)ParamEntry.Colors[0].A / 255f,
                    });
                }

                var RedCurve = GenerateCurve(0, clr0, ParamEntry);
                var GreenCurve = GenerateCurve(4, clr0, ParamEntry);
                var BlueCurve = GenerateCurve(8, clr0, ParamEntry);
                var AlphaCurve = GenerateCurve(12, clr0, ParamEntry);

                if (RedCurve != null)
                    matAnim.Curves.Add(RedCurve);
                if (GreenCurve != null)
                    matAnim.Curves.Add(GreenCurve);
                if (BlueCurve != null)
                    matAnim.Curves.Add(BlueCurve);
                if (AlphaCurve != null)
                    matAnim.Curves.Add(AlphaCurve);

                matAnim.ParamAnimInfos.Add(new ResU.ParamAnimInfo()
                {
                    Name = entry.Name,
                    BeginCurve = matAnim.Curves.Count > 0 ? curveIndex : ushort.MaxValue,
                    FloatCurveCount = (ushort)matAnim.Curves.Count,
                    SubBindIndex = ushort.MaxValue,
                    ConstantCount = (ushort)matAnim.Constants.Count,
                    BeginConstant = matAnim.Constants.Count > 0 ? constantIndex : ushort.MaxValue,
                });

                constantIndex += (ushort)matAnim.Constants.Count;
                curveIndex += (ushort)matAnim.Curves.Count;
            }

            return matAnim;
        }


        private static ResU.AnimCurve GenerateCurve(uint AnimOffset, CLR0Node anim, CLR0MaterialEntryNode entry)
        {
            ResU.AnimCurve curve = new ResU.AnimCurve();
            curve.AnimDataOffset = AnimOffset;
            curve.StartFrame = 0;
            curve.Offset = 0;
            curve.Scale = 1;
            curve.FrameType = ResU.AnimCurveFrameType.Single;
            curve.KeyType = ResU.AnimCurveKeyType.Single;
            curve.CurveType = ResU.AnimCurveType.Linear;

            List<float> Frames = new List<float>();
            List<float> Keys = new List<float>();

            for (int c = 0; c < entry.Colors.Count; c++)
            {
                Frames.Add(c);
                //Max of 4 values.  Cubic using 4, linear using 2, and step using 1
                float[] KeyValues = new float[4];

               switch (AnimOffset)
                {
                    case 0: //Red
                        Keys.Add((float)entry.Colors[c].R / 255f);
                        break;
                    case 4: //Green
                        Keys.Add((float)entry.Colors[c].G / 255f);
                        break;
                    case 8: //Blue
                        Keys.Add((float)entry.Colors[c].B / 255f);
                        break;
                    case 12: //Alpha
                        Keys.Add((float)entry.Colors[c].A / 255f);
                        break;
                    default:
                        throw new Exception("Invalid animation offset set!");
                }
            }

            //Max value in frames is our end frame
            curve.EndFrame = Frames.Max();
            curve.Frames = Frames.ToArray();

            //If a curve only has one frame we don't need to interpolate or add keys to a curve as it's constant
            if (curve.Frames.Length <= 1)
                return null;

            switch (curve.CurveType)
            {
                case ResU.AnimCurveType.Cubic:
                    curve.Keys = new float[Keys.Count, 4];
                    for (int frame = 0; frame < Keys.Count; frame++)
                    {
                        float Delta = 0;

                        if (frame < Keys.Count - 1)
                            Delta = Keys[frame + 1] - Keys[frame];

                        float value = Keys[frame];
                        float Slope = 0;
                        float Slope2 = 0;

                        curve.Keys[frame, 0] = value;
                        curve.Keys[frame, 1] = Slope;
                        curve.Keys[frame, 2] = Slope2;
                        curve.Keys[frame, 3] = Delta;
                    }
                    break;
                case ResU.AnimCurveType.StepInt:
                    //Step requires no interpolation
                    curve.Keys = new float[Keys.Count, 1];
                    for (int frame = 0; frame < Keys.Count; frame++)
                    {
                        curve.Keys[frame, 0] = 0;
                    }
                    break;
                case ResU.AnimCurveType.Linear:
                    curve.Keys = new float[Keys.Count, 2];
                    for (int frame = 0; frame < Keys.Count; frame++)
                    {
                        //Delta for second value used in linear curves
                        float time = curve.Frames[frame];
                        float Delta = 0;

                        if (frame < Keys.Count - 1)
                            Delta = Keys[frame + 1] - Keys[frame];

                        curve.Keys[frame, 0] = Keys[frame];
                        curve.Keys[frame, 1] = Delta;
                    }
                    break;
            }

            return curve;
        }

        private static ushort[] SetIndices(ResU.ShaderParamAnim fshu)
        {
            List<ushort> indces = new List<ushort>();
            foreach (var matAnim in fshu.ShaderParamMatAnims)
                indces.Add(65535);

            return indces.ToArray();
        }
        private static uint CalculateBakeSize(ResU.ShaderParamAnim fshu)
        {
            return 0;
        }
    }

    public class FSKAConverter
    {
        static float Deg2Rad = (float)(Math.PI / 180f);

        public static void Fska2Chr0(SkeletalAnim fska, string FileName)
        {
      
            CHR0Node chr0 = new CHR0Node();
            chr0.FrameCount = fska.FrameCount;
            chr0.Name = fska.Name;
            chr0.OriginalPath = fska.Path;
            chr0.UserEntries = new UserDataCollection();
            chr0.Loop = fska.Loop;
            chr0.CreateEntry();

            foreach (var entry in fska.BoneAnims)
                chr0.Children.Add(BoneAnim2Chr0Entry(entry, chr0));

            chr0.Export(FileName);
        }

        public class FSKAKeyNode
        {
            public float Value;
            public float Slope;
            public float Slope2;
            public float Delta;
            public float Frame;
        }

        public static CHR0EntryNode BoneAnim2Chr0Entry(BoneAnim boneAnim, CHR0Node chr0)
        {
            CHR0EntryNode chr0Entry = chr0.CreateEntry(boneAnim.Name);
            chr0Entry.UseModelRotate = false;
            chr0Entry.UseModelScale = false;
            chr0Entry.UseModelTranslate = false;


            //Float for time/frame
            Dictionary<float, FSKAKeyNode> TranslateX = new Dictionary<float, FSKAKeyNode>();
            Dictionary<float, FSKAKeyNode> TranslateY = new Dictionary<float, FSKAKeyNode>();
            Dictionary<float, FSKAKeyNode> TranslateZ = new Dictionary<float, FSKAKeyNode>();
            Dictionary<float, FSKAKeyNode> RotateX = new Dictionary<float, FSKAKeyNode>();
            Dictionary<float, FSKAKeyNode> RotateY = new Dictionary<float, FSKAKeyNode>();
            Dictionary<float, FSKAKeyNode> RotateZ = new Dictionary<float, FSKAKeyNode>();
            Dictionary<float, FSKAKeyNode> ScaleX = new Dictionary<float, FSKAKeyNode>();
            Dictionary<float, FSKAKeyNode> ScaleY = new Dictionary<float, FSKAKeyNode>();
            Dictionary<float, FSKAKeyNode> ScaleZ = new Dictionary<float, FSKAKeyNode>();

            foreach (var curve in boneAnim.Curves)
            {
                for (int frame = 0; frame < curve.Frames.Length; frame++)
                {
                    float time = curve.Frames[frame];
                    float value = 0;
                    float slope = 0;
                    float slope2 = 0;
                    float delta = 0;
                    if (curve.CurveType == AnimCurveType.Cubic)
                    {
                        value = curve.Keys[frame, 0];
                        slope = curve.Keys[frame, 1];
                        slope2 = curve.Keys[frame, 2];
                        delta = curve.Keys[frame, 3];
                    }
                    if (curve.CurveType == AnimCurveType.Linear)
                    {
                        value = curve.Keys[frame, 0];
                        delta = curve.Keys[frame, 1];
                    }
                    if (curve.CurveType == AnimCurveType.Linear)
                    {
                        value = curve.Keys[frame, 0];
                    }

                    switch (curve.AnimDataOffset)
                    {
                        case 0x10:
                            TranslateX.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                        case 0x14:
                            TranslateY.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                        case 0x18:
                            TranslateZ.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                        case 0x20:
                            RotateX.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                        case 0x24:
                            RotateY.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                        case 0x28:
                            RotateZ.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                        case 0x04:
                            ScaleX.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                        case 0x08:
                            ScaleY.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                        case 0x0C:
                            ScaleZ.Add(time, new FSKAKeyNode()
                            {
                                Value = value,
                                Slope = slope,
                                Slope2 = slope2,
                                Delta = delta,
                                Frame = time,
                            });
                            break;
                    }
                }
            }

            for (int frame = 0; frame < chr0.FrameCount; frame++)
            {
                if (TranslateX.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasTx = true;
                    keyFrame.Translation._x = TranslateX[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }
                if (TranslateY.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasTy = true;
                    keyFrame.Translation._y = TranslateY[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }
                if (TranslateZ.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasTz = true;
                    keyFrame.Translation._z = TranslateZ[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }

                if (RotateX.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasRx = true;
                    keyFrame.Rotation._x = RotateX[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }
                if (RotateY.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasRy = true;
                    keyFrame.Rotation._y = RotateY[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }
                if (RotateZ.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasSz = true;
                    keyFrame.Rotation._z = RotateZ[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }

                if (ScaleX.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasSx = true;
                    keyFrame.Scale._x = ScaleX[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }
                if (ScaleY.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasSy = true;
                    keyFrame.Scale._y = ScaleY[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }
                if (ScaleZ.ContainsKey(frame))
                {
                    CHRAnimationFrame keyFrame = new CHRAnimationFrame();
                    keyFrame.hasSz = true;
                    keyFrame.Scale._z = ScaleZ[frame].Value;
                    chr0Entry.SetKeyframe(frame, keyFrame);
                }
            }


            return chr0Entry;
        }

        public static SkeletalAnim Chr02Fska(string FileName)
        {
            CHR0Node chr0 = CHR0Node.FromFile(FileName);

            SkeletalAnim fska = new SkeletalAnim();
            fska.FrameCount = chr0.FrameCount;
            fska.Name = chr0.Name;
            fska.Path = chr0.OriginalPath;
            fska.UserDatas = new List<Syroot.NintenTools.NSW.Bfres.UserData>();
            fska.UserDataDict = new ResDict();

            //Set flags
            if (chr0.Loop)
                fska.FlagsAnimSettings |= SkeletalAnimFlags.Looping;
            fska.FlagsRotate = SkeletalAnimFlagsRotate.EulerXYZ;
            fska.FlagsScale = SkeletalAnimFlagsScale.Maya;

            //Set bone anims and then calculate data after
            foreach (var entry in chr0.Children)
                fska.BoneAnims.Add(Chr0Entry2BoneAnim((CHR0EntryNode)entry));

            fska.BakedSize = CalculateBakeSize(fska);
            fska.BindIndices = SetIndices(fska);

            return fska;
        }

        private static BoneAnim Chr0Entry2BoneAnim(CHR0EntryNode entry)
        {
            BoneAnim boneAnim = new BoneAnim();
            boneAnim.Name = entry.Name;

            if (entry.UseModelTranslate)
                boneAnim.FlagsBase |= BoneAnimFlagsBase.Translate;
            if (entry.UseModelRotate)
                boneAnim.FlagsBase |= BoneAnimFlagsBase.Rotate;
            if (entry.UseModelScale)
                boneAnim.FlagsBase |= BoneAnimFlagsBase.Scale;

            var FirstFrame = entry.GetAnimFrame(0);

            float xRadian = FirstFrame.Rotation._x * Deg2Rad;
            float yRadian = FirstFrame.Rotation._y * Deg2Rad;
            float zRadian = FirstFrame.Rotation._z * Deg2Rad;

            var baseData = new BoneAnimData();
            baseData.Translate = new Vector3F(FirstFrame.Translation._x, FirstFrame.Translation._y, FirstFrame.Translation._z);
            baseData.Rotate = new Vector4F(xRadian, yRadian, zRadian, 1);
            baseData.Scale = new Vector3F(FirstFrame.Scale._x, FirstFrame.Scale._y, FirstFrame.Scale._z);
            baseData.Flags = 0;
            boneAnim.BaseData = baseData;

            boneAnim.FlagsBase |= BoneAnimFlagsBase.Translate;
            boneAnim.FlagsBase |= BoneAnimFlagsBase.Scale;
            boneAnim.FlagsBase |= BoneAnimFlagsBase.Rotate;

            if (baseData.Rotate == new Vector4F(0, 0, 0, 1))
                boneAnim.FlagsTransform |= BoneAnimFlagsTransform.RotateZero;
            if (baseData.Translate == new Vector3F(0, 0, 0))
                boneAnim.FlagsTransform |= BoneAnimFlagsTransform.TranslateZero;
            if (baseData.Scale == new Vector3F(1, 1, 1))
                boneAnim.FlagsTransform |= BoneAnimFlagsTransform.ScaleOne;
            if (IsUniform(baseData.Scale))
                boneAnim.FlagsTransform |= BoneAnimFlagsTransform.ScaleUniform;
            if (!IsRoot(boneAnim))
                boneAnim.FlagsTransform |= BoneAnimFlagsTransform.SegmentScaleCompensate;

            boneAnim.BeginTranslate = 6;
            boneAnim.BeginRotate = 3;

            if (FirstFrame.HasKeys)
            {
                var AnimFrame = entry.GetAnimFrame(0);
                if (AnimFrame.hasTx)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.TranslateX;
                    var curve = GenerateCurve(0x10, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
                if (AnimFrame.hasTy)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.TranslateY;
                    var curve = GenerateCurve(0x14, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
                if (AnimFrame.hasTz)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.TranslateZ;
                    var curve = GenerateCurve(0x18, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
                if (AnimFrame.hasRx)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.RotateX;
                    var curve = GenerateCurve(0x20, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
                if (AnimFrame.hasRy)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.RotateY;
                    var curve = GenerateCurve(0x24, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
                if (AnimFrame.hasRz)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.RotateZ;
                    var curve = GenerateCurve(0x28, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
                if (AnimFrame.hasSx)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.ScaleX;
                    var curve = GenerateCurve(0x4, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
                if (AnimFrame.hasSy)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.ScaleY;
                    var curve = GenerateCurve(0x8, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
                if (AnimFrame.hasSz)
                {
                    boneAnim.FlagsCurve |= BoneAnimFlagsCurve.ScaleZ;
                    var curve = GenerateCurve(0xC, entry);
                    if (curve != null)
                        boneAnim.Curves.Add(curve);
                }
            }

            return boneAnim;
        }

        private static bool IsRoot(BoneAnim boneAnim)
        {
            if (boneAnim.Name.Contains("root") || boneAnim.Name.Contains("center"))
                return true;

            return false;
        }

        private static bool IsUniform(Vector3F value)
        {
            return value.X == value.Y && value.X == value.Z;
        }

        private static bool HasKeyFrames(CHR0EntryNode entry)
        {
            for (int frame = 0; frame < entry.FrameCount; frame++)
            {
                var AnimFrame = entry.GetAnimFrame(frame);
                if (frame > 0)
                {
                    if (AnimFrame.hasTx) return true;
                    if (AnimFrame.hasTy) return true;
                    if (AnimFrame.hasTz) return true;
                    if (AnimFrame.hasRx) return true;
                    if (AnimFrame.hasRy) return true;
                    if (AnimFrame.hasRz) return true;
                    if (AnimFrame.hasSx) return true;
                    if (AnimFrame.hasSy) return true;
                    if (AnimFrame.hasSz) return true;
                }
            }
            return false;
        }

        private static AnimCurve GenerateCurve(uint AnimOffset, CHR0EntryNode entry)
        {
            AnimCurve curve = new AnimCurve();
            curve.AnimDataOffset = AnimOffset;
            curve.StartFrame = 0;
            curve.Offset = 0;
            curve.Scale = 1;
            curve.FrameType = AnimCurveFrameType.Single;
            curve.KeyType = AnimCurveKeyType.Single;
            curve.CurveType = AnimCurveType.Cubic;

            List<float> Frames = new List<float>();
            List<float[]> Keys = new List<float[]>();

            for (int frame = 0; frame < entry.FrameCount; frame++)
            {
                //Max of 4 values.  Cubic using 4, linear using 2, and step using 1
                float[] KeyValues = new float[4];

                //Set the main values to the curve based on offset for encoding later
                var AnimFrame = entry.GetAnimFrame(frame);
                if (AnimFrame.hasTx && AnimOffset == 0x10)
                {
                    Frames.Add(frame);
                    KeyValues[0] = AnimFrame.Translation._x;
                    Keys.Add(KeyValues);
                }
                if (AnimFrame.hasTy && AnimOffset == 0x14)
                {
                    Frames.Add(frame);
                    KeyValues[0] = AnimFrame.Translation._y;
                    Keys.Add(KeyValues);
                }
                if (AnimFrame.hasTz && AnimOffset == 0x18)
                {
                    Frames.Add(frame);
                    KeyValues[0] = AnimFrame.Translation._z;
                    Keys.Add(KeyValues);
                }
                if (AnimFrame.hasRx && AnimOffset == 0x20)
                {
                    Frames.Add(frame);
                    float xRadian = AnimFrame.Rotation._x * Deg2Rad;
                    KeyValues[0] = xRadian;
                    Keys.Add(KeyValues);
                }
                if (AnimFrame.hasRy && AnimOffset == 0x24)
                {
                    Frames.Add(frame);
                    float yRadian = AnimFrame.Rotation._y * Deg2Rad;
                    KeyValues[0] = yRadian;
                    Keys.Add(KeyValues);
                }
                if (AnimFrame.hasRz && AnimOffset == 0x28)
                {
                    Frames.Add(frame);
                    float zRadian = AnimFrame.Rotation._z * Deg2Rad;
                    KeyValues[0] = zRadian;
                    Keys.Add(KeyValues);
                }
                if (AnimFrame.hasSx && AnimOffset == 0x04)
                {
                    Frames.Add(frame);
                    KeyValues[0] = AnimFrame.Scale._x;
                    Keys.Add(KeyValues);
                }
                if (AnimFrame.hasSy && AnimOffset == 0x08)
                {
                    Frames.Add(frame);
                    KeyValues[0] = AnimFrame.Scale._y;
                    Keys.Add(KeyValues);
                }
                if (AnimFrame.hasSz && AnimOffset == 0x0C)
                {
                    Frames.Add(frame);
                    KeyValues[0] = AnimFrame.Scale._z;
                    Keys.Add(KeyValues);
                }
            }

            //Max value in frames is our end frame
            curve.EndFrame = Frames.Max();
            curve.Frames = Frames.ToArray();

            //If a curve only has one frame we don't need to interpolate or add keys to a curve as it's constant
            if (curve.Frames.Length <= 1)
                return null;

            switch (curve.CurveType)
            {
                case AnimCurveType.Cubic:
                    curve.Keys = new float[Keys.Count, 4];
                    for (int frame = 0; frame < Keys.Count; frame++)
                    {
                        float Delta = 0;

                        if (frame < Keys.Count - 1)
                            Delta = Keys[frame + 1][0] - Keys[frame][0];

                        float value = Keys[frame][0];
                        float Slope = Keys[frame][1];
                        float Slope2 = Keys[frame][2];

                        curve.Keys[frame, 0] = value;
                        curve.Keys[frame, 1] = Slope;
                        curve.Keys[frame, 2] = Slope2;
                        curve.Keys[frame, 3] = Delta;
                    }
                    break;
                case AnimCurveType.StepInt:
                    //Step requires no interpolation
                    curve.Keys = new float[Keys.Count, 1];
                    for (int frame = 0; frame < Keys.Count; frame++)
                    {
                        curve.Keys[frame, 0] = Keys[frame][0];
                    }
                    break;
                case AnimCurveType.Linear:
                    curve.Keys = new float[Keys.Count, 2];
                    for (int frame = 0; frame < Keys.Count; frame++)
                    {
                        //Delta for second value used in linear curves
                        float time = curve.Frames[frame];
                        float Delta = 0;

                        if (frame < Keys.Count - 1)
                            Delta = Keys[frame + 1][0] - Keys[frame][0];

                        curve.Keys[frame, 0] = Keys[frame][0];
                        curve.Keys[frame, 1] = Delta;
                    }
                    break;
            }

            return curve;
        }

        private static ushort[] SetIndices(SkeletalAnim fska)
        {
            List<ushort> indces = new List<ushort>();
            foreach (var boneAnim in fska.BoneAnims)
                indces.Add(65535);

            return indces.ToArray();
        }
        private static uint CalculateBakeSize(SkeletalAnim fska)
        {
            return 0;
        }
    }
}
