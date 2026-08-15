using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AudioManager.SoundEntry))]
public class SoundEntryDrawer : PropertyDrawer
{
    const float LineH = 18f;
    const float Gap   = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => LineH * 2 + Gap * 3;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var nameProp     = property.FindPropertyRelative("name");
        var clipProp     = property.FindPropertyRelative("clip");
        var loopProp     = property.FindPropertyRelative("loop");
        var categoryProp = property.FindPropertyRelative("category");

        float y    = position.y + Gap;
        float w    = position.width;
        float x    = position.x;
        float half = w * 0.4f;

        // 첫 번째 줄: Name | Clip
        Rect nameRect = new Rect(x,              y, half,          LineH);
        Rect clipRect = new Rect(x + half + Gap, y, w - half - Gap, LineH);
        nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.stringValue);
        EditorGUI.ObjectField(clipRect, clipProp, GUIContent.none);

        // 두 번째 줄: Loop | Category
        y += LineH + Gap;
        float labelW    = 40f;
        float checkW    = 16f;
        float catLabelW = 60f;
        float catW      = w - labelW - checkW - catLabelW - Gap * 3;

        Rect loopLabelRect = new Rect(x,                            y, labelW,   LineH);
        Rect loopCheckRect = new Rect(x + labelW + Gap,             y, checkW,   LineH);
        Rect catLabelRect  = new Rect(x + labelW + checkW + Gap*2,  y, catLabelW, LineH);
        Rect catRect       = new Rect(x + labelW + checkW + catLabelW + Gap*3, y, catW, LineH);

        EditorGUI.LabelField(loopLabelRect, "Loop");
        loopProp.boolValue = EditorGUI.Toggle(loopCheckRect, loopProp.boolValue);
        EditorGUI.LabelField(catLabelRect, "Category");
        EditorGUI.PropertyField(catRect, categoryProp, GUIContent.none);
    }
}
