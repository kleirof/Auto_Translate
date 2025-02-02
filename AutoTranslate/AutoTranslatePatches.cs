using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Reflection;
using UnityEngine;
using ETGGUI;

namespace AutoTranslate
{
    public static class AutoTranslatePatches
    {
        public static void EmitCall<T>(this ILCursor iLCursor, string methodName, Type[] parameters = null, Type[] generics = null)
        {
            MethodInfo methodInfo = AccessTools.Method(typeof(T), methodName, parameters, generics);
            iLCursor.Emit(OpCodes.Call, methodInfo);
        }

        public static bool TheNthTime(this Func<bool> predict, int n = 1)
        {
            for (int i = 0; i < n; ++i)
            {
                if (!predict())
                    return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(dfLabel), nameof(dfLabel.Text), MethodType.Setter)]
        public class dfLabelTextPatchClass
        {
            [HarmonyPostfix]
            public static void dfLabelTextPostfix(dfLabel __instance)
            {
                dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                if (fontBase != null && __instance.Font != fontBase)
                {
                    __instance.Font = fontBase;
                    if (fontBase is dfFont dfFont)
                        __instance.Atlas = dfFont.Atlas;

                }
                AutoTranslateModule.instance.translateManager.AddTranslationRequest(__instance.text, __instance);
            }
        }

        [HarmonyPatch(typeof(dfLabel), nameof(dfLabel.OnLocalize))]
        public class dfLabelOnLocalizePatchClass
        {
            [HarmonyPostfix]
            public static void dfLabelOnLocalizePostfix(dfLabel __instance)
            {
                dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                if (fontBase != null && __instance.Font != fontBase)
                {
                    __instance.Font = fontBase;
                    if (fontBase is dfFont dfFont)
                        __instance.Atlas = dfFont.Atlas;

                }
                AutoTranslateModule.instance.translateManager.AddTranslationRequest(__instance.text, __instance);
            }
        }

        [HarmonyPatch(typeof(dfLabel), nameof(dfLabel.ModifyLocalizedText))]
        public class dfLabelModifyLocalizedTextPatchClass
        {
            [HarmonyPostfix]
            public static void dfLabelModifyLocalizedTextPostfix(dfLabel __instance)
            {
                dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                if (fontBase != null && __instance.Font != fontBase)
                {
                    __instance.Font = fontBase;
                    if (fontBase is dfFont dfFont)
                        __instance.Atlas = dfFont.Atlas;
                }
                AutoTranslateModule.instance.translateManager.AddTranslationRequest(__instance.text, __instance);
            }
        }

        [HarmonyPatch(typeof(dfButton), nameof(dfButton.Text), MethodType.Setter)]
        public class dfButtonTextPatchClass
        {
            [HarmonyPostfix]
            public static void dfButtonTextPostfix(dfButton __instance)
            {
                dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                if (fontBase != null && __instance.Font != fontBase)
                {
                    __instance.Font = fontBase;
                    if (fontBase is dfFont dfFont)
                        __instance.Atlas = dfFont.Atlas;
                }
                AutoTranslateModule.instance.translateManager.AddTranslationRequest(__instance.text, __instance);
            }
        }

        [HarmonyPatch(typeof(dfButton), nameof(dfButton.OnLocalize))]
        public class dfButtonOnLocalizePatchClass
        {
            [HarmonyPostfix]
            public static void dfButtonOnLocalizePostfix(dfButton __instance)
            {
                dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                if (fontBase != null && __instance.Font != fontBase)
                {
                    __instance.Font = fontBase;
                    if (fontBase is dfFont dfFont)
                        __instance.Atlas = dfFont.Atlas;

                }
                AutoTranslateModule.instance.translateManager.AddTranslationRequest(__instance.text, __instance);
            }
        }

