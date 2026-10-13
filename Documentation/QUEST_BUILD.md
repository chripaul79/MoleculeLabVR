# Quest 3 build and installation

Open this project using Unity 6000.6.2f1. Android Build Support, SDK/NDK and OpenJDK are installed with this editor. Keep Packages/manifest.json and packages-lock.json together when sharing the project.

## Build

1. Open Assets/Scenes/MoleculeLab.unity.
2. Select the Quest Development profile in File > Build Profiles. The target is standalone Android/Meta Quest, not desktop VR.
3. Run Molecule Lab > Validate Quest. Required OpenXR errors must be resolved before building.
4. Run Molecule Lab > Build Quest APK. Output: Builds/MoleculeLab-Quest3.apk. This is a development, debug-signed sideload build, not a store submission.

Command line (close the interactive editor first):

```sh
UNITY='/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD" -buildTarget Android -executeMethod MoleculeLab.Editor.QuestProjectSetup.BuildQuest -logFile Logs/QuestBuild.log
```

## Headset

Enable developer mode for your Quest using Meta's developer setup, connect by USB, and accept USB debugging inside the headset. Authentication and developer account steps must be completed by the headset owner.

```sh
ADB='/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
"$ADB" devices
"$ADB" install -r Builds/MoleculeLab-Quest3.apk
"$ADB" shell monkey -p com.moleculelab.vr 1
"$ADB" logcat -s Unity AndroidRuntime
```

Launch Molecule Lab VR from the headset's Unknown Sources application list. Enable hand tracking in headset settings. Controller and hand inputs share the XRI hands rig and its input modality manager.

## Performance

The target is 72 Hz / 72 FPS. Profile the Android player on the physical headset with Unity Profiler and Meta's performance tools. Editor framerate is not evidence of headset performance. Test the full lesson for 20 minutes, including repeated reactions, and record CPU/GPU frame times, thermal behavior, memory and tracking failures in ACCEPTANCE.md.

## Source control and recovery

The project already has a Unity Version Control (.plastic) workspace; it is preserved. Track Assets (including every .meta), Packages, ProjectSettings, Documentation and source-control ignore files. Ignore Library, Temp, Logs, Builds and Backups. No remote check-in is made automatically.

Backups/pre-molecule-lab-20260921.tar.gz contains the source project and the existing workspace metadata before implementation. Restore to a separate directory with Unity closed, then inspect before replacing anything. A Git ignore file is also supplied for an optional future Git migration; avoid running two active source-control workflows without agreeing this with the team.
