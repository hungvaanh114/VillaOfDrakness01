using UnityEditor;
using UnityEngine;

namespace UnityEditor.PostProcessing
{
    [CustomPropertyDrawer(typeof(UnityEngine.PostProcessing.MinAttribute))]
    public sealed class MinDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attribute = (UnityEngine.PostProcessing.MinAttribute)base.attribute;

            if (property.propertyType == SerializedPropertyType.Float)
            {
                float value = EditorGUI.FloatField(position, label, property.floatValue);
                property.floatValue = Mathf.Max(value, attribute.min);
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                int value = EditorGUI.IntField(position, label, property.intValue);
                property.intValue = Mathf.Max(value, (int)attribute.min);
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use Min with float or int.");
            }
        }
    }
}