        [HarmonyPatch(typeof(dfButton), nameof(dfButton.ModifyLocalizedText))]
        public class dfButtonModifyLocalizedTextPatchClass
        {
            [HarmonyPostfix]
            public static void dfButtonModifyLocalizedTextPostfix(dfButton __instance)
            {
                dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                if (fontBase != null && __instance.Font != fontBase)
                {
                    __instance.Font = fontBase;
                    if (fontBase is dfFont dfFont)
                        __instance.Atlas = dfFont.Atlas;
                }
                AutoTranslateModule.instance.translateManager.AddTranslationRequest(__instance.text, __instance);
            }
        }

        public class ShowTipPatchClass
        {
            private static SGUI.SLabel sLabel;

            public static void ShowTipPostfix(object __instance)
            {
                if (sLabel == null)
                {
                    Type type = AutoTranslateModule.instance.itemTipsModuleType;
                    FieldInfo fieldInfo = type.GetField("_infoLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                    sLabel = fieldInfo?.GetValue(__instance) as SGUI.SLabel;
                }

                string text = sLabel?.Text;
                if (text != null)
                    AutoTranslateModule.instance.translateManager.AddTranslationRequest(text, sLabel);
            }
        }

        public class LoadFontPatchClass
        {
            public static bool LoadFontPrefix(ref Font __result)
            {
                dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                if (fontBase != null && fontBase is dfDynamicFont dynamicFont)
                {
                    __result = dynamicFont.baseFont;
                    return false;
                }
                else if (fontBase != null && fontBase is dfFont dfFont)
                {
                    __result = FontConverter.GetFontFromdfFont(dfFont, 2);
                    return false;
                }

                dfFont font = FontManager.GetGameFont();
                if (font != null)
                {
                    __result = FontConverter.GetFontFromdfFont(font, 2);
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(tk2dTextMesh), nameof(tk2dTextMesh.text), MethodType.Setter)]
        public class tk2dTextMeshTextPatchClass
        {
            [HarmonyPostfix]
            public static void tk2dTextMeshTextPostfix(tk2dTextMesh __instance)
            {
                tk2dFontData tk2dFont = AutoTranslateModule.instance.fontManager.tk2dFont;
                if (tk2dFont != null && __instance.font != tk2dFont)
                {
                    FontManager.SetTextMeshFont(__instance, tk2dFont);
                }
                if (__instance.data != null && __instance.data.text != null)
                {
                    AutoTranslateModule.instance.translateManager.AddTranslationRequest(__instance.data.text, __instance);
                }
            }
        }

        [HarmonyPatch(typeof(tk2dTextMesh), nameof(tk2dTextMesh.font), MethodType.Setter)]
        public class tk2dTextMeshFontPatchClass
        {
            [HarmonyPrefix]
            public static bool tk2dTextMeshFontPrefix(tk2dTextMesh __instance)
            {
                tk2dFontData tk2dFont = AutoTranslateModule.instance.fontManager.tk2dFont;
                if (tk2dFont != null && __instance.font != tk2dFont)
                {
                    FontManager.SetTextMeshFont(__instance, tk2dFont);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(TextBoxManager), nameof(TextBoxManager.SetText))]
        public class SetTextPatchClass
        {
            [HarmonyPrefix]
            public static bool SetTextPrefix(ref bool instant)
            {
                instant = true;
                return true;
            }
        }

        public class DynamicFontRendererTokenizePatchClass
        {
            public static bool TokenizePrefix(dfDynamicFont.DynamicFontRenderer __instance, string text)
            {
                try
                {
                    if (__instance.tokens != null)
                    {
                        if (ReferenceEquals(__instance.tokens[0].Source, text))
                        {
                            return false;
                        }
                        __instance.tokens.ReleaseItems();
                        __instance.tokens.Release();
                    }
                    __instance.tokens = AutoTranslateModule.instance.fontManager?.Tokenize(text);
                    for (int i = 0; i < __instance.tokens.Count; i++)
                    {
                        __instance.calculateTokenRenderSize(__instance.tokens[i]);
                    }
                }
                finally
                {
                }
                return false;
            }
        }

