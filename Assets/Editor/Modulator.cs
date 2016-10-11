//using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections;

//[EditorWindowTitle(title = "Console", useTypeNameAsIconName = true)]
public class Modulator : EditorWindow
{
    [MenuItem("Window/Modulator")]
    static public void ModulatorWindow()
    {
        EditorWindow.GetWindow(typeof(Modulator));
    }

    public class Module
    {
        public string name;
        public bool compiled;
        public Module(string name, bool compiled)
        {
            name = name.Replace('\\', '/');
            name = name.Substring(name.LastIndexOf('/') + 1);
            this.name = name;
            this.compiled = compiled;
        }
        public string orgName { get { return string.Format("Assets/Modules/{0}", name); } }
        public string dstName { get { return string.Format("Assets/WebPlayerTemplates/{0}", name); } }
    }
    List<Module> modules;
    [System.Serializable]
    public class Reference
    {
        public Component component;
        public string type;
        public Reference(Component component, string type)
        {
            this.component = component;
            this.type = type;
        }
    }
    Reference[] references;

    void GetModules()
    {
        if (modules == null) modules = new List<Module>();
        modules.Clear();
        foreach (var name in System.IO.Directory.GetDirectories("Assets/WebPlayerTemplates"))
            modules.Add(new Module(name, true));
        foreach (var name in System.IO.Directory.GetDirectories("Assets/Modules"))
            modules.Add(new Module(name, false));

        modules = modules.OrderBy(x => x.name).ToList();
    }

    Vector2 ModuleScroll;

    void OnGUI()
    {
        if (bWaitReload || EditorApplication.isCompiling) GUI.enabled = false;
        GetModules();

        ModuleScroll = GUILayout.BeginScrollView(ModuleScroll);
        foreach (var module in modules)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(module.name);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(module.compiled ? "DeModulate" : "Modulate", GUILayout.Width(100)))
            {
                if (module.compiled) DeCompile(module);
                else Compile(module);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    void DeCompile(Module module)
    {
        string dllName = string.Format("\"Assets/Modules/{0}.dll\"", module.name);

        if (System.IO.File.Exists(dllName))
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            Assembly asm = null;
            foreach (var a in assemblies)
            {
                if (a.GetName().Name == module.name)
                {
                    asm = a;
                    break;
                }
            }

            // Mira todos los tipos que sea dependientes de componentes.
            List<Reference> refs = new List<Reference>();
            System.Type[] types = asm.GetTypes();
            foreach (var type in types)
            {
                if (type.IsSubclassOf(typeof(Component)))
                {
                    Component[] components = Resources.FindObjectsOfTypeAll(type) as Component[];
                    foreach (var component in components)
                    {
                        refs.Add(new Reference(component, type.ToString()));
                        bWaitReload = true;
                    }
                }
            }
            references = refs.ToArray();
        }
        if (System.IO.Directory.Exists(module.dstName)) AssetDatabase.MoveAsset(module.dstName, module.orgName);

        if (System.IO.File.Exists(dllName)) AssetDatabase.DeleteAsset(dllName);
        module.compiled = false;
        AssetDatabase.Refresh();
    }

    Module currentModule;
    void Compile(Module module)
    {
        currentModule = module;
        // Compilo el directorio
        CompileDirectory(module.orgName);
    }

    System.Diagnostics.Process CompilingProcess;

