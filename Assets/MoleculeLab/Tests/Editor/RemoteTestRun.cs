using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MoleculeLab.Tests
{
    /// <summary>
    /// Creating Temp/MoleculeLab.run-tests runs the MoleculeLab.Tests suite in the open editor and writes a summary to
    /// Temp/MoleculeLab.test-results.txt (lets tools check the project without using the Test Runner window).
    /// </summary>
    [InitializeOnLoad]
    static class RemoteTestRun
    {
        const string RequestFile="Temp/MoleculeLab.run-tests", ResultFile="Temp/MoleculeLab.test-results.txt", Running="MoleculeLab.RemoteTestsRunning";
        static double nextPoll;
        static RemoteTestRun()
        {
            var api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Callbacks());
            EditorApplication.update+=() =>
            {
                if(EditorApplication.timeSinceStartup<nextPoll) return; nextPoll=EditorApplication.timeSinceStartup+2;
                if(!File.Exists(RequestFile) || SessionState.GetBool(Running,false) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                AssetDatabase.Refresh();
                if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                // Optional: the request file holds a regex of test names to run (e.g. "Squeezing|Beakers").
                var only=File.ReadAllText(RequestFile).Trim();
                File.Delete(RequestFile); File.WriteAllText(ResultFile,"RUNNING "+only+"\n"); SessionState.SetBool(Running,true);
                var filter=new Filter{testMode=TestMode.EditMode,assemblyNames=new[]{"MoleculeLab.Tests"}};
                if(only.Length>0) filter.groupNames=new[]{only};
                api.Execute(new ExecutionSettings(filter));
            };
        }
        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if(!SessionState.GetBool(Running,false) || result.HasChildren) return;
                var line=result.TestStatus+"  "+result.FullName+"  ("+result.Duration.ToString("0.0")+" s)\n";
                if(result.TestStatus==TestStatus.Failed) line+="    "+(result.Message??"").Replace("\n","\n    ")+"\n    "+FirstLines(result.StackTrace,4)+"\n";
                File.AppendAllText(ResultFile,line);
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                if(!SessionState.GetBool(Running,false)) return;
                SessionState.SetBool(Running,false);
                File.AppendAllText(ResultFile,$"DONE passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount}\n");
            }
            static string FirstLines(string text,int n)
            {
                if(string.IsNullOrEmpty(text)) return "";
                var lines=text.Split('\n'); var b=new StringBuilder();
                for(var i=0;i<lines.Length && i<n;i++) b.Append(lines[i].Trim()).Append(" | ");
                return b.ToString();
            }
        }
    }
}
