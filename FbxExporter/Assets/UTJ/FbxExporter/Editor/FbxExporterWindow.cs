using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;


namespace UTJ.FbxExporter
{
    public class FbxExporterWindow : EditorWindow
    {
        [MenuItem("Window/Fbx Exporter")]
        public static void Open()
        {
            var window = EditorWindow.GetWindow<FbxExporterWindow>();
            window.titleContent = new GUIContent("Fbx Exporter");
            window.Show();
        }

        public enum Scope
        {
            Selected,
            EntireScene,
        }

        public class Record
        {
            public FbxExporter exporter;
            public string path;
            public DateTime started = DateTime.Now;
        }

        // ¼ÇÂ¼µÈ´ýµ¼³ö¶ÔÏó
        static HashSet<GameObject> waitExportObjects;
        //ÓÃÓÚÈ¥ÖØ
        static Dictionary<string, int> nameMaps = new Dictionary<string, int>();
        static string exportPath;

        static List<Record> s_records = new List<Record>();

        // ÊÇ·ñÕýÔÚÖ´ÐÐupdateº¯Êý
        static bool m_isPollAsync = false;

        Scope m_scope = Scope.Selected;
        bool m_includeChildren = true;
        bool m_isSeperateExport = false;
        static FbxExporter.Format m_format = FbxExporter.Format.FbxBinary;
        FbxExporter.ExportOptions m_opt = FbxExporter.ExportOptions.defaultValue;

        bool DoExport(string path, FbxExporter.Format format, GameObject[] objects)
        {
            var exporter = new FbxExporter(m_opt);
            exporter.CreateScene(System.IO.Path.GetFileName(path));

            foreach (var obj in objects)
                exporter.AddNode(obj);

            if(exporter.WriteAsync(path, format))
            {
                Debug.Log("Export started: " + path);
                s_records.Add(new Record {path = path, exporter = exporter});
                if (m_isPollAsync == false) { 
                    EditorApplication.update += PollAsyncWrite;
                    m_isPollAsync = true;
                }
                return true;
            }
            else
            {
                Debug.Log("Export failed: " + path);
                return false;
            }
        }

        static void PollAsyncWrite()
        {
            // poll async write
            bool finished = false;
            foreach (var record in s_records)
            {
                if (record.exporter.IsFinished())
                {
                    var elapsed = DateTime.Now - record.started;
                    Debug.Log("Export finished: " + record.path + " (" + elapsed.TotalSeconds + " seconds)");
                    finished = true;
                }
            }
            if (finished)
            {
                s_records.RemoveAll((a) => {
                    var isFinished = a.exporter.IsFinished();
                    if (isFinished) { 
                        a.exporter.Release();
                    }
                    return isFinished;
                });

                if (s_records.Count == 0 && m_isPollAsync == true) { 
                    EditorApplication.update -= PollAsyncWrite;
                    m_isPollAsync = false;
                }
            }
        }

        void addExportTask(GameObject taskObject) {
            string filename;

            string extension = "fbx";
            if (m_format == FbxExporter.Format.Obj) { extension = "obj"; }

            //²éÕÒmesh
            var meshFilterComponent = taskObject.GetComponent<MeshFilter>();
            if (meshFilterComponent && meshFilterComponent.sharedMesh.vertexCount > 0)
            {
                filename = meshFilterComponent.sharedMesh.name;

                // È¥µô" Instance"
                filename = filename.Replace(" Instance", "");

                //ÅÐ¶ÏÊÇ·ñµ¼³ö¹ý
                var isExported = nameMaps.ContainsKey(filename);

                if (isExported == false)
                {
                    nameMaps.Add(filename, 1);

                    var tmpPath = exportPath.Substring(0, exportPath.LastIndexOf("/") + 1) + filename + "." + extension;
                    //Debug.Log(tmpPath);

                    var tmpObjects = new HashSet<GameObject>();
                    tmpObjects.Add(taskObject.gameObject);
                    DoExport(tmpPath, m_format, tmpObjects.ToArray());
                }
                else
                {
                    Debug.Log(filename);
                }
            }
        }