    bool bIMCompiling = false;
    bool bWaitReload = false;
    MethodInfo methodAddComponent = null;
    string errorReturn;
    void Update()
    {
        if (bIMCompiling)
        {
            CompilingProcess.WaitForExit(100);
            if (CompilingProcess.HasExited)
            {
                CompilingProcess.WaitForExit();
                bIMCompiling = false;

                bool oneError = false;
                errorReturn += CompilingProcess.StandardOutput.ReadToEnd();
                string[] errors = errorReturn.Split('\n');
                foreach (var error in errors)
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        try
                        {
                            int a = error.IndexOf('(');
                            int b = error.IndexOf(')');
                            int c = error.IndexOf(' ', b + 3);
                            string[] rowcol = error.Substring(a + 1, (b - a) - 1).Split(',');
                            string type = error.Substring(b + 3, c - (b + 3)).ToLower();
                            if (type == "error")
                            {
                                LogPlayerBuildError(error, error.Substring(0, a), int.Parse(rowcol[0]), 0);
                            }
                        }
                        catch
                        {
                            Debug.LogError(error);
                        }
                    }
                }
                errorReturn = "";
                errorReturn += CompilingProcess.StandardError.ReadToEnd();
                errors = errorReturn.Split('\n');
                foreach (var error in errors)
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        try
                        {
                            int a = error.IndexOf('(');
                            int b = error.IndexOf(')');
                            int c = error.IndexOf(' ', b + 3);
                            string[] rowcol = error.Substring(a + 1, (b - a) - 1).Split(',');
                            string type = error.Substring(b + 3, c - (b + 3)).ToLower();
                            if (type == "error")
                            {
                                oneError = true;
                                LogPlayerBuildError(error, error.Substring(0, a), int.Parse(rowcol[0]), 0);
                            }
                        }
                        catch
                        {
                            Debug.LogError(error);
                        }
                    }
                }
                Debug.Log(">>>> Fin compilar "+outFile);
                if (!oneError)
                {
                    // Mira todos los tipos que sea dependientes de componentes.
                    List<Reference> refs = new List<Reference>();
                    var asm = Assembly.LoadFile(outFile);
                    System.Type[] types = asm.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.IsSubclassOf(typeof(Component)))
                        {
                            Component[] components = GetRefsByType(type.ToString());
                            foreach (var component in components)
                            {
                                refs.Add(new Reference(component, type.ToString()));
                                bWaitReload = true;
                            }
                        }
                    }
                    references = refs.ToArray();

                    if (System.IO.Directory.Exists(currentModule.orgName)) AssetDatabase.MoveAsset(currentModule.orgName, currentModule.dstName);
                    currentModule.compiled = true;
                    AssetDatabase.Refresh();

                }
                CompilingProcess = null;
            }
            else
            {
                if (CompilingProcess.StandardError.Peek() > 0)
                    errorReturn += CompilingProcess.StandardError.ReadToEnd();
            }

        }



        if (bWaitReload && !EditorApplication.isCompiling)
        {
            this.Repaint();
            bWaitReload = false;
            if (methodAddComponent == null)
            {
                var methods = typeof(GameObject).GetMethods();
                foreach (var m in methods)
                {
                    if (m.Name == "AddComponent" && m.IsGenericMethod)
                    {
                        methodAddComponent = m;
                        break;
                    }
                }
            }

            // Reasigna tipos.
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var r in references)
            {
                foreach (var asm in assemblies)
                {
                    System.Type theType = asm.GetType(r.type);
                    if (theType != null)
                    {
                        if (PrefabUtility.GetPrefabParent(r.component.gameObject) == null)
                        {
                            methodAddComponent.MakeGenericMethod(theType).Invoke(r.component.gameObject, null);
                            DestroyImmediate(r.component, true);
                        }
                    }
                }
            }
        }

    }

    void OnDestroy() {
        CompilingProcess.Kill();
        CompilingProcess.Close();
    }

    string GetRecursiveFiles(string directory, string mask, string current)
    {
        string[] directories = System.IO.Directory.GetDirectories(directory);
        foreach (var dir in directories)
            current = GetRecursiveFiles(dir, mask, current);
        string[] files = System.IO.Directory.GetFiles(directory, mask);
        foreach (var file in files)
            current += "\"" + file + "\" ";

        return current;
    }

    bool GetEneabledMeta(string meta, string value)
    {
        int idx = meta.IndexOf(value + ":");
        int idx2 = meta.IndexOf("enabled: ", idx) + 9;
        return meta[idx2] == '1';
    }


    const string outputPath = "Assets/Modules";

    string outFile;
    void CompileDirectory(string directory)
    {
        string dllName = directory.Substring(directory.LastIndexOf('/') + 1);

        outFile = string.Format("\"{0}/{1}.dll\"", outputPath, dllName);
        string files = "";
        files = GetRecursiveFiles(directory, "*.cs", files);

        if (!System.IO.Directory.Exists(outputPath))
            System.IO.Directory.CreateDirectory(outputPath);

        string[] plugins = System.IO.Directory.GetFiles("Assets", "*.dll", System.IO.SearchOption.AllDirectories);

        string refers = string.Format(" -r:\"{0}/Frameworks/Managed/UnityEngine.dll\" -r:\"{0}/Frameworks/Managed/UnityEditor.dll\"  ", EditorApplication.applicationContentsPath, Application.dataPath);
//        refers += string.Format(" -r:\"{0}/Frameworks/Mono/lib/mono/unity/mscorlib.dll\" -r:\"{0}//Frameworks/Mono/lib/mono/unity/.dll\"  ", EditorApplication.applicationContentsPath, Application.dataPath);
       
        foreach (var plug in plugins) {
            var tmp = plug.Replace('\\', '/');
            if (tmp.ToLower().Contains("plugins/")) {
                string meta = System.IO.File.ReadAllText(tmp + ".meta");
                if (GetEneabledMeta(meta, "Any") || GetEneabledMeta(meta, "Editor"))
                {
                    try
                    {
                        Assembly ret = Assembly.LoadFrom(plug);
                        refers += " \"-r:" + plug + "\"";
                    }
                    catch
                    {
                    }
                }
            }
        }

        errorReturn = "";
        CompilingProcess = new System.Diagnostics.Process();
        string exe = string.Format("{0}/Frameworks/Mono/bin/gmcs", EditorApplication.applicationContentsPath);
//        string exe = "/Applications/Unity/Unity514.app/Contents/Frameworks/Mono/bin/mcs";
        CompilingProcess.StartInfo.FileName = exe;
        CompilingProcess.StartInfo.Arguments = string.Format("{0} -target:library {1} -out:{2} /nowarn:436", refers, files, outFile);
        CompilingProcess.StartInfo.RedirectStandardInput = true;
        CompilingProcess.StartInfo.RedirectStandardOutput = true;
        CompilingProcess.StartInfo.RedirectStandardError = true;
        CompilingProcess.StartInfo.CreateNoWindow = true;
        CompilingProcess.StartInfo.UseShellExecute = false;

        CompilingProcess.Start();
        bIMCompiling = true;
    }

    Assembly code;
    Assembly editor;
    Component[] GetRefsByType(string type)
    {
        if (code == null || editor == null)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var tasm in assemblies)
            {
                if (tasm.GetName().Name == "Assembly-CSharp-Editor") editor = tasm;
                if (tasm.GetName().Name == "Assembly-CSharp") code = tasm;
            }
        }
        System.Type tmp = code.GetType(type);
        if (tmp == null) tmp = editor.GetType(type);
        if (tmp != null)
        {
            return Resources.FindObjectsOfTypeAll(tmp) as Component[];
        }
        return null;
    }

    MethodInfo LogPlayerBuildErrorMI;
    void LogPlayerBuildError(string message, string file, int line, int column)
    {
        if (LogPlayerBuildErrorMI == null)
            LogPlayerBuildErrorMI = typeof(Debug).GetMethod("LogPlayerBuildError", BindingFlags.Static | BindingFlags.NonPublic);
        LogPlayerBuildErrorMI.Invoke(null, new object[] { message, file, line, column });
    }



}

