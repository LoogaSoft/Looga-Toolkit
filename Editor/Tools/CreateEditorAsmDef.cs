using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace LoogaSoft.Tools.Editor
{
    public class CreateEditorAsmDef
    {
        [MenuItem("Assets/Create/Scripting/Editor Assembly Definition", priority = 24)]
        public static void CreateAsset()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                ScriptableObject.CreateInstance<DoCreateEditorAssemblyDefinition>(),
                "NewEditorAsmDef.asmdef",
                EditorGUIUtility.IconContent("AssemblyDefinitionAsset Icon").image as Texture2D,
                null
            );
        }
    }

    public class DoCreateEditorAssemblyDefinition : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string fileName = Path.GetFileNameWithoutExtension(pathName);

            string jsonContent = $@"{{
            ""name"": ""{fileName}"",
            ""references"": [],
            ""includePlatforms"": [""Editor""],
            ""excludePlatforms"": [],
            ""allowUnsafeCode"": false,
            ""overrideReferences"": false,
            ""precompiledReferences"": [],
            ""autoReferenced"": true,
            ""defineConstraints"": [],
            ""versionDefines"": [],
            ""noEngineReferences"": false
            }}";

            File.WriteAllText(pathName, jsonContent, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(pathName);

            Object obj = AssetDatabase.LoadAssetAtPath(pathName, typeof(Object));
            ProjectWindowUtil.ShowCreatedAsset(obj);
        }
    }
}