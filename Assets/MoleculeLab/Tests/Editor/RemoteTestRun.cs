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
        /// <summary>Test classes that run without entering Play mode: a few seconds in total.</summary>
        const string QuickTests=@"^MoleculeLab\.Tests\.(ChemistryTests|ExpandedChemistryTests|FreeBuildTests|InventoryPlacementTests|MoleculeInfoTests|ReactionRuleTests|SceneConfigurationTests)\.";
        const string SavedOptions="MoleculeLab.SavedPlayModeOptions";
        /// <summary>
        /// Playthrough tests enter and leave Play mode once each; reloading the scripting domain every time costs 12-20 s.
        /// During a test run Play mode starts without a domain reload (the runtime code keeps no static game state),
        /// and the project's own setting is restored when the run finishes.
        /// </summary>
        internal static void FastPlayMode(bool on)
        {
            if(on)
            {
                if(SessionState.GetString(SavedOptions,"").Length==0)
                    SessionState.SetString(SavedOptions,(EditorSettings.enterPlayModeOptionsEnabled?"1":"0")+":"+(int)EditorSettings.enterPlayModeOptions);
                EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            }
            else
            {
                var saved=SessionState.GetString(SavedOptions,""); if(saved.Length==0) return;
                var parts=saved.Split(':'); EditorSettings.enterPlayModeOptionsEnabled=parts[0]=="1"; EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)int.Parse(parts[1]);
                SessionState.EraseString(SavedOptions);
            }
        }
        static RemoteTestRun()
        {
            var api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Callbacks());
            EditorApplication.update+=() =>
            {
                if(EditorApplication.timeSinceStartup<nextPoll) return; nextPoll=EditorApplication.timeSinceStartup+2;
                // A run that never reported back (e.g. it was asked for while Play mode was starting) must not block later requests.
                if(SessionState.GetBool(Running,false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    var started=SessionState.GetFloat(Running+".started",0);
                    if(started<=0 || EditorApplication.timeSinceStartup-started>20*60) SessionState.SetBool(Running,false);
                }
                if(!SessionState.GetBool(Running,false) && !EditorApplication.isPlayingOrWillChangePlaymode && !TestRunActive) FastPlayMode(false);
                if(!File.Exists(RequestFile) || SessionState.GetBool(Running,false) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                AssetDatabase.Refresh();
                if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                // Optional: the request file holds a regex of test names to run (e.g. "Squeezing|Beakers"),
                // or "quick" for the fast tests that need no Play mode (chemistry, reactions, free-build rules).
                var only=File.ReadAllText(RequestFile).Trim();
                if(only=="quick") only=QuickTests;
                File.Delete(RequestFile); File.WriteAllText(ResultFile,"RUNNING "+only+"\n"); SessionState.SetBool(Running,true); SessionState.SetFloat(Running+".started",(float)EditorApplication.timeSinceStartup);
                var filter=new Filter{testMode=TestMode.EditMode,assemblyNames=new[]{"MoleculeLab.Tests"}};
                if(only.Length>0) filter.groupNames=new[]{only};
                api.Execute(new ExecutionSettings(filter));
            };
        }
        /// <summary>True from RunStarted to RunFinished of any run (Test Runner window included).</summary>
        static bool TestRunActive;
        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { TestRunActive=true; FastPlayMode(true); }
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
                TestRunActive=false; FastPlayMode(false);
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