        public class DynamicFontRendererCalculateLinebreaksPatchClass
        {
            [HarmonyILManipulator]
            public static void CalculateLinebreaksPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (((Func<bool>)(() =>
                    crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(0)
                    ))).TheNthTime(8))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)3);
                    crs.EmitCall<DynamicFontRendererCalculateLinebreaksPatchClass>(nameof(DynamicFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_1));
                    crs.Index++;
                }

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(0)
                    ))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)3);
                    crs.EmitCall<DynamicFontRendererCalculateLinebreaksPatchClass>(nameof(DynamicFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_1));
                }

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(2)
                    ))
                {
                    crs.EmitCall<DynamicFontRendererCalculateLinebreaksPatchClass>(nameof(DynamicFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_2));
                }
            }

            private static void CalculateLinebreaksPatchCall_1(ref int orig)
            {
                orig--;
            }

            private static int CalculateLinebreaksPatchCall_2(int orig)
            {
                return 2;
            }
        }

        public class BitmappedFontRendererTokenizePatchClass
        {
            public static bool TokenizePrefix(dfFont.BitmappedFontRenderer __instance, string text, ref dfList<dfMarkupToken> __result)
            {
                try
                {
                    if (__instance.tokens != null)
                    {
                        if (ReferenceEquals(__instance.tokens[0].Source, text))
                        {
                            __result = __instance.tokens;
                            return false;
                        }
                        __instance.tokens.ReleaseItems();
                        __instance.tokens.Release();
                    }
                    __instance.tokens = AutoTranslateModule.instance.fontManager?.Tokenize(text);
                    for (int i = 0; i < __instance.tokens.Count; i++)
                    {
                        __instance.calculateTokenRenderSize(__instance.tokens[i]);
                    }
                    __result = __instance.tokens;
                }
                finally
                {
                }
                return false;
            }
        }

        public class BitmappedFontRendererCalculateLinebreaksPatchClass
        {

            [HarmonyILManipulator]
            public static void CalculateLinebreaksPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(7)
                    ))
                {
                    crs.EmitCall<BitmappedFontRendererCalculateLinebreaksPatchClass>(nameof(BitmappedFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_1));
                }

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(2)
                    ))
                {
                    crs.EmitCall<BitmappedFontRendererCalculateLinebreaksPatchClass>(nameof(BitmappedFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_2));
                }
            }

            private static int CalculateLinebreaksPatchCall_1(int orig)
            {
                return 7;
            }

            private static int CalculateLinebreaksPatchCall_2(int orig)
            {
                return 2;
            }
        }

        [HarmonyPatch(typeof(UINotificationController), nameof(UINotificationController.DoNotificationInternal))]
        public class CheckLanguageFontsPatchClass
        {
            [HarmonyPostfix]
            public static void DoNotificationInternalPostfix(UINotificationController __instance)
            {
                __instance.CenterLabel.TextScale = 2f;
            }
        }

        [HarmonyPatch(typeof(tk2dTextMesh), nameof(tk2dTextMesh.CheckFontsForLanguage))]
        public class tk2dTextMeshCheckFontsForLanguagePatchClass
        {
            [HarmonyPrefix]
            public static bool CheckFontsForLanguagePrefix()
            {
                if (AutoTranslateModule.instance.fontManager.tk2dFont != null)
                    return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(dfLabel), nameof(dfLabel.CheckFontsForLanguage))]
        public class dfLabelCheckFontsForLanguagePatchClass
        {
            [HarmonyPrefix]
            public static bool CheckFontsForLanguagePrefix()
            {
                if (AutoTranslateModule.instance.fontManager.tk2dFont != null)
                    return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(dfButton), nameof(dfButton.CheckFontsForLanguage))]
        public class dfButtonCheckFontsForLanguagePatchClass
        {
            [HarmonyPrefix]
            public static bool CheckFontsForLanguagePrefix()
            {
                if (AutoTranslateModule.instance.fontManager.tk2dFont != null)
                    return false;
                return true;
            }
        }
    }
}