        void PollAsyncAddExportTask()
        {
            // ÅÐ¶Ïµ±Ç°Ö´ÐÐµÄÈÎÎñÊýÁ¿
            var currentTaskCount = s_records.Count;
            if (currentTaskCount < 1) {
                var needAddCount = 1 - currentTaskCount;

                // ¼ÇÂ¼ÐèÒªÒÆ³ýµÄkey
                var needDelKets = new List<GameObject>();

                // Ìí¼ÓÈÎÎñ
                foreach (var taskObject in waitExportObjects)
                {
                    addExportTask(taskObject.gameObject);
                    needDelKets.Add(taskObject);

                    needAddCount--;
                    if (needAddCount <= 0)
                    {
                        break;
                    }
                }

                // ÒÆ³ýÌí¼Ó¹ýµÄÈÎÎñ
                foreach (var needDelObj in needDelKets) {
                    waitExportObjects.Remove(needDelObj);
                }
            }

            //ÅÐ¶ÏÊÇ·ñ»¹Ê£ÏÂÈÎÎñ
            if ( waitExportObjects.Count == 0)
            {
                EditorApplication.update -= PollAsyncAddExportTask;
                Debug.Log("Ìí¼ÓÈÎÎñ½áÊø.");
            }
        }

        void OnGUI()
        {
            m_format = (FbxExporter.Format)EditorGUILayout.EnumPopup("Format", m_format);

            EditorGUILayout.Space();

            m_scope = (Scope)EditorGUILayout.EnumPopup("Scope", m_scope);
            if(m_scope == Scope.Selected)
            {
                EditorGUI.indentLevel++;
                m_includeChildren = EditorGUILayout.Toggle("Include Children", m_includeChildren);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            m_isSeperateExport = EditorGUILayout.Toggle("Seperate Export", m_isSeperateExport);

            m_opt.transform = EditorGUILayout.Toggle("Include Transform", m_opt.transform);
            m_opt.flip_handedness = EditorGUILayout.Toggle("Flip Handedness", m_opt.flip_handedness);
            m_opt.flip_faces = EditorGUILayout.Toggle("Flip Faces", m_opt.flip_faces);
            m_opt.quadify = EditorGUILayout.Toggle("Quadify", m_opt.quadify);
            if (m_opt.quadify)
            {
                EditorGUI.indentLevel++;
                m_opt.quadify_full_search = EditorGUILayout.Toggle("Full Search", m_opt.quadify_full_search);
                m_opt.quadify_threshold_angle = EditorGUILayout.FloatField("Threshold Angle", m_opt.quadify_threshold_angle);
                EditorGUI.indentLevel--;
            }
            m_opt.scale_factor = EditorGUILayout.FloatField("Scale Factor", m_opt.scale_factor);
            m_opt.system_unit = (FbxExporter.SystemUnit)EditorGUILayout.EnumPopup("System Unit", m_opt.system_unit);

            EditorGUILayout.Space();

            if (GUILayout.Button("Export"))
            {
                var objects = new HashSet<GameObject>();

                if (m_scope == Scope.Selected)
                {
                    foreach (var o in Selection.gameObjects)
                        objects.Add(o);
                    if (m_includeChildren)
                    {
                        foreach (var o in Selection.gameObjects)
                        {
                            foreach (var c in o.GetComponentsInChildren<MeshRenderer>())
                                objects.Add(c.gameObject);
                            foreach (var c in o.GetComponentsInChildren<SkinnedMeshRenderer>())
                                objects.Add(c.gameObject);
                            foreach (var c in o.GetComponentsInChildren<Terrain>())
                                objects.Add(c.gameObject);
                        }
                    }
                }
                else
                {
                    foreach (var o in GameObject.FindObjectsOfType<MeshRenderer>())
                        objects.Add(o.gameObject);
                    foreach (var o in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
                        objects.Add(o.gameObject);
                    foreach (var o in GameObject.FindObjectsOfType<Terrain>())
                        objects.Add(o.gameObject);
                }

                if (objects.Count == 0)
                {
                    Debug.LogWarning("FbxExporter: Nothing to export");
                }
                else
                {
                    string extension = "fbx";
                    if (m_format == FbxExporter.Format.Obj) { extension = "obj"; }

                    string filename = EditorSceneManager.GetActiveScene().name;
                    if (m_scope == Scope.Selected || filename.Length == 0)
                    {
                        foreach (var o in objects)
                        {
                            filename = o.name;
                            break;
                        }
                    }

                    var path = EditorUtility.SaveFilePanel("Export ." + extension + " file", "", SanitizeForFileName(filename), extension);
                    if (path != null && path.Length > 0)
                    {
                        if (m_isSeperateExport)
                        {
                            exportPath = path;
                            waitExportObjects = objects;

                            EditorApplication.update += PollAsyncAddExportTask;
                        }
                        else
                        {
                            DoExport(path, m_format, objects.ToArray());
                        }
                    }
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

