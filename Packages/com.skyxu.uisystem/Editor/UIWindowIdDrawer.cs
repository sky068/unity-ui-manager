using UnityEditor;
using UnityEngine;

namespace Game.UISystem.Editor
{
    [CustomPropertyDrawer(typeof(UIWindowId))]
    internal sealed class UIWindowIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var value = property.FindPropertyRelative("value");
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PropertyField(position, value, label);
            EditorGUI.EndProperty();
        }
    }
}
