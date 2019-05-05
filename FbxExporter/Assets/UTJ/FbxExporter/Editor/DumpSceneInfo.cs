using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;
using LitJson;

namespace Assets.UTJ.FbxExporter.Editor
{
    public struct MyVector3 {
        public int x;
        public int y;
        public int z;
    }
    public struct MyVector4
    {
        public int x;
        public int y;
        public int z;
        public int w;
    }

    public struct SceneInfo
    {
        public String Name;
        public String MeshName;
        public MyVector3 Position;
        public MyVector4 Rotation;
        public MyVector3 Scale;
        public List<SceneInfo> Children;
    }

    class DumpSceneInfoWindow : EditorWindow
    {
        [MenuItem("Window/DumpSceneInfo")]
        public static void Open()
        {
            var window = EditorWindow.GetWindow<DumpSceneInfoWindow>();
            window.titleContent = new GUIContent("Dump Scene Info");
            window.Show();
        }

        public SceneInfo GetSceneInfo(GameObject ParentObject)
        {
            SceneInfo tmpSceneInfo = new SceneInfo();

            tmpSceneInfo.Name = ParentObject.name;
            tmpSceneInfo.Position.x = (int)ParentObject.transform.position.x * 1000;
            tmpSceneInfo.Position.y = (int)ParentObject.transform.position.y * 1000;
            tmpSceneInfo.Position.z = (int)ParentObject.transform.position.z * 1000;

            tmpSceneInfo.Rotation.x = (int)ParentObject.transform.rotation.x * 1000;
            tmpSceneInfo.Rotation.y = (int)ParentObject.transform.rotation.y * 1000;
            tmpSceneInfo.Rotation.z = (int)ParentObject.transform.rotation.z * 1000;
            tmpSceneInfo.Rotation.w = (int)ParentObject.transform.rotation.w * 1000;

            tmpSceneInfo.Scale.x = (int)ParentObject.transform.localScale.x * 1000;
            tmpSceneInfo.Scale.y = (int)ParentObject.transform.localScale.y * 1000;
            tmpSceneInfo.Scale.z = (int)ParentObject.transform.localScale.z * 1000;

            // find mesh name
            var meshFilterComponent = ParentObject.GetComponent<MeshFilter>();
            if (meshFilterComponent && meshFilterComponent.mesh.vertexCount > 0)
            {
                tmpSceneInfo.MeshName = meshFilterComponent.mesh.name;
            }

            var childrenCount = ParentObject.transform.childCount;
            tmpSceneInfo.Children = new List<SceneInfo>();
            for (int i = 0; i < childrenCount; i ++) {
                var childTransform = ParentObject.transform.GetChild(i);
                var childSceneInfo = GetSceneInfo(childTransform.gameObject);
                tmpSceneInfo.Children.Add(childSceneInfo);
            }

            return tmpSceneInfo;
        }

        private int int64(float v)
        {
            throw new NotImplementedException();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Export"))
            {
                if (Selection.gameObjects.Length <= 0) {
                    return;
                }

                SceneInfo sceneInfo = GetSceneInfo(Selection.gameObjects[0]);

                // save to json file
                string filename = sceneInfo.Name;
                string extension = "json";
                var path = EditorUtility.SaveFilePanel("Export ." + extension + " file", "", SanitizeForFileName(filename), extension);
                if (path != null && path.Length > 0)
                {
                    string jsonString = JsonMapper.ToJson(sceneInfo);
                    File.WriteAllText(path, jsonString, Encoding.UTF8);
                }
            }
        }
        public static string SanitizeForFileName(string name)
        {
            var reg = new Regex("[\\/:\\*\\?<>\\|\\\"]");
            return reg.Replace(name, "_");
        }
    }
}